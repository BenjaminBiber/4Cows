using System.Globalization;
using Meadow.Api.Infrastructure;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Meadow.Data.Services;
using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Api.BackgroundServices;

/// <summary>
/// Die taegliche Sammel-Erinnerung der Behandlungsplaene: hoechstens EINE Push
/// je Tag an alle Anmeldungen, mit der Zusammenfassung "x Kuehe heute faellig,
/// y Kuehe ueberfaellig". Klick oeffnet die Startseite.
///
/// Gebaut wie <see cref="BandageReminderPushBackgroundService"/> - PeriodicTimer,
/// Erstlauf beim Start, Env-Intervall mit Fallback, OperationCanceledException
/// still, alles andere geloggt und nie nach aussen geworfen (eine entkommene
/// Exception reisst den Host mit).
///
/// Zwei Unterschiede zum Verband-Dienst, beide fachlich:
/// <list type="bullet">
/// <item>Eine Sammelmeldung statt einer Push je Vorgang - zwei faellige Plaene
/// sind ein Gang in den Stall. Deshalb ist der Entdopplungs-Schluessel der TAG
/// und nicht die Behandlung (<see cref="TreatmentPlanPushLog"/>).</item>
/// <item>Ist nichts faellig, geht KEINE Push raus und es wird auch KEIN Merker
/// geschrieben - der Tag bleibt offen, falls spaeter am Tag doch noch etwas
/// faellig wird.</item>
/// </list>
///
/// Die Faelligkeit selbst entscheidet niemand hier: sie kommt aus
/// <see cref="TreatmentPlanRuns"/>, der Wortlaut aus
/// <see cref="TreatmentPlanReminderText"/>, die Entdopplung aus
/// <see cref="PushLogic.ShouldSendReminderToday"/>. Dieser Dienst fuehrt nur
/// zusammen.
/// </summary>
public sealed class TreatmentPlanPushBackgroundService : BackgroundService
{
    private readonly ICowTreatmentService _cowTreatments;
    private readonly IMedicineService _medicines;
    private readonly ICowService _cows;
    private readonly ITreatmentPlanCancellationService _cancellations;
    private readonly IPushSender _pushSender;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly TimeSpan _interval;

    public TreatmentPlanPushBackgroundService(
        ICowTreatmentService cowTreatments,
        IMedicineService medicines,
        ICowService cows,
        ITreatmentPlanCancellationService cancellations,
        IPushSender pushSender,
        IDbContextFactory<DatabaseContext> contextFactory)
    {
        _cowTreatments = cowTreatments;
        _medicines = medicines;
        _cows = cows;
        _cancellations = cancellations;
        _pushSender = pushSender;
        _contextFactory = contextFactory;
        // Stuendlich wie beim Verband: die Entdopplung sorgt dafuer, dass
        // trotzdem hoechstens EINE Push je Tag rausgeht. Das kurze Intervall
        // faengt nur ab, dass ein erst im Laufe des Tages faellig gewordener
        // Plan nicht bis morgen wartet.
        // Invariant geparst: auf einem deutschen Host laese die Standardkultur
        // "0.01" als Tausendertrennung und machte daraus 1 Stunde.
        _interval = TimeSpan.FromHours(
            double.TryParse(Environment.GetEnvironmentVariable("TreatmentPlanReminderIntervalHours"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var hours) && hours > 0
                ? hours
                : 1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            await SendDueReminderAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendDueReminderAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Push aus (VAPID unkonfiguriert) -> gar nichts tun. Kein Fehler,
            // kein DB-Zugriff. Gleiche Haltung wie im Verband-Dienst.
            if (!_pushSender.IsEnabled)
            {
                return;
            }

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                LoggerService.LogWarning(typeof(TreatmentPlanPushBackgroundService),
                    "Keine Datenbankverbindung. Behandlungsplan-Erinnerung wird uebersprungen.");
                return;
            }

            var today = DateTime.Today;

            // Frisch laden statt auf einen alten Cache zu vertrauen: ein
            // stundenalter Stand uebersaehe genau die eben faellig gewordenen
            // Gaben und kaeme mit einem schon abgebrochenen Plan.
            await _cowTreatments.GetAllDataAsync();
            await _medicines.GetAllDataAsync();
            await _cows.GetAllDataAsync();
            await _cancellations.GetAllDataAsync();

            var runs = TreatmentPlanRuns.Build(_cowTreatments.Treatments.Values, _medicines.Medicines,
                _cancellations.Cancellations.Values, today);
            var body = TreatmentPlanReminderText.PushSummary(TreatmentPlanRuns.Due(runs, _cows.Cows));
            if (body is null)
            {
                // Nichts faellig -> kein Senden, kein Merker.
                return;
            }

            var lastSentOn = await context.TreatmentPlanPushLogs
                .OrderByDescending(l => l.SentOn)
                .Select(l => (DateTime?)l.SentOn)
                .FirstOrDefaultAsync(cancellationToken);

            // Der Kern der Abnahme, als reine Regel: heute schon geschickt ->
            // nichts. Der Merker liegt in der Datenbank, also gilt das auch
            // ueber einen Neustart hinweg.
            if (!PushLogic.ShouldSendReminderToday(lastSentOn, today))
            {
                return;
            }

            var sent = await _pushSender.SendTreatmentPlanReminderToAllAsync(body, cancellationToken);

            // Merker HOCHZIEHEN, auch wenn keine einzige Anmeldung existierte -
            // die Entdopplung entscheidet ueber "heute schon versucht", nicht
            // ueber Zustellung. CancellationToken.None: nach erfolgtem Senden
            // MUSS der Merker landen, auch wenn der Host gerade herunterfaehrt,
            // sonst gaebe es beim naechsten Lauf eine zweite Push. Begruendung
            // wie im Verband-Dienst.
            context.TreatmentPlanPushLogs.Add(new TreatmentPlanPushLog { SentOn = today });
            await context.SaveChangesAsync(CancellationToken.None);

            LoggerService.LogInformation(typeof(TreatmentPlanPushBackgroundService),
                "Behandlungsplan-Erinnerung an {Count} Anmeldung(en) versendet: {Body}", sent, body);
        }
        catch (OperationCanceledException)
        {
            // Sauberes Herunterfahren, nichts zu loggen.
        }
        catch (Exception e)
        {
            LoggerService.LogError(typeof(TreatmentPlanPushBackgroundService),
                "Fehler beim Versenden der Behandlungsplan-Erinnerung: {@Message}", e, e.Message);
        }
    }
}
