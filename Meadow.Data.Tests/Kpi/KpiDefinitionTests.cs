using BB_Cow.Class;
using BB_Cow.Kpi;

namespace BBCowDataLibrary.Tests.Kpi;

public class KpiDefinitionTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.ClawTreatment,
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.ClawFinding,
            Timeframe = KpiTimeframe.Days30,
            CompareToPrevious = true,
            Unit = "ml",
            Decimals = 2,
            Filters = { [KpiTagKeys.ClawFinding] = new List<string> { "Mortellaro", "Verband" } }
        };

        var restored = KpiDefinition.Deserialize(KpiDefinition.Serialize(definition));

        Assert.NotNull(restored);
        Assert.Equal(definition.Source, restored!.Source);
        Assert.Equal(definition.Measure, restored.Measure);
        Assert.Equal(definition.GroupBy, restored.GroupBy);
        Assert.Equal(definition.Timeframe, restored.Timeframe);
        Assert.True(restored.CompareToPrevious);
        Assert.Equal("ml", restored.Unit);
        Assert.Equal(2, restored.Decimals);
        Assert.Equal(new[] { "Mortellaro", "Verband" }, restored.Filters[KpiTagKeys.ClawFinding]);
    }

    [Fact]
    public void Writes_enums_as_names_not_numbers()
    {
        // The reason this matters: definitions are stored for years. If enums were persisted by
        // ordinal, inserting a member into KpiMeasure would silently reinterpret every stored row.
        var json = KpiDefinition.Serialize(new KpiDefinition
        {
            Source = KpiSourceId.PlannedClawTreatment,
            Measure = KpiMeasure.CountDistinctCows
        });

        Assert.Contains("\"PlannedClawTreatment\"", json);
        Assert.Contains("\"CountDistinctCows\"", json);
    }

    [Fact]
    public void Ignores_properties_it_does_not_know()
    {
        // A row written by a newer build must not crash an older one.
        var restored = KpiDefinition.Deserialize(
            """{"source":"CowTreatment","measure":"Count","somethingFromTheFuture":{"a":1}}""");

        Assert.NotNull(restored);
        Assert.Equal(KpiSourceId.CowTreatment, restored!.Source);
        Assert.Equal(KpiMeasure.Count, restored.Measure);
    }

    [Fact]
    public void Fills_missing_properties_with_defaults()
    {
        // Also protects hand-written JSON, which only ever carries the fields it needs.
        var restored = KpiDefinition.Deserialize("""{"source":"ClawTreatment"}""");

        Assert.NotNull(restored);
        Assert.Equal(KpiSourceId.ClawTreatment, restored!.Source);
        Assert.Equal(KpiMeasure.Count, restored.Measure);
        Assert.Equal(KpiTimeframe.All, restored.Timeframe);
        Assert.Empty(restored.Filters);
        Assert.False(restored.CompareToPrevious);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{\"source\":")]
    public void Returns_null_for_unusable_json(string? json)
    {
        // Null rather than an exception, so the caller can turn it into a visible Error state.
        Assert.Null(KpiDefinition.Deserialize(json));
    }

    [Fact]
    public void AllowsComparison_needs_a_bounded_window_and_a_number()
    {
        Assert.False(new KpiDefinition { Timeframe = KpiTimeframe.All }.AllowsComparison);
        Assert.True(new KpiDefinition { Timeframe = KpiTimeframe.Days7 }.AllowsComparison);
        Assert.False(new KpiDefinition
        {
            Timeframe = KpiTimeframe.Days7,
            Measure = KpiMeasure.TopValue
        }.AllowsComparison);
    }

    [Fact]
    public void A_kpi_counts_as_builder_only_with_a_definition()
    {
        // Belt and braces against a silent fallback to Script, which would run a query the author
        // believed to be inactive.
        Assert.False(new KPI { Kind = (int)KpiKind.Builder, Definition = null }.IsBuilder);
        Assert.False(new KPI { Kind = (int)KpiKind.Builder, Definition = "  " }.IsBuilder);
        Assert.False(new KPI { Kind = (int)KpiKind.Sql, Definition = "{}" }.IsBuilder);
        Assert.True(new KPI { Kind = (int)KpiKind.Builder, Definition = "{}" }.IsBuilder);
    }

    [Fact]
    public void Sql_is_the_default_kind_so_untouched_rows_behave_as_before()
    {
        // This is what makes the migration's DEFAULT 0 safe for existing customer databases.
        Assert.Equal(0, (int)KpiKind.Sql);
        Assert.False(new KPI().IsBuilder);
    }
}
