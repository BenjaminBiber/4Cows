using Meadow.Shared.Services;

namespace Meadow.Client.Components.Services;

/// <summary>
/// Ein Ort, an dem steht, welche Caches eine Seite braucht.
///
/// Hintergrund: die Datenservices sind Singletons mit prozessweitem Cache,
/// und jede Seite rief bisher selbst zusammen, was sie laden muss. Zwei
/// Seiten haben dabei Lookups vergessen - mit der Folge, dass
/// CowService.FilterFuncCow bei unbekannter Cow_ID <c>false</c> liefert und
/// beim Direktaufruf der Seite ALLE Zeilen wegfiltert.
///
/// Deshalb ruft jede Seite hier genau eine Methode auf.
/// </summary>
public sealed class MeadowDataLoader
{
    private readonly ICowService _cows;
    private readonly IMedicineService _medicines;
    private readonly IWhereHowService _whereHows;
    private readonly ITreatmentReasonService _reasons;
    private readonly IClawFindingService _findings;
    private readonly IUdderService _udders;
    private readonly ICowTreatmentService _cowTreatments;
    private readonly IClawTreatmentService _clawTreatments;
    private readonly IPCowTreatmentService _plannedCow;
    private readonly IPClawTreatmentService _plannedClaw;
    private readonly ISettingsService _settings;

    public MeadowDataLoader(
        ICowService cows,
        IMedicineService medicines,
        IWhereHowService whereHows,
        ITreatmentReasonService reasons,
        IClawFindingService findings,
        IUdderService udders,
        ICowTreatmentService cowTreatments,
        IClawTreatmentService clawTreatments,
        IPCowTreatmentService plannedCow,
        IPClawTreatmentService plannedClaw,
        ISettingsService settings)
    {
        _cows = cows;
        _medicines = medicines;
        _whereHows = whereHows;
        _reasons = reasons;
        _findings = findings;
        _udders = udders;
        _cowTreatments = cowTreatments;
        _clawTreatments = clawTreatments;
        _plannedCow = plannedCow;
        _plannedClaw = plannedClaw;
        _settings = settings;
    }

    private enum Cache { Lookups, CowTreatments, ClawTreatments, PlannedCow, PlannedClaw }

    private readonly HashSet<Cache> _done = new();
    private readonly Dictionary<Cache, Task> _inFlight = new();

    /// <summary>
    /// Laedt einen Cache hoechstens einmal pro Datenstand.
    ///
    /// Im Serverbetrieb war ein erneutes GetAllDataAsync eine Datenbankrunde im
    /// selben Prozess - unangenehm, aber unsichtbar. Ueber HTTP ist jeder
    /// Aufruf eine Netzrunde, und jede Seite ruft hier eine Ensure-Methode auf:
    /// gemessen waren das sieben Nachschlagetabellen PRO Seitenwechsel.
    ///
    /// Die Logik sitzt bewusst HIER und nicht in den Diensten. 26 Stellen in
    /// den .razor-Dateien rufen GetAllDataAsync() direkt auf, und rund elf davon
    /// sind bewusste Neuladungen NACH einer Aenderung. Wuerde man dort
    /// memoisieren, wuerden genau die still zu Leeraufrufen, und die Tabelle
    /// hoerte nach dem Speichern auf, sich zu aktualisieren.
    ///
    /// _inFlight fasst gleichzeitige Aufrufer auf denselben Cache zusammen -
    /// beim Seitenstart laufen mehrere Ensure-Methoden nebeneinander.
    /// </summary>
    private Task Once(Cache cache, Func<Task> load)
    {
        if (_done.Contains(cache))
        {
            return Task.CompletedTask;
        }

        if (_inFlight.TryGetValue(cache, out var running))
        {
            return running;
        }

        var task = RunAsync(cache, load);
        _inFlight[cache] = task;
        return task;
    }

    private async Task RunAsync(Cache cache, Func<Task> load)
    {
        try
        {
            await load();
            _done.Add(cache);
        }
        finally
        {
            _inFlight.Remove(cache);
        }
    }

    /// <summary>
    /// Verwirft alles Geladene. Gedacht fuer den Fall, dass X-Data-Version eine
    /// fremde Aenderung meldet.
    /// </summary>
    public void Invalidate()
    {
        _done.Clear();
    }

    /// <summary>Kuehe, Medikamente, Wie/Wo, Behandlungsgruende, Klauenbefunde, Euterviertel - alles, was Spalten aufloest.</summary>
    public Task EnsureLookupsAsync() => Once(Cache.Lookups, () => Task.WhenAll(
        _settings.GetAllDataAsync(),
        _cows.GetAllDataAsync(),
        _medicines.GetAllDataAsync(),
        _whereHows.GetAllDataAsync(),
        _reasons.GetAllDataAsync(),
        _findings.GetAllDataAsync(),
        _udders.GetAllDataAsync()));

    public Task EnsureCowTreatmentsAsync() => Task.WhenAll(
        EnsureLookupsAsync(),
        Once(Cache.CowTreatments, _cowTreatments.GetAllDataAsync));

    public Task EnsureClawTreatmentsAsync() => Task.WhenAll(
        EnsureLookupsAsync(),
        Once(Cache.ClawTreatments, _clawTreatments.GetAllDataAsync));

    public Task EnsurePlannedCowAsync() => Task.WhenAll(
        EnsureLookupsAsync(),
        Once(Cache.PlannedCow, _plannedCow.GetAllDataAsync));

    public Task EnsurePlannedClawAsync() => Task.WhenAll(
        EnsureLookupsAsync(),
        Once(Cache.PlannedClaw, _plannedClaw.GetAllDataAsync));

    /// <summary>
    /// Die Kuh-Uebersicht: Tiere plus die beiden erfassten Behandlungsarten
    /// fuer die Zaehlspalten. Geplante Termine stehen dort nicht.
    /// </summary>
    public Task EnsureCowOverviewAsync() => Task.WhenAll(
        EnsureLookupsAsync(),
        Once(Cache.CowTreatments, _cowTreatments.GetAllDataAsync),
        Once(Cache.ClawTreatments, _clawTreatments.GetAllDataAsync));

    /// <summary>
    /// Die Kuh-Seite rechnet ueber alle vier Behandlungsarten, genau wie das
    /// Dashboard. Eigener Name statt eines Aufrufs von EnsureDashboardAsync,
    /// damit die Regel "eine Methode pro Seite" hier nicht bricht - und damit
    /// man sie aendern kann, ohne das Dashboard mitzuziehen.
    /// </summary>
    public Task EnsureCowProfileAsync() => EnsureDashboardAsync();

    /// <summary>Alles - das Dashboard rechnet ueber saemtliche Behandlungsarten.</summary>
    public Task EnsureDashboardAsync() => Task.WhenAll(
        EnsureLookupsAsync(),
        Once(Cache.CowTreatments, _cowTreatments.GetAllDataAsync),
        Once(Cache.ClawTreatments, _clawTreatments.GetAllDataAsync),
        Once(Cache.PlannedCow, _plannedCow.GetAllDataAsync),
        Once(Cache.PlannedClaw, _plannedClaw.GetAllDataAsync));
}
