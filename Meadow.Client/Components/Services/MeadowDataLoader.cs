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

    /// <summary>Kuehe, Medikamente, Wie/Wo, Behandlungsgruende, Klauenbefunde, Euterviertel - alles, was Spalten aufloest.</summary>
    public async Task EnsureLookupsAsync()
    {
        await _settings.GetAllDataAsync();
        await _cows.GetAllDataAsync();
        await _medicines.GetAllDataAsync();
        await _whereHows.GetAllDataAsync();
        await _reasons.GetAllDataAsync();
        await _findings.GetAllDataAsync();
        await _udders.GetAllDataAsync();
    }

    public async Task EnsureCowTreatmentsAsync()
    {
        await EnsureLookupsAsync();
        await _cowTreatments.GetAllDataAsync();
    }

    public async Task EnsureClawTreatmentsAsync()
    {
        await EnsureLookupsAsync();
        await _clawTreatments.GetAllDataAsync();
    }

    public async Task EnsurePlannedCowAsync()
    {
        await EnsureLookupsAsync();
        await _plannedCow.GetAllDataAsync();
    }

    public async Task EnsurePlannedClawAsync()
    {
        await EnsureLookupsAsync();
        await _plannedClaw.GetAllDataAsync();
    }

    /// <summary>
    /// Die Kuh-Uebersicht: Tiere plus die beiden erfassten Behandlungsarten
    /// fuer die Zaehlspalten. Geplante Termine stehen dort nicht.
    /// </summary>
    public async Task EnsureCowOverviewAsync()
    {
        await EnsureLookupsAsync();
        await _cowTreatments.GetAllDataAsync();
        await _clawTreatments.GetAllDataAsync();
    }

    /// <summary>
    /// Die Kuh-Seite rechnet ueber alle vier Behandlungsarten, genau wie das
    /// Dashboard. Eigener Name statt eines Aufrufs von EnsureDashboardAsync,
    /// damit die Regel "eine Methode pro Seite" hier nicht bricht - und damit
    /// man sie aendern kann, ohne das Dashboard mitzuziehen.
    /// </summary>
    public Task EnsureCowProfileAsync() => EnsureDashboardAsync();

    /// <summary>Alles - das Dashboard rechnet ueber saemtliche Behandlungsarten.</summary>
    public async Task EnsureDashboardAsync()
    {
        await EnsureLookupsAsync();
        await _cowTreatments.GetAllDataAsync();
        await _clawTreatments.GetAllDataAsync();
        await _plannedCow.GetAllDataAsync();
        await _plannedClaw.GetAllDataAsync();
    }
}
