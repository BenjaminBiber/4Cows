using BB_Cow.Class;
using BB_Cow.Kpi;
using static BBCowDataLibrary.Tests.Kpi.KpiTestData;

namespace BBCowDataLibrary.Tests.Kpi;

public class KpiEvaluatorTests
{
    // ---- Measures ------------------------------------------------------

    [Fact]
    public void Count_counts_rows()
    {
        var result = Evaluate(Definition(), new[] { Row(), Row(), Row() });

        Assert.Equal(KpiResultState.Ok, result.State);
        Assert.Equal(3, result.Number);
        Assert.Equal(3, result.MatchedRows);
    }

    [Fact]
    public void CountDistinctCows_counts_each_cow_once()
    {
        // Three treatments, two of them on the same animal.
        var rows = new[] { Row("c1"), Row("c1"), Row("c2") };

        var result = Evaluate(Definition(measure: KpiMeasure.CountDistinctCows), rows);

        Assert.Equal(2, result.Number);
        Assert.Equal(3, result.MatchedRows);
    }

    [Fact]
    public void SumDosage_skips_rows_without_a_dosage()
    {
        var rows = new[] { Row(dosage: 10), Row(dosage: 5), Row(dosage: null) };

        var result = Evaluate(Definition(measure: KpiMeasure.SumDosage, decimals: 1), rows);

        Assert.Equal(15, result.Number);
    }

    [Fact]
    public void AvgDosage_ignores_missing_values_instead_of_treating_them_as_zero()
    {
        // The bug this guards: averaging over three rows would give 5, not 7.5.
        var rows = new[] { Row(dosage: 10), Row(dosage: 5), Row(dosage: null) };

        var result = Evaluate(Definition(measure: KpiMeasure.AvgDosage, decimals: 2), rows);

        Assert.Equal(7.5, result.Number);
    }

    [Fact]
    public void AvgDosage_over_nothing_is_undefined_not_zero()
    {
        var result = Evaluate(Definition(measure: KpiMeasure.AvgDosage), Array.Empty<KpiRow>());

        Assert.Equal(KpiResultState.Empty, result.State);
        Assert.Null(result.Number);
        Assert.Equal("–", result.Display);
    }

    [Fact]
    public void SumDosage_over_mixed_units_keeps_the_number_but_drops_the_unit()
    {
        // The reason this matters: since the dosage unit moved onto the medicine, a sum can add
        // millilitres to tablets. "3 + 20 = 23 ml" is not a crash, it is a plausible-looking
        // falsehood on a dashboard tile - so the unit must not be attached and the tile has to say
        // that units were mixed.
        var rows = new[]
        {
            Row(dosage: 20, dosageUnit: "ml"),
            Row(dosage: 3, dosageUnit: "Stück")
        };

        var result = Evaluate(Definition(measure: KpiMeasure.SumDosage, unit: "ml"), rows);

        Assert.Equal(23, result.Number);
        Assert.DoesNotContain("ml)", result.Display);
        Assert.Contains("gemischte Einheiten", result.Display);
        Assert.Contains("ml", result.Message);
        Assert.Contains("Stück", result.Message);
    }

    [Fact]
    public void SumDosage_over_one_unit_labels_the_value_with_it()
    {
        // The medicine's own unit wins over the definition's free-text field: it is closer to the
        // data than a label somebody typed before units existed.
        var rows = new[] { Row(dosage: 20, dosageUnit: "Stück"), Row(dosage: 3, dosageUnit: "Stück") };

        var result = Evaluate(Definition(measure: KpiMeasure.SumDosage, unit: "ml"), rows);

        Assert.Contains("Stück", result.Display);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Rows_without_a_recorded_unit_do_not_count_as_a_second_unit()
    {
        // Every medicine starts without a unit, so "some recorded, some not" is the normal state
        // for a long while. Treating the gap as a distinct unit would warn on almost every tile
        // and train people to ignore the warning.
        var rows = new[] { Row(dosage: 20, dosageUnit: "ml"), Row(dosage: 3, dosageUnit: null) };

        var result = Evaluate(Definition(measure: KpiMeasure.SumDosage), rows);

        Assert.Equal(23, result.Number);
        Assert.Null(result.Message);
    }

    [Fact]
    public void SumDosage_over_nothing_is_zero()
    {
        var result = Evaluate(Definition(measure: KpiMeasure.SumDosage), Array.Empty<KpiRow>());

        Assert.Equal(KpiResultState.Empty, result.State);
        Assert.Equal(0, result.Number);
    }

    // ---- Top-1 ---------------------------------------------------------

    [Fact]
    public void TopValue_returns_the_most_frequent_label()
    {
        var rows = new[]
        {
            Row(tags: Tag(KpiTagKeys.Medicine, "Penicillin")),
            Row(tags: Tag(KpiTagKeys.Medicine, "Penicillin")),
            Row(tags: Tag(KpiTagKeys.Medicine, "Cefa"))
        };

        var result = Evaluate(
            Definition(measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.Medicine), rows);

        Assert.Equal("Penicillin", result.Label);
        Assert.Equal("Penicillin", result.Display);
        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void TopValue_breaks_ties_deterministically()
    {
        // The old SQL used "ORDER BY COUNT(*) DESC LIMIT 1" with no tiebreaker, so on a tie the
        // winner was whatever the engine happened to return - the tile could change between two
        // renders on unchanged data. Ordinal on the label pins it.
        var rows = new[]
        {
            Row(tags: Tag(KpiTagKeys.Medicine, "Zink")),
            Row(tags: Tag(KpiTagKeys.Medicine, "Aspirin"))
        };

        var forwards = Evaluate(
            Definition(measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.Medicine), rows);
        var backwards = Evaluate(
            Definition(measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.Medicine), rows.Reverse().ToArray());

        Assert.Equal("Aspirin", forwards.Label);
        Assert.Equal(forwards.Label, backwards.Label);
    }

    [Fact]
    public void TopValue_skips_rows_with_no_group_value()
    {
        // This replaces the hardcoded "WHERE c.UDDER_ID != 16". The sentinel udder row projects to
        // an empty label, so it simply has no value under the key and drops out of the ranking.
        var rows = new[]
        {
            Row(tags: Tag(KpiTagKeys.UdderQuarter, "LV/ RH")),
            Row(),
            Row()
        };

        var result = Evaluate(
            Definition(measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.UdderQuarter), rows);

        Assert.Equal("LV/ RH", result.Label);
        Assert.Equal(1, result.Number);
    }

    [Fact]
    public void TopValue_without_any_group_value_is_empty()
    {
        var result = Evaluate(
            Definition(measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.UdderQuarter),
            new[] { Row(), Row() });

        Assert.Equal(KpiResultState.Empty, result.State);
        Assert.Equal("–", result.Display);
    }

    [Fact]
    public void TopValue_counts_a_multi_valued_row_once_per_value()
    {
        // A claw treatment carrying two findings belongs to both groups.
        var rows = new[]
        {
            Row(tags: Tag(KpiTagKeys.ClawFinding, "Mortellaro", "Klotz")),
            Row(tags: Tag(KpiTagKeys.ClawFinding, "Mortellaro"))
        };

        var definition = Definition(
            source: KpiSourceId.ClawTreatment,
            measure: KpiMeasure.TopValue,
            groupBy: KpiGroupBy.ClawFinding);

        var result = Evaluate(definition, rows);

        Assert.Equal("Mortellaro", result.Label);
        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void TopValue_by_cow_groups_by_the_animal_not_by_its_collar_number()
    {
        // A collar number is re-issued after a cow leaves the herd - CowService.IsCollarInUse only
        // reserves numbers of animals still present. Two treatments of the retired cow and one of
        // its successor must NOT add up to a three-treatment "cow 42".
        var rows = new[]
        {
            Row("retired-cow", label: "42"),
            Row("new-cow", label: "42"),
            Row("new-cow", label: "42"),
            Row("other-cow", label: "7")
        };

        var result = Evaluate(
            Definition(measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.Cow), rows);

        Assert.Equal("42", result.Label);
        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void TopValue_by_cow_ignores_animals_without_a_collar_number()
    {
        var rows = new[] { Row("c1", label: ""), Row("c2", label: "9") };

        var result = Evaluate(
            Definition(measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.Cow), rows);

        Assert.Equal("9", result.Label);
    }

    // ---- Timeframe -----------------------------------------------------

    [Fact]
    public void Days7_includes_both_boundaries_and_excludes_the_future()
    {
        // Mirrors DateRanges.Matches, which deliberately drops future rows because the dialogs can
        // post-date. If the evaluator disagreed, a tile and its drill-down would differ.
        var rows = new[]
        {
            Row(date: Now),                    // today - in
            Row(date: Now.AddDays(-7)),        // exactly the lower bound - in
            Row(date: Now.AddDays(-8)),        // one day too old - out
            Row(date: Now.AddDays(1))          // future - out
        };

        var result = Evaluate(Definition(timeframe: KpiTimeframe.Days7), rows);

        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void All_timeframe_keeps_future_rows()
    {
        var rows = new[] { Row(date: Now.AddDays(500)), Row(date: Now.AddYears(-3)) };

        var result = Evaluate(Definition(timeframe: KpiTimeframe.All), rows);

        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void Planned_sources_look_forward_not_backward()
    {
        // Planned treatments are deliberately future-dated. Looking backwards here would report
        // zero for every planned KPI with a timeframe.
        var rows = new[]
        {
            Row(date: Now.AddDays(3)),         // in the next 7 days - in
            Row(date: Now.AddDays(7)),         // upper bound - in
            Row(date: Now.AddDays(8)),         // beyond - out
            Row(date: Now.AddDays(-3))         // already past - out
        };

        var definition = Definition(
            source: KpiSourceId.PlannedCowTreatment, timeframe: KpiTimeframe.Days7);

        var result = Evaluate(definition, rows);

        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void Timeframe_on_a_source_without_a_date_is_an_error()
    {
        // Cow has no date column at all, so this is not a zero - it is an unanswerable question.
        var definition = Definition(source: KpiSourceId.Cow, timeframe: KpiTimeframe.Days30);

        var result = Evaluate(definition, new[] { Row(date: null) });

        Assert.Equal(KpiResultState.Error, result.State);
        Assert.NotNull(result.Message);
    }

    // ---- Previous period ----------------------------------------------

    [Fact]
    public void Previous_period_is_adjacent_and_does_not_overlap()
    {
        // Days7 spans 8 calendar days because both ends are inclusive, so the previous window must
        // shift by 8, not by 7. Shifting by 7 would count the row on the shared boundary twice.
        var rows = new[]
        {
            Row(date: Now),                    // current
            Row(date: Now.AddDays(-7)),        // current, lower bound
            Row(date: Now.AddDays(-8)),        // previous, upper bound
            Row(date: Now.AddDays(-15)),       // previous, lower bound
            Row(date: Now.AddDays(-16))        // older than both
        };

        var result = Evaluate(Definition(timeframe: KpiTimeframe.Days7, compare: true), rows);

        Assert.Equal(2, result.Number);
        Assert.Equal(2, result.Previous);
        Assert.Equal(0, result.DeltaPercent);
        Assert.Equal(KpiTrend.Flat, result.Trend);
    }

    [Fact]
    public void Comparison_reports_a_percentage_and_a_direction()
    {
        var rows = new[]
        {
            Row(date: Now), Row(date: Now), Row(date: Now),
            Row(date: Now.AddDays(-10))
        };

        var result = Evaluate(Definition(timeframe: KpiTimeframe.Days7, compare: true), rows);

        Assert.Equal(3, result.Number);
        Assert.Equal(1, result.Previous);
        Assert.Equal(200, result.DeltaPercent);
        Assert.Equal(KpiTrend.Up, result.Trend);
        Assert.Equal("+200 %", result.DeltaDisplay);
    }

    [Fact]
    public void Comparison_against_zero_yields_no_percentage()
    {
        // Growth from 0 is not "infinite", it is simply not a ratio.
        var rows = new[] { Row(date: Now) };

        var result = Evaluate(Definition(timeframe: KpiTimeframe.Days7, compare: true), rows);

        Assert.Equal(1, result.Number);
        Assert.Equal(0, result.Previous);
        Assert.Null(result.DeltaPercent);
        Assert.Null(result.DeltaDisplay);
        Assert.Equal(KpiTrend.None, result.Trend);
    }

    [Fact]
    public void Comparison_is_dropped_rather_than_failing_when_it_cannot_apply()
    {
        // A definition can legitimately keep the flag while its author switches to "all time".
        // Losing the trend line is right; a broken tile is not.
        var result = Evaluate(
            Definition(timeframe: KpiTimeframe.All, compare: true), new[] { Row(date: Now) });

        Assert.Equal(KpiResultState.Ok, result.State);
        Assert.Null(result.DeltaDisplay);
    }

    [Fact]
    public void Planned_sources_offer_no_comparison()
    {
        Assert.False(Source(KpiSourceId.PlannedCowTreatment).SupportsCompare);
        Assert.False(Source(KpiSourceId.PlannedClawTreatment).SupportsCompare);
        Assert.True(Source(KpiSourceId.CowTreatment).SupportsCompare);
        Assert.False(Source(KpiSourceId.Cow).SupportsCompare);
    }

    // ---- Filters -------------------------------------------------------

    [Fact]
    public void An_empty_filter_group_means_no_restriction()
    {
        var definition = Definition();
        definition.Filters[KpiTagKeys.Medicine] = new List<string>();

        var result = Evaluate(definition, new[]
        {
            Row(tags: Tag(KpiTagKeys.Medicine, "Penicillin")),
            Row()
        });

        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void Values_inside_one_group_are_or_ed()
    {
        var definition = Definition();
        definition.Filters[KpiTagKeys.Medicine] = new List<string> { "Penicillin", "Cefa" };

        var result = Evaluate(definition, new[]
        {
            Row(tags: Tag(KpiTagKeys.Medicine, "Penicillin")),
            Row(tags: Tag(KpiTagKeys.Medicine, "Cefa")),
            Row(tags: Tag(KpiTagKeys.Medicine, "Zink"))
        });

        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void Separate_groups_are_and_ed()
    {
        var definition = Definition();
        definition.Filters[KpiTagKeys.Medicine] = new List<string> { "Penicillin" };
        definition.Filters[KpiTagKeys.UdderQuarter] = new List<string> { "LV" };

        var result = Evaluate(definition, new[]
        {
            Row(tags: Tag(KpiTagKeys.Medicine, "Penicillin").And(KpiTagKeys.UdderQuarter, "LV")),
            Row(tags: Tag(KpiTagKeys.Medicine, "Penicillin")),
            Row(tags: Tag(KpiTagKeys.UdderQuarter, "LV"))
        });

        Assert.Equal(1, result.Number);
    }

    [Fact]
    public void Filter_values_are_matched_case_insensitively()
    {
        // Matches CowTableFilter.Medicines, which is an OrdinalIgnoreCase HashSet.
        var definition = Definition();
        definition.Filters[KpiTagKeys.Medicine] = new List<string> { "penicillin" };

        var result = Evaluate(definition, new[] { Row(tags: Tag(KpiTagKeys.Medicine, "Penicillin")) });

        Assert.Equal(1, result.Number);
    }

    // ---- Diagnostics ---------------------------------------------------

    [Fact]
    public void A_filter_value_that_exists_nowhere_is_reported_instead_of_reading_zero()
    {
        // What a renamed medicine looks like from here. Definitions must store names, because the
        // nightly demo reset re-assigns Medicine ids while KPI rows survive - so this is the
        // failure mode a customer would otherwise hit before we did.
        var definition = Definition();
        definition.Filters[KpiTagKeys.Medicine] = new List<string> { "Penicillin alt" };

        var result = Evaluate(definition, new[] { Row(tags: Tag(KpiTagKeys.Medicine, "Penicillin")) });

        Assert.Equal(0, result.Number);
        Assert.NotNull(result.Message);
        Assert.Contains("Penicillin alt", result.Message);
    }

    [Fact]
    public void A_period_with_no_rows_is_empty_but_carries_no_warning()
    {
        // The distinction that "--" used to destroy: nothing here is broken, the window is just quiet.
        var definition = Definition(timeframe: KpiTimeframe.Days7);
        definition.Filters[KpiTagKeys.Medicine] = new List<string> { "Penicillin" };

        var result = Evaluate(definition, new[]
        {
            Row(date: Now.AddDays(-100), tags: Tag(KpiTagKeys.Medicine, "Penicillin"))
        });

        Assert.Equal(KpiResultState.Empty, result.State);
        Assert.Equal(0, result.Number);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Empty_and_Error_stay_distinguishable()
    {
        var empty = Evaluate(Definition(), Array.Empty<KpiRow>());
        var broken = Evaluate(
            Definition(source: KpiSourceId.Cow, measure: KpiMeasure.SumDosage), Array.Empty<KpiRow>());

        Assert.Equal(KpiResultState.Empty, empty.State);
        Assert.Equal(KpiResultState.Error, broken.State);
        Assert.NotEqual(empty.Display, broken.Display);
    }

    // ---- Capability validation ----------------------------------------

    [Fact]
    public void A_measure_the_source_does_not_offer_is_an_error()
    {
        var result = Evaluate(
            Definition(source: KpiSourceId.ClawTreatment, measure: KpiMeasure.SumDosage),
            new[] { Row(dosage: 5) });

        Assert.Equal(KpiResultState.Error, result.State);
    }

    [Fact]
    public void TopValue_without_a_grouping_is_an_error()
    {
        var result = Evaluate(
            Definition(measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.None), new[] { Row() });

        Assert.Equal(KpiResultState.Error, result.State);
    }

    [Fact]
    public void A_grouping_the_source_does_not_offer_is_an_error()
    {
        var result = Evaluate(
            Definition(
                source: KpiSourceId.ClawTreatment,
                measure: KpiMeasure.TopValue,
                groupBy: KpiGroupBy.Medicine),
            new[] { Row(tags: Tag(KpiTagKeys.Medicine, "Penicillin")) });

        Assert.Equal(KpiResultState.Error, result.State);
    }

    [Fact]
    public void Every_declared_grouping_is_evaluable_on_its_source()
    {
        // Guards the registry against itself: a source may not advertise a dimension that has no
        // tag key, because the dialog would then offer a combination that always errors.
        foreach (var source in KpiSourceRegistry.All)
        {
            foreach (var grouping in source.Groupings)
            {
                Assert.NotNull(KpiTagKeys.ForGroupBy(grouping));
                Assert.Contains(KpiMeasure.TopValue, source.Measures);
            }
        }
    }

    [Fact]
    public void Only_sources_with_a_dosage_field_offer_sum_or_average()
    {
        foreach (var source in KpiSourceRegistry.All)
        {
            var offersDosage = source.Measures.Contains(KpiMeasure.SumDosage)
                || source.Measures.Contains(KpiMeasure.AvgDosage);

            Assert.Equal(source.HasDosage, offersDosage);
        }
    }
}
