using BB_Cow.Services;
using BB_KPI.Services;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace _4Cows_FE.Components.Services;

/// <summary>
/// Setzt die oeffentliche Demo-Instanz jede Nacht auf den Ausgangsbestand
/// zurueck. Nur im Demo-Modus registriert; dort ersetzt er den
/// CowSyncBackgroundService, der gegen Demo-Daten jede Kuh als IsGone
/// markieren wuerde.
///
/// Gebaut wie CowSyncBackgroundService: IDbContextFactory direkt injiziert
/// (alles, was hier gebraucht wird, ist Singleton), CanConnect-Guard,
/// OperationCanceledException still, alles andere geloggt und nie nach
/// aussen geworfen - eine entkommene Exception reisst den Host mit.
///
/// Ein Unterschied zum Vorbild: dort steht ein PeriodicTimer(24h), der beim
/// Start sofort feuert und danach im Takt des Prozessstarts driftet. Fuer
/// "nachts" ist das nichts, deshalb wird hier pro Runde die Spanne bis zur
/// naechsten Zielstunde berechnet.
/// </summary>
public sealed class DemoResetBackgroundService : BackgroundService
{
    /// <summary>
    /// Reihenfolge Kinder zuerst. Der Schemastand aus EF hat gar keine
    /// Fremdschluessel, Datenbanken aus dem alten Installationsskript
    /// 4Cows-DB-V3.sql aber sehr wohl - inklusive ON DELETE CASCADE auf
    /// Cow(Ear_Tag_Number). Deshalb defensiv loeschen.
    ///
    /// Bewusst NICHT dabei:
    /// - Udder: die Zeile "kein Viertel" muss auf ID 16 bleiben, sonst
    ///   liefert das mitgelieferte KPI "Meist behandeltes Viertel" (WHERE
    ///   UDDER_ID != 16) still falsche Werte.
    /// - KPI und AppSetting: DataSeeder legt beide nur beim Prozessstart an
    ///   (AnyAsync-Guard bzw. Diff je Schluessel). Ein naechtlicher Wipe
    ///   liesse sie bis zum naechsten Neustart leer.
    /// - LOGS: gehoert dem Serilog-Sink, EF kennt die Tabelle nicht.
    /// </summary>
    private static readonly string[] TablesToClear =
    {
        "Claw_Treatment",
        "Cow_Treatment",
        "Planned_Claw_Treatment",
        "Planned_Cow_Treatment",
        "Cow",
        "Medicine",
        "WhereHow"
    };

    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DemoSettings _demo;

    private readonly SettingsService _settings;
    private readonly CowService _cows;
    private readonly MedicineService _medicines;
    private readonly WhereHowService _whereHows;
    private readonly UdderService _udders;
    private readonly CowTreatmentService _cowTreatments;
    private readonly ClawTreatmentService _clawTreatments;
    private readonly PCowTreatmentService _plannedCow;
    private readonly PClawTreatmentService _plannedClaw;
    private readonly KPIService _kpis;

    public DemoResetBackgroundService(
        IDbContextFactory<DatabaseContext> contextFactory,
        DemoSettings demo,
        SettingsService settings,
        CowService cows,
        MedicineService medicines,
        WhereHowService whereHows,
        UdderService udders,
        CowTreatmentService cowTreatments,
        ClawTreatmentService clawTreatments,
        PCowTreatmentService plannedCow,
        PClawTreatmentService plannedClaw,
        KPIService kpis)
    {
        _contextFactory = contextFactory;
        _demo = demo;
        _settings = settings;
        _cows = cows;
        _medicines = medicines;
        _whereHows = whereHows;
        _udders = udders;
        _cowTreatments = cowTreatments;
        _clawTreatments = clawTreatments;
        _plannedCow = plannedCow;
        _plannedClaw = plannedClaw;
        _kpis = kpis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kein Reset beim Start: der Seeder in Program.cs hat gerade erst
        // frische Daten gelegt.
        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = UntilNext(_demo.ResetHour);

            // Zeitpunkt statt Dauer: eine auf Stunden gerundete Wartezeit
            // meldet kurz vor dem Termin "in 0 Stunden".
            LoggerService.LogInformation(typeof(DemoResetBackgroundService),
                "Naechster Demo-Reset am {@When} (in {@Hours}h {@Minutes}min).",
                DateTime.Now.Add(wait).ToString("dd.MM. HH:mm"),
                (int)wait.TotalHours, wait.Minutes);

            try
            {
                await Task.Delay(wait, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await ResetAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Spanne bis zur naechsten Zielstunde in Ortszeit. Liegt sie heute
    /// schon hinter uns, zaehlt der morgige Termin.
    /// </summary>
    private static TimeSpan UntilNext(int hour)
    {
        var now = DateTime.Now;
        var next = now.Date.AddHours(hour);

        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }

    private async Task ResetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                LoggerService.LogWarning(typeof(DemoResetBackgroundService),
                    "Keine Datenbankverbindung. Demo-Reset wird uebersprungen.");
                return;
            }

            await ClearAsync(context, cancellationToken);
            await DemoDataSeeder.SeedAsync(context);
            await ReloadCachesAsync();

            LoggerService.LogInformation(typeof(DemoResetBackgroundService),
                "Demo-Daten zurueckgesetzt und neu erzeugt.");
        }
        catch (OperationCanceledException)
        {
            // Sauberes Herunterfahren, nichts zu loggen.
        }
        catch (Exception e)
        {
            LoggerService.LogError(typeof(DemoResetBackgroundService),
                "Fehler beim Demo-Reset: {@Message}", e, e.Message);
        }
    }

    /// <summary>
    /// Leert die Bewegungsdaten.
    ///
    /// DELETE FROM statt TRUNCATE: TRUNCATE ist in MariaDB DDL, committet
    /// implizit, laesst sich nicht zuruecknehmen und scheitert an
    /// Fremdschluesselverweisen. Und keinesfalls DROP - MigrationHelper
    /// erkennt eine bereits migrierte Datenbank an der Existenz der Tabelle
    /// Cow; waere sie weg und __EFMigrationsHistory gefuellt, ueberspringt
    /// MigrateAsync das Schema und es kommt nicht wieder.
    ///
    /// FOREIGN_KEY_CHECKS = 0 um das Ganze herum, wie es auch die Migration
    /// AddCowIdAndIsCalv macht: Legacy-Datenbanken tragen Fremdschluessel,
    /// die das EF-Modell gar nicht kennt.
    /// </summary>
    private static async Task ClearAsync(DatabaseContext context, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;", cancellationToken);

        foreach (var table in TablesToClear)
        {
            // EF1002 warnt vor interpolierten Strings in ExecuteSqlRaw. Hier
            // ist der eingesetzte Wert ein Tabellenname aus der Konstante
            // TablesToClear oben - ein Bezeichner also, den man ohnehin nicht
            // parametrisieren koennte, und er stammt aus keiner Eingabe.
#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync($"DELETE FROM `{table}`;", cancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE `{table}` AUTO_INCREMENT = 1;", cancellationToken);
#pragma warning restore EF1002
        }

        await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;", cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Alle zehn Datenservices sind prozessweite Singletons mit
    /// Immutable-Caches, und die Datenschicht kennt kein einziges
    /// Aenderungs-Event. Ohne diesen Schritt zeigt jeder Circuit, der die
    /// Nacht ueber offen stand, Daten von vor dem Reset - mit IDs, die es
    /// nicht mehr gibt. GetAllDataAsync ersetzt den Cache jeweils komplett.
    ///
    /// Reihenfolge wie in MeadowDataLoader.EnsureDashboardAsync, plus KPI.
    /// MeadowDataLoader selbst ist Scoped und laesst sich hier nicht
    /// injizieren.
    /// </summary>
    private async Task ReloadCachesAsync()
    {
        await _settings.GetAllDataAsync();
        await _cows.GetAllDataAsync();
        await _medicines.GetAllDataAsync();
        await _whereHows.GetAllDataAsync();
        await _udders.GetAllDataAsync();
        await _cowTreatments.GetAllDataAsync();
        await _clawTreatments.GetAllDataAsync();
        await _plannedCow.GetAllDataAsync();
        await _plannedClaw.GetAllDataAsync();
        await _kpis.GetAllDataAsync();
    }
}
