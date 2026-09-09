using BB_Cow.Class;
using BB_Cow.Services;

namespace BB_Cow.Kpi;

/// <summary>
/// Connects the pure evaluator to the running application: implements <see cref="IKpiLookups"/> on
/// top of the ten singleton data services and builds the rows for one source on demand.
///
/// NOT memoised, on purpose. The data layer has no change event of any kind - the comment on
/// DemoResetBackgroundService.ReloadCachesAsync says so explicitly - so a cache here would need an
/// invalidation hook in ten services and would go stale the moment one of them was missed. Building
/// a few thousand rows out of dictionaries that are already in memory costs microseconds and can
/// never be out of date. Please do not "optimise" this into a cache.
/// </summary>
public sealed class KpiRowProvider : IKpiLookups
{
    private readonly CowService _cows;
    private readonly MedicineService _medicines;
    private readonly WhereHowService _whereHows;
    private readonly UdderService _udders;

    // CowTreatmentService sits in the global namespace while its nine siblings are BB_Cow.Services.
    // Nothing to fix here, but that is why there is no using for it.
    private readonly CowTreatmentService _cowTreatments;
    private readonly ClawTreatmentService _clawTreatments;
    private readonly PCowTreatmentService _plannedCowTreatments;
    private readonly PClawTreatmentService _plannedClawTreatments;

    public KpiRowProvider(
        CowService cows,
        MedicineService medicines,
        WhereHowService whereHows,
        UdderService udders,
        CowTreatmentService cowTreatments,
        ClawTreatmentService clawTreatments,
        PCowTreatmentService plannedCowTreatments,
        PClawTreatmentService plannedClawTreatments)
    {
        _cows = cows;
        _medicines = medicines;
        _whereHows = whereHows;
        _udders = udders;
        _cowTreatments = cowTreatments;
        _clawTreatments = clawTreatments;
        _plannedCowTreatments = plannedCowTreatments;
        _plannedClawTreatments = plannedClawTreatments;
    }

    /// <summary>Rows of one source, built from the current caches.</summary>
    public IReadOnlyList<KpiRow> Rows(KpiSourceId source)
    {
        var info = KpiSourceRegistry.Find(source);
        return info is null ? Array.Empty<KpiRow>() : info.BuildRows(this);
    }

    // ---- IKpiLookups ---------------------------------------------------

    public string CollarLabel(string cowId)
    {
        var collar = _cows.GetCollarNumberByCowId(cowId);
        // int.MinValue is this service's "unknown" marker, not a collar number.
        return collar == int.MinValue ? string.Empty : collar.ToString();
    }

    // These may return the literal "--" for an unknown id. Passed through as-is: KpiSourceRegistry
    // drops that placeholder when it builds the tag, so the rule holds for every IKpiLookups
    // implementation rather than only for this one.
    public string MedicineName(int medicineId) => _medicines.GetMedicineNameById(medicineId);

    // Leerer Rueckfallwert, nicht "ml": eine fehlende Einheit soll die
    // Mischungspruefung in KpiEvaluator nicht als zweite Einheit auslegen.
    public string MedicineDosageUnit(int medicineId) => _medicines.GetDosageUnit(medicineId, "");

    public string WhereHowName(int whereHowId) => _whereHows.GetWhereHowNameById(whereHowId);

    /// <summary>
    /// Reuses WhereHowService.GetUdderString, which is what the tables already display, so a tile
    /// and a table never spell the same udder differently. It returns "" for the all-false sentinel
    /// row - and that empty string is exactly what replaces the hardcoded "WHERE UDDER_ID != 16".
    ///
    /// Only the surrounding parentheses are stripped: "(LV/ RH)" reads as a suffix behind a
    /// where/how name, but a KPI tile shows the value on its own.
    /// </summary>
    public string UdderLabel(int udderId)
        => _whereHows.GetUdderString(_udders.GetById(udderId)).Trim('(', ')').Trim();

    public IEnumerable<Cow> Cows => _cows.Cows.Values;

    public IEnumerable<CowTreatment> CowTreatments => _cowTreatments.Treatments.Values;

    public IEnumerable<ClawTreatment> ClawTreatments => _clawTreatments.Treatments.Values;

    public IEnumerable<PlannedCowTreatment> PlannedCowTreatments => _plannedCowTreatments.Treatments.Values;

    public IEnumerable<PlannedClawTreatment> PlannedClawTreatments => _plannedClawTreatments.Treatments.Values;
}
