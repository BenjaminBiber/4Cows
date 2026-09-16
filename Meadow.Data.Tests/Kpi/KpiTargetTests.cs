using Meadow.Shared.Kpi;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Kpi;

/// <summary>
/// The traffic light: when it lights up, when it stays dark, and where its boundaries lie.
///
/// Most of these pin down a refusal rather than a colour. A light that shows up where it should not
/// is worse than no light, because it reads as a statement someone checked - so every case in which
/// the evaluator declines to rate is a test of its own.
/// </summary>
public class KpiTargetTests
{
    private static KpiDefinition WithTarget(
        KpiTargetDirection direction,
        double? good,
        double? warning = null,
        KpiMeasure measure = KpiMeasure.Count,
        KpiGroupBy groupBy = KpiGroupBy.None)
    {
        var definition = KpiTestData.Definition(measure: measure, groupBy: groupBy);
        definition.TargetDirection = direction;
        definition.TargetGood = good;
        definition.TargetWarning = warning;
        return definition;
    }

    private static KpiResult Rate(KpiDefinition definition, int rowCount)
    {
        var rows = Enumerable.Range(0, rowCount)
            .Select(i => KpiTestData.Row(cowId: $"c{i}"))
            .ToList();

        return KpiEvaluator.Evaluate(definition, KpiTestData.Source(), rows, KpiTestData.Now);
    }

    [Fact]
    public void A_definition_without_a_target_is_not_rated()
    {
        // Every definition stored before targets existed says exactly this, so it is also the
        // backwards-compatibility test.
        var result = Rate(KpiTestData.Definition(), 5);

        Assert.Equal(KpiStatus.None, result.Status);
    }

    [Theory]
    [InlineData(0, KpiStatus.Good)]
    [InlineData(3, KpiStatus.Good)]   // boundary: inclusive
    [InlineData(4, KpiStatus.Warning)]
    [InlineData(6, KpiStatus.Warning)] // boundary: inclusive
    [InlineData(7, KpiStatus.Bad)]
    public void Lower_is_better_rates_the_three_bands_at_their_boundaries(int rows, KpiStatus expected)
    {
        // "Offene Verbände: bis 3 gut, bis 6 Warnung, darüber rot."
        var definition = WithTarget(KpiTargetDirection.LowerIsBetter, good: 3, warning: 6);

        Assert.Equal(expected, Rate(definition, rows).Status);
    }

    [Theory]
    [InlineData(12, KpiStatus.Good)]
    [InlineData(10, KpiStatus.Good)]  // boundary: inclusive
    [InlineData(9, KpiStatus.Warning)]
    [InlineData(6, KpiStatus.Warning)] // boundary: inclusive
    [InlineData(5, KpiStatus.Bad)]
    public void Higher_is_better_rates_the_three_bands_at_their_boundaries(int rows, KpiStatus expected)
    {
        var definition = WithTarget(KpiTargetDirection.HigherIsBetter, good: 10, warning: 6);

        Assert.Equal(expected, Rate(definition, rows).Status);
    }

    [Theory]
    [InlineData(2, KpiStatus.Good)]
    [InlineData(9, KpiStatus.Bad)]
    public void A_target_without_a_warning_threshold_has_only_two_bands(int rows, KpiStatus expected)
    {
        var definition = WithTarget(KpiTargetDirection.LowerIsBetter, good: 3);

        Assert.Equal(expected, Rate(definition, rows).Status);
    }

    [Fact]
    public void A_warning_threshold_without_a_good_one_rates_nothing()
    {
        // "Warnung, aber kein Gut" describes no band anybody could mean.
        var definition = WithTarget(KpiTargetDirection.LowerIsBetter, good: null, warning: 6);

        Assert.False(definition.HasTarget);
        Assert.Equal(KpiStatus.None, Rate(definition, 99).Status);
    }

    [Fact]
    public void Zero_in_an_empty_period_is_still_rated()
    {
        // THE case this feature exists for. FromCount reports State = Empty for a zero, so hanging
        // the rating on State instead of on Number would leave "0 offene Verbände" - the single
        // most reassuring tile on the dashboard - permanently grey.
        var definition = WithTarget(KpiTargetDirection.LowerIsBetter, good: 3, warning: 6);
        var result = Rate(definition, 0);

        Assert.Equal(KpiResultState.Empty, result.State);
        Assert.Equal(KpiStatus.Good, result.Status);
    }

    [Fact]
    public void TopValue_is_never_rated_even_with_thresholds_stored()
    {
        // Its Number carries the winner's hit count, not the label on the tile. Colouring by that
        // would colour the tile by something nobody can see. Hand-written JSON, because the dialog
        // never offers this combination - which is the point.
        var definition = KpiDefinition.Deserialize(
            """
            {"source":"CowTreatment","measure":"TopValue","groupBy":"Medicine",
             "targetDirection":"LowerIsBetter","targetGood":1}
            """)!;

        Assert.False(definition.AllowsTarget);

        var rows = new[]
        {
            KpiTestData.Row(tags: KpiTestData.Tag(KpiTagKeys.Medicine, "Penicillin")),
            KpiTestData.Row(tags: KpiTestData.Tag(KpiTagKeys.Medicine, "Penicillin"))
        };

        var result = KpiEvaluator.Evaluate(definition, KpiTestData.Source(), rows, KpiTestData.Now);

        Assert.Equal("Penicillin", result.Label);
        Assert.Equal(KpiStatus.None, result.Status);
    }

    [Fact]
    public void An_average_over_nothing_is_not_rated()
    {
        // FromDosage leaves Number null there, on purpose: undefined is not zero.
        var definition = WithTarget(KpiTargetDirection.HigherIsBetter, good: 1, measure: KpiMeasure.AvgDosage);

        var result = KpiEvaluator.Evaluate(
            definition, KpiTestData.Source(), Array.Empty<KpiRow>(), KpiTestData.Now);

        Assert.Null(result.Number);
        Assert.Equal(KpiStatus.None, result.Status);
    }

    [Fact]
    public void An_error_is_never_rated()
    {
        // A timeframe on the cow source, which has no date column at all.
        var definition = KpiTestData.Definition(
            source: KpiSourceId.Cow, timeframe: KpiTimeframe.Days30);
        definition.TargetDirection = KpiTargetDirection.LowerIsBetter;
        definition.TargetGood = 100;

        var result = KpiEvaluator.Evaluate(
            definition, KpiTestData.Source(KpiSourceId.Cow), Array.Empty<KpiRow>(), KpiTestData.Now);

        Assert.Equal(KpiResultState.Error, result.State);
        Assert.Equal(KpiStatus.None, result.Status);
    }

    [Fact]
    public void Thresholds_in_the_wrong_order_withhold_the_light_and_say_so()
    {
        // Not an error: the number is right, the definition is not. Validate stays reserved for
        // things that are structurally impossible.
        var definition = WithTarget(KpiTargetDirection.LowerIsBetter, good: 6, warning: 3);

        Assert.False(definition.TargetIsConsistent);

        var result = Rate(definition, 2);

        Assert.Equal(KpiStatus.None, result.Status);
        Assert.NotEqual(KpiResultState.Error, result.State);
        Assert.Contains("Warnschwelle", result.Message);
    }

    [Fact]
    public void A_stale_filter_value_silences_the_light()
    {
        // "This number is doubtful" and "this number is fine" must not be said at the same time.
        // A renamed medicine makes the tile read 0 - which a "lower is better" target would
        // otherwise celebrate as the best possible result.
        var definition = WithTarget(KpiTargetDirection.LowerIsBetter, good: 3);
        definition.Filters[KpiTagKeys.Medicine] = new List<string> { "Gibt es nicht mehr" };

        var rows = new[] { KpiTestData.Row(tags: KpiTestData.Tag(KpiTagKeys.Medicine, "Penicillin")) };
        var result = KpiEvaluator.Evaluate(definition, KpiTestData.Source(), rows, KpiTestData.Now);

        Assert.Equal(0d, result.Number);
        Assert.NotNull(result.Message);
        Assert.Equal(KpiStatus.None, result.Status);
    }

    [Fact]
    public void Mixed_dosage_units_silence_the_light()
    {
        var definition = WithTarget(
            KpiTargetDirection.LowerIsBetter, good: 1000, measure: KpiMeasure.SumDosage);

        var rows = new[]
        {
            KpiTestData.Row(dosage: 10, dosageUnit: "ml"),
            KpiTestData.Row(cowId: "c2", dosage: 3, dosageUnit: "Stück")
        };

        var result = KpiEvaluator.Evaluate(definition, KpiTestData.Source(), rows, KpiTestData.Now);

        Assert.NotNull(result.Message);
        Assert.Equal(KpiStatus.None, result.Status);
    }

    [Fact]
    public void Target_fields_round_trip_through_json()
    {
        var definition = WithTarget(KpiTargetDirection.HigherIsBetter, good: 90, warning: 75);

        var restored = KpiDefinition.Deserialize(KpiDefinition.Serialize(definition))!;

        Assert.Equal(KpiTargetDirection.HigherIsBetter, restored.TargetDirection);
        Assert.Equal(90d, restored.TargetGood);
        Assert.Equal(75d, restored.TargetWarning);
    }

    [Fact]
    public void The_direction_is_written_as_a_name_not_a_number()
    {
        // Same rule as every other enum here: reordering the members later must not silently
        // reinterpret stored definitions.
        var json = KpiDefinition.Serialize(WithTarget(KpiTargetDirection.LowerIsBetter, good: 3));

        Assert.Contains("\"LowerIsBetter\"", json);
    }

    [Fact]
    public void A_definition_stored_before_targets_existed_is_unrated()
    {
        // Verbatim from ConvertCountKpisToBuilder, i.e. a row that is actually out there.
        var definition = KpiDefinition.Deserialize(
            """{"source":"CowTreatment","measure":"Count","groupBy":"None","timeframe":"All"}""")!;

        Assert.Equal(KpiTargetDirection.None, definition.TargetDirection);
        Assert.False(definition.HasTarget);
        Assert.Equal(KpiStatus.None, Rate(definition, 5).Status);
    }

    [Fact]
    public void The_summary_spells_the_target_out_in_the_direction_it_is_read()
    {
        Assert.Equal(
            "Ziel: ≤ 3 gut, ≤ 6 Warnung",
            KpiDefinitionSummary.Target(WithTarget(KpiTargetDirection.LowerIsBetter, 3, 6)));

        Assert.Equal(
            "Ziel: ≥ 90 gut",
            KpiDefinitionSummary.Target(WithTarget(KpiTargetDirection.HigherIsBetter, 90)));

        Assert.Equal(string.Empty, KpiDefinitionSummary.Target(KpiTestData.Definition()));
    }

    [Fact]
    public void The_sql_builder_reports_the_target_as_missing_rather_than_dropping_it()
    {
        // The expert mode loses the light. Whoever switches should read that here, not find out on
        // the dashboard.
        var definition = WithTarget(KpiTargetDirection.LowerIsBetter, good: 3);
        var script = KpiSqlBuilder.Build(definition, KpiTestData.Source());

        Assert.Contains(script.Notes, n => n.Contains("Ampel"));

        var without = KpiSqlBuilder.Build(KpiTestData.Definition(), KpiTestData.Source());
        Assert.DoesNotContain(without.Notes, n => n.Contains("Ampel"));
    }
}
