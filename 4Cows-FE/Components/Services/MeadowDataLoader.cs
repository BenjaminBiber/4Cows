using BB_Cow.Services;

namespace _4Cows_FE.Components.Services;

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
    private readonly CowService _cows;
    private readonly MedicineService _medicines;
    private readonly WhereHowService _whereHows;
    private readonly TreatmentReasonService _reasons;
    private readonly UdderService _udders;
    private readonly CowTreatmentService _cowTreatments;
    private readonly ClawTreatmentService _clawTreatments;
    private readonly PCowTreatmentService _plannedCow;
    private readonly PClawTreatmentService _plannedClaw;
    private readonly SettingsService _settings;

    public MeadowDataLoader(
        CowService cows,
        MedicineService medicines,
        WhereHowService whereHows,
        TreatmentReasonService reasons,
        UdderService udders,
        CowTreatmentService cowTreatments,
        ClawTreatmentService clawTreatments,
        PCowTreatmentService plannedCow,
        PClawTreatmentService plannedClaw,
        SettingsService settings)
    {
        _cows = cows;
        _medicines = medicines;
        _whereHows = whereHows;
        _reasons = reasons;
        _udders = udders;
        _cowTreatments = cowTreatments;
        _clawTreatments = clawTreatments;
        _plannedCow = plannedCow;
        _plannedClaw = plannedClaw;
        _settings = settings;
    }

    /// <summary>Kuehe, Medikamente, Wie/Wo, Behandlungsgruende, Euterviertel - alles, was Spalten aufloest.</summary>
    public async Task EnsureLookupsAsync()
    {
        await _settings.GetAllDataAsync();
        await _cows.GetAllDataAsync();
        await _medicines.GetAllDataAsync();
        await _whereHows.GetAllDataAsync();
        await _reasons.GetAllDataAsync();
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
