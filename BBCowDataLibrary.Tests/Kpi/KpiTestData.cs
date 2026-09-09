using BB_Cow.Class;
using BB_Cow.Kpi;

namespace BBCowDataLibrary.Tests.Kpi;

/// <summary>
/// Builders for the evaluator tests. KpiRow is deliberately a plain record with no dependencies, so
/// no database, no EF and no DI is involved anywhere in these tests.
/// </summary>
internal static class KpiTestData
{
    /// <summary>Fixed "today" for every timeframe test, so boundaries are assertable.</summary>
    public static readonly DateTime Now = new(2026, 6, 15);

    public static KpiRow Row(
        string cowId = "c1",
        DateTime? date = null,
        double? dosage = null,
        string? label = null,
        string? dosageUnit = null,
        Dictionary<string, IReadOnlyList<string>>? tags = null) => new()
    {
        CowId = cowId,
        CowLabel = label ?? cowId,
        Date = date,
        Dosage = dosage,
        DosageUnit = dosageUnit,
        Tags = tags ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    };

    /// <summary>One tag group. Chain further groups with <see cref="And"/>.</summary>
    public static Dictionary<string, IReadOnlyList<string>> Tag(string key, params string[] values)
        => new(StringComparer.Ordinal) { [key] = values };

    public static Dictionary<string, IReadOnlyList<string>> And(
        this Dictionary<string, IReadOnlyList<string>> tags, string key, params string[] values)
    {
        tags[key] = values;
        return tags;
    }

    public static KpiSourceInfo Source(KpiSourceId id = KpiSourceId.CowTreatment)
        => KpiSourceRegistry.Find(id)!;

    public static KpiDefinition Definition(
        KpiSourceId source = KpiSourceId.CowTreatment,
        KpiMeasure measure = KpiMeasure.Count,
        KpiTimeframe timeframe = KpiTimeframe.All,
        KpiGroupBy groupBy = KpiGroupBy.None,
        bool compare = false,
        int decimals = 0,
        string? unit = null) => new()
    {
        Source = source,
        Measure = measure,
        Timeframe = timeframe,
        GroupBy = groupBy,
        CompareToPrevious = compare,
        Decimals = decimals,
        Unit = unit
    };

    public static KpiResult Evaluate(KpiDefinition definition, IReadOnlyList<KpiRow> rows)
        => KpiEvaluator.Evaluate(definition, Source(definition.Source), rows, Now);
}

/// <summary>
/// Stand-in for the ten singleton data services. The real ones take an IDbContextFactory and cannot
/// be constructed in a unit test - which is precisely why IKpiLookups is an interface.
/// </summary>
internal sealed class FakeKpiLookups : IKpiLookups
{
    public Dictionary<string, string> Collars { get; } = new(StringComparer.Ordinal);
    public Dictionary<int, string> Medicines { get; } = new();

    /// <summary>Dosiereinheit je Medikament; fehlt ein Eintrag, gilt "keine".</summary>
    public Dictionary<int, string> MedicineUnits { get; } = new();

    public Dictionary<int, string> WhereHows { get; } = new();
    public Dictionary<int, string> Udders { get; } = new();

    public List<Cow> CowList { get; } = new();
    public List<CowTreatment> CowTreatmentList { get; } = new();
    public List<ClawTreatment> ClawTreatmentList { get; } = new();
    public List<PlannedCowTreatment> PlannedCowTreatmentList { get; } = new();
    public List<PlannedClawTreatment> PlannedClawTreatmentList { get; } = new();

    public string CollarLabel(string cowId) => Collars.TryGetValue(cowId, out var c) ? c : string.Empty;

    public string MedicineName(int medicineId) => Medicines.TryGetValue(medicineId, out var m) ? m : string.Empty;

    public string MedicineDosageUnit(int medicineId)
        => MedicineUnits.TryGetValue(medicineId, out var u) ? u : string.Empty;

    public string WhereHowName(int whereHowId) => WhereHows.TryGetValue(whereHowId, out var w) ? w : string.Empty;

    public string UdderLabel(int udderId) => Udders.TryGetValue(udderId, out var u) ? u : string.Empty;

    public IEnumerable<Cow> Cows => CowList;
    public IEnumerable<CowTreatment> CowTreatments => CowTreatmentList;
    public IEnumerable<ClawTreatment> ClawTreatments => ClawTreatmentList;
    public IEnumerable<PlannedCowTreatment> PlannedCowTreatments => PlannedCowTreatmentList;
    public IEnumerable<PlannedClawTreatment> PlannedClawTreatments => PlannedClawTreatmentList;
}
