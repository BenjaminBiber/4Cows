using BB_Cow.Class;
using BB_Cow.Kpi;
using static BBCowDataLibrary.Tests.Kpi.KpiTestData;

namespace BBCowDataLibrary.Tests.Kpi;

/// <summary>
/// The acceptance test for the builder: every KPI this application has ever shipped must be
/// expressible without SQL. If one of these cannot be, the builder is not finished.
/// </summary>
public class KpiSeedsTests
{
    public static TheoryData<string> SeedTitles()
    {
        var data = new TheoryData<string>();
        foreach (var seed in KpiSeeds.Default)
        {
            data.Add(seed.Title);
        }

        return data;
    }

    private static KpiSeed Seed(string title) => KpiSeeds.Default.Single(s => s.Title == title);

    [Fact]
    public void Ships_seven_kpis_with_gapless_sort_order()
    {
        Assert.Equal(7, KpiSeeds.Default.Count);
        Assert.Equal(Enumerable.Range(0, 7), KpiSeeds.Default.Select(s => s.SortOrder));
        Assert.Equal(7, KpiSeeds.Default.Select(s => s.Title).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(SeedTitles))]
    public void Every_seed_evaluates_without_error_on_empty_data(string title)
    {
        // Empty, not populated: this asserts the DEFINITION is valid - source known, measure
        // offered, grouping supported. A capability mismatch would surface as Error here.
        var seed = Seed(title);
        var source = KpiSourceRegistry.Find(seed.Definition.Source);

        Assert.NotNull(source);

        var result = KpiEvaluator.Evaluate(seed.Definition, source!, Array.Empty<KpiRow>(), Now);

        Assert.NotEqual(KpiResultState.Error, result.State);
        Assert.Null(result.Message);
    }

    [Theory]
    [MemberData(nameof(SeedTitles))]
    public void Every_seed_survives_being_stored_and_read_back(string title)
    {
        var seed = Seed(title);

        var restored = KpiDefinition.Deserialize(KpiDefinition.Serialize(seed.Definition));

        Assert.NotNull(restored);
        Assert.Equal(seed.Definition.Source, restored!.Source);
        Assert.Equal(seed.Definition.Measure, restored.Measure);
        Assert.Equal(seed.Definition.GroupBy, restored.GroupBy);
    }

    [Theory]
    [MemberData(nameof(SeedTitles))]
    public void Every_seed_has_a_drill_down_route(string title)
    {
        // The old Url was free text, so a typo silently produced a 404. Deriving it from the
        // registry makes that impossible - but only if every source actually declares one.
        var source = KpiSourceRegistry.Find(Seed(title).Definition.Source)!;

        Assert.False(string.IsNullOrWhiteSpace(source.Route));
    }

    [Theory]
    [MemberData(nameof(SeedTitles))]
    public void Every_seed_reads_as_a_sentence_in_the_settings_list(string title)
    {
        var summary = KpiDefinitionSummary.Describe(Seed(title).Definition);

        Assert.False(string.IsNullOrWhiteSpace(summary));
        Assert.DoesNotContain("Definition unlesbar", summary);
    }

    [Fact]
    public void The_four_count_seeds_count_their_table()
    {
        var counts = KpiSeeds.Default.Where(s => s.Definition.Measure == KpiMeasure.Count).ToList();

        Assert.Equal(4, counts.Count);

        // No filters and no timeframe, exactly like the "SELECT COUNT(*) FROM <table>" they replace.
        Assert.All(counts, s =>
        {
            Assert.Empty(s.Definition.Filters);
            Assert.Equal(KpiTimeframe.All, s.Definition.Timeframe);
            Assert.False(s.Definition.CompareToPrevious);
        });

        Assert.Equal(
            new[]
            {
                KpiSourceId.PlannedCowTreatment, KpiSourceId.CowTreatment,
                KpiSourceId.ClawTreatment, KpiSourceId.PlannedClawTreatment
            },
            counts.Select(s => s.Definition.Source));
    }

    [Fact]
    public void The_three_ranking_seeds_replace_the_top_one_queries()
    {
        var rankings = KpiSeeds.Default.Where(s => s.Definition.Measure == KpiMeasure.TopValue).ToList();

        Assert.Equal(3, rankings.Count);
        Assert.All(rankings, s => Assert.NotEqual(KpiGroupBy.None, s.Definition.GroupBy));
    }

    [Fact]
    public void The_udder_seed_needs_no_sentinel_filter()
    {
        // The point of the conversion: "WHERE c.UDDER_ID != 16" is gone and nothing replaced it.
        var seed = Seed("Meist behandeltes Viertel");

        Assert.Equal(KpiGroupBy.UdderQuarter, seed.Definition.GroupBy);
        Assert.Empty(seed.Definition.Filters);
    }

    [Fact]
    public void The_udder_seed_ranks_quarter_combinations_the_way_the_old_query_did()
    {
        // "GROUP BY ct.COW_QUARTER_ID" grouped by the combination, so "LV/ RH" competed as one
        // group. Two single-LV treatments must therefore beat one LV/RH treatment.
        var rows = new[]
        {
            Row(tags: Tag(KpiTagKeys.UdderQuarter, "LV")),
            Row(tags: Tag(KpiTagKeys.UdderQuarter, "LV")),
            Row(tags: Tag(KpiTagKeys.UdderQuarter, "LV/ RH"))
        };

        var result = Evaluate(Seed("Meist behandeltes Viertel").Definition, rows);

        Assert.Equal("LV", result.Label);
        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void A_seed_stored_as_a_kpi_row_is_recognised_as_declarative()
    {
        var seed = Seed("Kuh Behandlungen");
        var kpi = new KPI
        {
            Title = seed.Title,
            Url = KpiSourceRegistry.Find(seed.Definition.Source)!.Route,
            Script = string.Empty,
            SortOrder = seed.SortOrder,
            Kind = (int)KpiKind.Builder,
            Definition = KpiDefinition.Serialize(seed.Definition)
        };

        Assert.True(kpi.IsBuilder);
        Assert.Equal("Kuh_Daten", kpi.Url);
        Assert.Contains("Kuh Behandlungen", KpiDefinitionSummary.Describe(kpi));
    }
}
