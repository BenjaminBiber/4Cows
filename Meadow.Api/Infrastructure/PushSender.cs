using Meadow.Data.Services;
using Meadow.Data.Sql;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.EntityFrameworkCore;
using WebPush;
using DbPushSubscription = Meadow.Shared.Models.PushSubscription;
using WebPushSubscription = WebPush.PushSubscription;

namespace Meadow.Api.Infrastructure;

/// <summary>
/// Die Naht, ueber die server-initiiertes Web Push hinausgeht. Task 3 baut nur
/// diesen Kanal; Task 6 haengt hier die fachliche Verband-Erinnerung an.
/// </summary>
public interface IPushSender
{
    /// <summary>Ob Push ueberhaupt konfiguriert ist (VAPID vollstaendig).</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Schickt eine Nachricht an EINE gespeicherte Anmeldung. Ist die Anmeldung
    /// beim Push-Dienst tot (404/410), wird ihre Zeile entfernt und
    /// <c>false</c> zurueckgegeben. <c>true</c> heisst "angenommen", nicht
    /// "zugestellt" - Zustellung entscheidet der Push-Dienst spaeter allein.
    /// </summary>
    Task<bool> SendAsync(DbPushSubscription subscription, string title, string body, string? url = null, CancellationToken ct = default);

    /// <summary>
    /// Der fachliche Sendeweg (Task 6): verschickt fuer eine ueberfaellige
    /// Klauenbehandlung an ALLE gespeicherten Anmeldungen eine
    /// Verband-Erinnerung und meldet, an wie viele sie ging. Tote Anmeldungen
    /// fliegen dabei raus.
    ///
    /// <paramref name="today"/> wird vom Scheduler uebergeben, damit die
    /// Anzeige der ueberfaelligen Tage denselben Zeitbezug hat wie die
    /// Faelligkeits-Entscheidung (keine Mitternacht-Abweichung).
    ///
    /// Ausschliesslich serverintern aufrufbar - der BackgroundService ruft ihn
    /// direkt. Es gibt bewusst KEINEN oeffentlichen Endpunkt mehr, der ein
    /// Rundum-Senden an alle Anmeldungen von aussen ausloest (der fruehere
    /// POST /api/push/test ist mit Task 6 entfallen).
    /// </summary>
    Task<int> SendTreatmentReminderToAllAsync(ClawTreatment treatment, DateTime today, CancellationToken ct = default);
}

/// <summary>
/// Verschickt Web Push ueber die WebPush-Lib und raeumt tote Anmeldungen ab.
///
/// Singleton wie die uebrigen Zustandsdienste, haelt aber selbst keinen Cache:
/// die Anmeldungen liegen ausschliesslich in der Datenbank, die je Sendung frisch
/// gelesen wird. Ein alter Cache wuerde hier an einen Endpoint senden, den der
/// Nutzer laengst abgemeldet hat - genau der Fehler, den das Loeschen bei 410
/// vermeidet.
/// </summary>
public sealed class WebPushSender : IPushSender
{
    private readonly PushOptions _options;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatus;
    private readonly VapidDetails? _vapid;

    public WebPushSender(
        PushOptions options,
        IDbContextFactory<DatabaseContext> contextFactory,
        DatabaseStatusService databaseStatus)
    {
        _options = options;
        _contextFactory = contextFactory;
        _databaseStatus = databaseStatus;

        // Nur wenn vollstaendig konfiguriert. VapidDetails validiert die
        // Schluessellaengen im Konstruktor und wuerde bei leeren Werten werfen -
        // deshalb hier gar nicht erst bauen, wenn Push aus ist.
        _vapid = _options.Enabled
            ? new VapidDetails(_options.Subject, _options.PublicKey, _options.PrivateKey)
            : null;
    }

    public bool IsEnabled => _vapid is not null;

    public async Task<bool> SendAsync(
        DbPushSubscription subscription, string title, string body, string? url = null, CancellationToken ct = default)
    {
        if (_vapid is null)
        {
            // Push nicht konfiguriert - kein Fehler, nur nichts zu tun.
            return false;
        }

        // Der Payload, den der service-worker.js im push-Handler auspackt:
        // title/body fuer showNotification, url fuer notificationclick.
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title,
            body,
            url
        });

        var client = new WebPushClient();
        var target = new WebPushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);

        try
        {
            await client.SendNotificationAsync(target, payload, _vapid, ct);
            return true;
        }
        catch (WebPushException ex)
        {
            // 404/410: der Push-Dienst kennt diesen Endpoint nicht mehr - die
            // Anmeldung ist tot und muss weg, sonst laeuft jede kuenftige
            // Sendung erneut in denselben Fehler. Die Entscheidung, WELCHE Codes
            // "tot" heissen, steht als reine Regel in PushLogic.IsGone.
            if (PushLogic.IsGone((int)ex.StatusCode))
            {
                await RemoveDeadAsync(subscription.Endpoint, ct);
            }
            else
            {
                LoggerService.LogError(typeof(WebPushSender),
                    "Web-Push an {Endpoint} fehlgeschlagen ({Status}).", ex,
                    subscription.Endpoint, ex.StatusCode);
            }

            return false;
        }
    }

    public Task<int> SendTreatmentReminderToAllAsync(ClawTreatment treatment, DateTime today, CancellationToken ct = default)
    {
        // Text wortgleich zum In-App-Hinweis (BandageReminderNoticeProvider),
        // damit Nutzer denselben Wortlaut auf beiden Kanaelen sehen. Die
        // ueberfaellig-Tage sind reine Anzeige; die Faelligkeit selbst hat die
        // Shared-Regel im Scheduler schon entschieden. "today" kommt vom
        // Scheduler - einmaliger Zeitbezug, keine Mitternacht-Abweichung.
        var overdueDays = (today.Date - treatment.TreatmentDate.Date).Days;
        var seit = overdueDays == 1 ? "seit 1 Tag" : $"seit {overdueDays} Tagen";

        return BroadcastAsync(
            title: "Verband-Erinnerung",
            body: $"Verband an Ohrmarke {treatment.EarTagNumber} liegt {seit} - bitte abnehmen.",
            url: "/Verband_Daten",
            ct);
    }

    /// <summary>
    /// Verschickt EINE Nachricht an ALLE gespeicherten Anmeldungen und meldet, an
    /// wie viele sie ging; tote Anmeldungen fliegen ueber <see cref="SendAsync"/>
    /// dabei raus. Serverinterner Rundum-Sendeweg - es gibt bewusst keinen
    /// oeffentlichen Endpunkt mehr, der ihn von aussen ausloest.
    /// </summary>
    private async Task<int> BroadcastAsync(string title, string body, string? url, CancellationToken ct)
    {
        if (_vapid is null)
        {
            return 0;
        }

        List<DbPushSubscription> all;
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            all = await context.PushSubscriptions.AsNoTracking().ToListAsync(ct);
            _databaseStatus.ReportSuccess();
        }
        catch (Exception ex)
        {
            _databaseStatus.ReportFailure();
            LoggerService.LogError(typeof(WebPushSender),
                "Anmeldungen fuer Push konnten nicht gelesen werden: {Message}", ex, ex.Message);
            return 0;
        }

        var sent = 0;
        foreach (var subscription in all)
        {
            if (await SendAsync(subscription, title, body, url, ct))
            {
                sent++;
            }
        }

        return sent;
    }

    /// <summary>
    /// Loescht eine tote Anmeldung anhand ihres Endpoints. Fehlertolerant: das
    /// Loeschen darf die eigentliche Sendung nicht zum 500 machen.
    /// </summary>
    private async Task RemoveDeadAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            await context.PushSubscriptions
                .Where(s => s.Endpoint == endpoint)
                .ExecuteDeleteAsync(ct);
            _databaseStatus.ReportSuccess();
            LoggerService.LogInformation(typeof(WebPushSender),
                "Tote Push-Anmeldung entfernt: {Endpoint}", endpoint);
        }
        catch (Exception ex)
        {
            _databaseStatus.ReportFailure();
            LoggerService.LogError(typeof(WebPushSender),
                "Tote Push-Anmeldung {Endpoint} konnte nicht entfernt werden: {Message}", ex, endpoint, ex.Message);
        }
    }
}
