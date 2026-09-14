using BB_Cow.Class;

namespace BB_Cow.Kpi;

/// <summary>One shipped default KPI, before it becomes a database row.</summary>
public sealed record KpiSeed(string Title, int SortOrder, KpiDefinition Definition);

/// <summary>
/// The seven KPIs a fresh installation starts with, declaratively.
///
/// They live here rather than inline in DataSeeder for one reason: they are the acceptance test for
/// the whole builder. If all seven of the KPIs this application has always shipped can be expressed
/// without SQL, the builder is complete enough - and a test can assert that without touching a
/// database.
/// </summary>
public static class KpiSeeds
{
    public static IReadOnlyList<KpiSeed> Default { get; } = new KpiSeed[]
    {
        new("Geplante Kuh Behandlungen", 0, new KpiDefinition
        {
            Source = KpiSourceId.PlannedCowTreatment,
            Measure = KpiMeasure.Count
        }),
        new("Kuh Behandlungen", 1, new KpiDefinition
        {
            Source = KpiSourceId.CowTreatment,
            Measure = KpiMeasure.Count
        }),
        new("Klauen Behandlungen", 2, new KpiDefinition
        {
            Source = KpiSourceId.ClawTreatment,
            Measure = KpiMeasure.Count
        }),
        new("Geplante Klauen Behandlungen", 3, new KpiDefinition
        {
            Source = KpiSourceId.PlannedClawTreatment,
            Measure = KpiMeasure.Count
        }),
        new("Kuh mit den meisten Behandlungen", 4, new KpiDefinition
        {
            Source = KpiSourceId.CowTreatment,
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.Cow
        }),
        new("Meist behandeltes Viertel", 5, new KpiDefinition
        {
            Source = KpiSourceId.CowTreatment,
            Measure = KpiMeasure.TopValue,
            // The COMBINATION, matching the old "GROUP BY ct.COW_QUARTER_ID": a treatment of two
            // quarters is its own group ("LV/ RH"), not two separate ones. Exploding into single
            // quarters would change the value this tile has always shown.
            //
            // The old script also carried "WHERE c.UDDER_ID != 16" to skip the all-false udder row.
            // No filter replaces it: such a row projects to an empty group value and drops out of
            // the ranking by itself, so nothing depends on that id any more.
            GroupBy = KpiGroupBy.UdderQuarter
        }),
        new("Kuh mit den meisten Klauen Behandlungen", 6, new KpiDefinition
        {
            Source = KpiSourceId.ClawTreatment,
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.Cow
        })
    };
}
