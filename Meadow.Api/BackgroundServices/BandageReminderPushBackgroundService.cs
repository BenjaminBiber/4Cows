using Meadow.Api.Infrastructure;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Meadow.Data.Services;
using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Api.BackgroundServices;

/// <summary>
/// Der server-initiierte Verband-Erinnerer: findet regelmaessig die
/// ueberfaelligen offenen Verbaende und schickt fuer jeden HOECHSTENS EINMAL JE
/// TAG eine Web-Push-Nachricht an alle gespeicherten Anmeldungen.
///
/// Gebaut wie <see cref="CowSyncBackgroundService"/>: PeriodicTimer,
/// CanConnect-Guard, OperationCanceledException still, alles andere geloggt und
/// nie nach aussen geworfen (eine entkommene Exception reisst den Host mit).
/// Intervall aus Env/Config mit Fallback.
///
/// Drei Bausteine, jeder aus einer frueheren Aufgabe und hier NUR zusammengefuehrt:
/// <list type="bullet">
/// <item>die EINE ueberfaellig-Regel aus Task 5
/// (<see cref="ClawTreatmentExtensions.OverdueBandages"/>) - keine zweite
/// Regel-Kopie;</item>
/// <item>der Sender aus Task 3 (<see cref="IPushSender"/>) samt Abraeumen toter
/// Anmeldungen bei 404/410;</item>
/// <item>die persistente Entdopplung aus der PushReminderLog-Tabelle, deren
/// Entscheidung als reine Funktion in
/// <see cref="PushLogic.ShouldSendReminderToday"/> steht.</item>
/// </list>
///
/// Das eigentliche Versenden ueber den Push-Dienst ist nur zur Laufzeit
/// pruefbar (echte VAPID-Keys, erreichbarer FCM/Mozilla/Apple-Dienst, echte
/// Anmeldung im Browser). Testbar - und getestet - ist die Entdopplungs-Regel.
/// </summary>
public sealed class BandageReminderPushBackgroundService : BackgroundService
{
    private readonly IClawTreatmentService _clawTreatments;
    private readonly ISettingsService _settings;
    private readonly IPushSender _pushSender;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly TimeSpan _interval;

    public BandageReminderPushBackgroundService(
        IClawTreatmentService clawTreatments,
        ISettingsService settings,
        IPushSender pushSender,
        IDbContextFactory<DatabaseContext> contextFactory)
    {
        _clawTreatments = clawTreatments;
        _settings = settings;
        _pushSender = pushSender;
        _contextFactory = contextFactory;
        // Stuendlicher Check als Fallback: die Entdopplung sorgt dafuer, dass
        // trotzdem hoechstens EINE Erinnerung je Verband und Tag rausgeht. Ein
        // kurzes Intervall faengt nur ab, dass ein am fruehen Morgen frisch
        // ueberfaellig gewordener Verband nicht bis zum naechsten Tageslauf
        // wartet. Ueber BandageReminderIntervalHours einstellbar - Muster wie
        // XLinkSyncIntervalHours im Vorbild.
        _interval = TimeSpan.FromHours(
            double.TryParse(Environment.GetEnvironmentVariable("BandageReminderIntervalHours"), out var hours) && hours > 0
                ? hours
                : 1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            await SendDueRemindersAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Push aus (VAPID unkonfiguriert) -> gar nichts tun. Kein Fehler,
            // kein DB-Zugriff, keine leeren Sendeversuche. Gleiche Haltung wie
            // im Sender selbst.
            if (!_pushSender.IsEnabled)
            {
                return;
            }

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                LoggerService.LogWarning(typeof(BandageReminderPushBackgroundService),
                    "Keine Datenbankverbindung. Verband-Erinnerung wird uebersprungen.");
                return;
            }

            // Frisch laden statt auf einen alten Cache zu vertrauen: dieser
            // Dienst ist die einzige Stelle, die den Klauen-Cache serverseitig
            // ausserhalb der Endpunkte liest, und ein stundenalter Stand
            // uebersaehe genau die eben faellig gewordenen Verbaende.
            await _clawTreatments.GetAllDataAsync();
            await _settings.GetAllDataAsync();

            var reminderDays = _settings.BandageRemovalReminderDays;
            var today = DateTime.Now;

            var overdue = ClawTreatmentExtensions
                .OverdueBandages(_clawTreatments.Treatments.Values, reminderDays, today)
                .ToList();

            if (overdue.Count == 0)
            {
                return;
            }

            // Die Merker der ueberfaelligen Behandlungen in einem Zug lesen -
            // eine Runde zur Datenbank statt einer je Behandlung.
            var overdueIds = overdue.Select(t => t.ClawTreatmentId).ToList();
            var lastSentByTreatment = await context.PushReminderLogs
                .Where(l => overdueIds.Contains(l.ClawTreatmentId))
                .ToDictionaryAsync(l => l.ClawTreatmentId, l => l.SentOn, cancellationToken);

            var sentCount = 0;
            foreach (var treatment in overdue)
            {
                DateTime? lastSentOn = lastSentByTreatment.TryGetValue(treatment.ClawTreatmentId, out var when)
                    ? when
                    : null;

                // Der Kern der Abnahme, als reine Regel: heute schon geschickt ->
                // nichts. Persistent, also greift die Regel auch nach einem
                // Neustart.
                if (!PushLogic.ShouldSendReminderToday(lastSentOn, today))
                {
                    continue;
                }

                await _pushSender.SendTreatmentReminderToAllAsync(treatment, cancellationToken);

                // Merker HOCHZIEHEN, auch wenn keine einzige Anmeldung existierte
                // oder alle tot waren: die Entdopplung entscheidet ueber "heute
                // schon versucht", nicht ueber Zustellung - Zustellung entscheidet
                // der Push-Dienst allein. Sonst versuchte jeder Lauf des Tages es
                // fuer denselben Verband erneut, sobald mal kein Geraet angemeldet
                // ist.
                await MarkSentAsync(context, treatment.ClawTreatmentId, today, cancellationToken);
                sentCount++;
            }

            if (sentCount > 0)
            {
                LoggerService.LogInformation(typeof(BandageReminderPushBackgroundService),
                    "Verband-Erinnerungen fuer {Count} Behandlung(en) versendet.", sentCount);
            }
        }
        catch (OperationCanceledException)
        {
            // Sauberes Herunterfahren, nichts zu loggen.
        }
        catch (Exception e)
        {
            LoggerService.LogError(typeof(BandageReminderPushBackgroundService),
                "Fehler beim Versenden der Verband-Erinnerungen: {@Message}", e, e.Message);
        }
    }

    /// <summary>
    /// Hebt den Entdopplungs-Merker der Behandlung auf HEUTE - Upsert ueber die
    /// Behandlungs-ID (zugleich der Primaerschluessel). Kein FirstOrDefault noetig
    /// als schneller Weg; die Behandlungs-ID ist der Schluessel, ein
    /// vorhandener Merker wird aktualisiert, ein fehlender angelegt.
    /// </summary>
    private static async Task MarkSentAsync(
        DatabaseContext context, int clawTreatmentId, DateTime today, CancellationToken cancellationToken)
    {
        var existing = await context.PushReminderLogs
            .FirstOrDefaultAsync(l => l.ClawTreatmentId == clawTreatmentId, cancellationToken);

        if (existing is null)
        {
            context.PushReminderLogs.Add(new PushReminderLog
            {
                ClawTreatmentId = clawTreatmentId,
                SentOn = today
            });
        }
        else
        {
            existing.SentOn = today;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
