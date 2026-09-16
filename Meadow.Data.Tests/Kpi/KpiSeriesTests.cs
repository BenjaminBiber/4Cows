using Meadow.Shared.Kpi;
using Meadow.Shared.Models;
using Meadow.Shared.Profile;

namespace Meadow.Data.Tests.Kpi;

/// <summary>
/// The series behind the sparkline.
///
/// The load-bearing test is Points_add_up_to_the_tile_value. A little curve that disagrees with the
/// big number above it is worse than no curve at all, because it looks like data. Everything else
/// here is about boundaries and about the three cases where there is deliberately no series.
/// </summary>
public class KpiSeriesTests
{
    private static KpiSeries Series(
        KpiDefinition definition,
        IReadOnlyList<KpiRow> rows,
        KpiSourceId source = KpiSourceId.CowTreatment)
        => KpiEvaluator.Series(
            definition, KpiTestData.Source(source), rows, KpiTestData.Now);

    private static KpiRow Row(int daysAgo, double? dosage = null, string cowId = "c1")
        => KpiTestData.Row(cowId: cowId, date: KpiTestData.Now.AddDays(-daysAgo), dosage: dosage);

    [Fact]
    public void Days7_yields_eight_daily_points_because_the_window_includes_both_ends()
    {
        // The single most likely off-by-one in the whole feature. KpiEvaluator.Window is inclusive
        // at both ends - that is why shifting to the previous period uses days + 1 - so "7 Tage"
        // spans eight calendar days. Seven points would not add up to the tile.
        var series = Series(KpiTestData.Definition(timeframe: KpiTimeframe.Days7), Array.Empty<KpiRow>());

        Assert.Equal(KpiBucket.Day, series.Bucket);
        Assert.Equal(8, series.Points.Count);
    }

    [Fact]
    public void Days30_yields_thirtyone_daily_points()
    {
        var series = Series(KpiTestData.Definition(timeframe: KpiTimeframe.Days30), Array.Empty<KpiRow>());

        Assert.Equal(31, series.Points.Count);
    }

    [Fact]
    public void Days90_yields_thirteen_weekly_points_that_tile_the_window_exactly()
    {
        var series = Series(KpiTestData.Definition(timeframe: KpiTimeframe.Days90), Array.Empty<KpiRow>());

        Assert.Equal(KpiBucket.Week, series.Bucket);
        Assert.Equal(13, series.Points.Count);

        // 91 days = 13 x 7, no remainder, no gaps: each section starts the day after the previous
        // one ends.
        for (var i = 1; i < series.Points.Count; i++)
        {
            Assert.Equal(series.Points[i - 1].Start.AddDays(7), series.Points[i].Start);
        }

        Assert.Equal(KpiTestData.Now.Date.AddDays(-90), series.Points[0].Start);
    }

    [Theory]
    [InlineData(KpiTimeframe.Days7)]
    [InlineData(KpiTimeframe.Days30)]
    [InlineData(KpiTimeframe.Days90)]
    public void Points_add_up_to_the_tile_value(KpiTimeframe timeframe)
    {
        // THE contract. Rows spread across the window, plus two outside it that neither the tile
        // nor the series may count.
        var rows = new[]
        {
            Row(0), Row(1), Row(1), Row(6), Row(7),
            Row(29), Row(30), Row(89), Row(90),
            Row(91), Row(400)
        };

        var definition = KpiTestData.Definition(timeframe: timeframe);

        var tile = KpiEvaluator.Evaluate(definition, KpiTestData.Source(), rows, KpiTestData.Now);
        var series = Series(definition, rows);

        Assert.Equal(tile.Number, series.Points.Sum(p => p.Value ?? 0));
        Assert.Equal(tile.MatchedRows, series.Points.Sum(p => p.MatchedRows));
    }

    [Fact]
    public void A_sum_also_adds_up_to_the_tile_value()
    {
        var rows = new[] { Row(0, dosage: 2.5), Row(3, dosage: 10), Row(29, dosage: 1) };
        var definition = KpiTestData.Definition(measure: KpiMeasure.SumDosage, timeframe: KpiTimeframe.Days30);

        var tile = KpiEvaluator.Evaluate(definition, KpiTestData.Source(), rows, KpiTestData.Now);
        var series = Series(definition, rows);

        Assert.Equal(tile.Number, series.Points.Sum(p => p.Value ?? 0));
    }

    [Fact]
    public void An_empty_section_counts_zero_but_averages_nothing()
    {
        // A count over nothing is a real zero; an average over nothing is undefined. On the chart
        // that is the difference between a dip to the floor and a gap in the line.
        var counts = Series(
            KpiTestData.Definition(timeframe: KpiTimeframe.Days7), Array.Empty<KpiRow>());
        Assert.All(counts.Points, p => Assert.Equal(0d, p.Value));

        var averages = Series(
            KpiTestData.Definition(measure: KpiMeasure.AvgDosage, timeframe: KpiTimeframe.Days7),
            Array.Empty<KpiRow>());
        Assert.All(averages.Points, p => Assert.Null(p.Value));
    }

    [Fact]
    public void Distinct_cows_are_counted_per_section_not_cumulatively()
    {
        // Documented, not a bug: the same cow treated on two days counts in both sections, so the
        // points deliberately do NOT add up to the tile. A running distinct count would be a
        // different measure from the one shown above the chart.
        var rows = new[] { Row(0, cowId: "c1"), Row(3, cowId: "c1") };
        var definition = KpiTestData.Definition(
            measure: KpiMeasure.CountDistinctCows, timeframe: KpiTimeframe.Days7);

        var tile = KpiEvaluator.Evaluate(definition, KpiTestData.Source(), rows, KpiTestData.Now);
        var series = Series(definition, rows);

        Assert.Equal(1d, tile.Number);
        Assert.Equal(2d, series.Points.Sum(p => p.Value ?? 0));
    }

    [Fact]
    public void Filters_apply_to_the_series_exactly_as_to_the_value()
    {
        var rows = new[]
        {
            KpiTestData.Row(date: KpiTestData.Now, tags: KpiTestData.Tag(KpiTagKeys.Medicine, "Penicillin")),
            KpiTestData.Row(cowId: "c2", date: KpiTestData.Now,
                tags: KpiTestData.Tag(KpiTagKeys.Medicine, "Anderes"))
        };

        var definition = KpiTestData.Definition(timeframe: KpiTimeframe.Days7);
        definition.Filters[KpiTagKeys.Medicine] = new List<string> { "Penicillin" };

        var series = Series(definition, rows);

        Assert.Equal(1, series.Points.Sum(p => p.MatchedRows));
    }

    [Fact]
    public void A_planned_source_runs_forward_from_today()
    {
        // Same direction rule as the window: looking back would show an empty chart for every
        // planned KPI, since those rows are deliberately future-dated.
        var series = Series(
            KpiTestData.Definition(source: KpiSourceId.PlannedCowTreatment, timeframe: KpiTimeframe.Days7),
            Array.Empty<KpiRow>(),
            KpiSourceId.PlannedCowTreatment);

        Assert.Equal(KpiTestData.Now.Date, series.Points[0].Start);
        Assert.Equal(KpiTestData.Now.Date.AddDays(7), series.Points[^1].Start);
    }

    [Fact]
    public void All_yields_twelve_monthly_points_oldest_first()
    {
        var series = Series(KpiTestData.Definition(), Array.Empty<KpiRow>());

        Assert.Equal(KpiBucket.Month, series.Bucket);
        Assert.Equal(12, series.Points.Count);
        Assert.Equal(new DateTime(KpiTestData.Now.Year, KpiTestData.Now.Month, 1), series.Points[^1].Start);
        Assert.True(series.Points[0].Start < series.Points[^1].Start);
    }

    [Fact]
    public void The_month_window_matches_the_cow_profile()
    {
        // Two constants for one idea drift. Cheaper to assert than to reference across a namespace
        // that already points the other way.
        Assert.Equal(CowProfileBuilder.WindowMonths, KpiEvaluator.MonthWindow);
    }

    [Fact]
    public void An_all_time_series_does_not_claim_to_cover_the_whole_timeframe()
    {
        // The tile counts a three-year-old treatment; the twelve-month series does not. Saying so
        // is what stops a reader concluding the chart is broken.
        var rows = new[] { Row(0), Row(daysAgo: 1000) };
        var series = Series(KpiTestData.Definition(), rows);

        Assert.False(series.CoversTimeframe);
        Assert.Equal(1, series.Points.Sum(p => p.MatchedRows));
    }

    [Fact]
    public void A_bounded_series_claims_to_cover_its_timeframe()
    {
        Assert.True(Series(
            KpiTestData.Definition(timeframe: KpiTimeframe.Days30), Array.Empty<KpiRow>()).CoversTimeframe);
    }

    [Fact]
    public void The_average_skips_sections_without_a_value_instead_of_counting_them_as_zero()
    {
        // The second figure on the tile. An average over an undefined section is undefined, not
        // nought - counting the empty ones would halve every dosage average on a quiet week.
        var rows = new[] { Row(0, dosage: 10), Row(1, dosage: 20) };
        var definition = KpiTestData.Definition(
            measure: KpiMeasure.AvgDosage, timeframe: KpiTimeframe.Days7, decimals: 1, unit: "ml");

        var series = Series(definition, rows);

        // Two sections have a value (10 and 20), six have none.
        Assert.Equal("15,0 ml", series.AverageDisplay);
    }

    [Fact]
    public void A_counted_average_keeps_one_decimal_even_though_the_tile_shows_none()
    {
        // 9 rows over 8 daily sections is 1,125 - rounding that to 1 would throw away the only
        // thing that distinguishes the average from the tile's own figure.
        var rows = Enumerable.Range(0, 8).Select(i => Row(i)).Append(Row(0)).ToArray();
        var series = Series(KpiTestData.Definition(timeframe: KpiTimeframe.Days7), rows);

        Assert.Equal("1,1", series.AverageDisplay);
    }

    [Fact]
    public void The_average_carries_the_unit_of_the_value_above_it()
    {
        var rows = new[] { Row(0, dosage: 4), Row(2, dosage: 6) };
        var definition = KpiTestData.Definition(
            measure: KpiMeasure.SumDosage, timeframe: KpiTimeframe.Days7, decimals: 2, unit: "ml");

        Assert.EndsWith("ml", Series(definition, rows).AverageDisplay);
    }

    [Fact]
    public void The_average_alone_is_reason_enough_to_compute_the_series()
    {
        // The second figure is its own switch, not a property of the chart: "120, im Schnitt 8,2
        // pro Monat" needs no picture. So asking for the average must make KpiDashboard compute
        // the series even with no curve to draw.
        var definition = KpiTestData.Definition(timeframe: KpiTimeframe.Days7);

        Assert.False(definition.WantsSeries);

        definition.ShowAverage = true;
        Assert.True(definition.WantsSeries);

        definition.ShowAverage = false;
        definition.SeriesDisplay = KpiSeriesDisplay.Inline;
        Assert.True(definition.WantsSeries);
    }

    [Fact]
    public void A_label_measure_wants_no_series_however_it_is_asked()
    {
        // Neither switch can talk a Top-1 ranking into a history.
        var definition = KpiTestData.Definition(
            measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.Medicine);
        definition.ShowAverage = true;
        definition.SeriesDisplay = KpiSeriesDisplay.Chart;

        Assert.False(definition.WantsSeries);
    }

    [Fact]
    public void An_empty_series_of_averages_has_no_average_at_all()
    {
        var series = Series(
            KpiTestData.Definition(measure: KpiMeasure.AvgDosage, timeframe: KpiTimeframe.Days7),
            Array.Empty<KpiRow>());

        Assert.Null(series.AverageDisplay);
    }

    [Fact]
    public void Every_bucket_has_a_label_for_the_average()
    {
        // The label names the SECTION, not the timeframe: "30 Tage" draws daily sections while
        // "90 Tage" draws weekly ones, and "Ø pro Tag" over a weekly chart would be a different
        // number from the one shown.
        foreach (var timeframe in KpiTimeframes.All)
        {
            var series = Series(KpiTestData.Definition(timeframe: timeframe), Array.Empty<KpiRow>());
            var label = KpiDefinitionSummary.AverageLabel(series.Bucket);

            Assert.StartsWith("Ø pro ", label);
        }
    }

    [Fact]
    public void TopValue_has_no_series_and_says_why()
    {
        var series = Series(
            KpiTestData.Definition(measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.Medicine),
            Array.Empty<KpiRow>());

        Assert.Equal(KpiBucket.None, series.Bucket);
        Assert.Empty(series.Points);
        Assert.Contains("Verlauf", series.Message);
    }

    [Fact]
    public void The_cow_source_has_no_series_and_says_why()
    {
        var series = Series(
            KpiTestData.Definition(source: KpiSourceId.Cow), Array.Empty<KpiRow>(), KpiSourceId.Cow);

        Assert.Equal(KpiBucket.None, series.Bucket);
        Assert.Contains("Datumsspalte", series.Message);
    }

    [Fact]
    public void A_broken_definition_has_no_series()
    {
        // Validate guards the series too - a timeframe on a source without a date column.
        var series = Series(
            KpiTestData.Definition(source: KpiSourceId.Cow, timeframe: KpiTimeframe.Days30),
            Array.Empty<KpiRow>(),
            KpiSourceId.Cow);

        Assert.Equal(KpiBucket.None, series.Bucket);
        Assert.NotNull(series.Message);
    }

    [Fact]
    public void An_untouched_series_knows_it_has_nothing_to_draw()
    {
        var empty = Series(KpiTestData.Definition(timeframe: KpiTimeframe.Days7), Array.Empty<KpiRow>());
        Assert.False(empty.HasAny);
        Assert.Equal(0, empty.Max);

        var touched = Series(KpiTestData.Definition(timeframe: KpiTimeframe.Days7), new[] { Row(0) });
        Assert.True(touched.HasAny);
        Assert.Equal(1, touched.Max);
    }
}
