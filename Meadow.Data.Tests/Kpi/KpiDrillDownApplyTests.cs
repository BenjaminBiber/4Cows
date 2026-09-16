using Meadow.Client.Components.Ui;
using Meadow.Shared.Kpi;
using Meadow.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace Meadow.Data.Tests.Kpi;

/// <summary>
/// The other half of the drill-down, which had no tests at all: KpiDrillDownUrl builds the link,
/// and THESE adapters lay it onto the pages' existing filter objects.
///
/// The gap that made this worth writing: the claw table was the only one of the four without a
/// Range, so "range=30" arrived and was dropped on the floor. The tile said 4 and the table behind
/// it opened all 80 rows - exactly the disagreement KpiTimeframe was pinned to DateRange to avoid.
/// </summary>
public class KpiDrillDownApplyTests
{
    /// <summary>
    /// The smallest NavigationManager that can hold a URI. The adapters only ever read Uri.
    /// </summary>
    private sealed class FakeNav : NavigationManager
    {
        public FakeNav(string query)
            => Initialize("http://localhost/", "http://localhost/Klauen_Daten" + query);
    }

    private static string Link(KpiDefinition definition, string? label = null)
        => KpiDrillDownUrl.Build(new KpiTileModel
        {
            Kpi = new KPI { Kind = (int)KpiKind.Builder, Definition = KpiDefinition.Serialize(definition) },
            Result = new KpiResult { State = KpiResultState.Ok, Display = "1", Label = label }
        });

    private static string QueryOf(string href)
    {
        var index = href.IndexOf('?');
        return index < 0 ? "" : href[index..];
    }

    [Theory]
    [InlineData(KpiTimeframe.Days7, DateRange.Days7)]
    [InlineData(KpiTimeframe.Days30, DateRange.Days30)]
    [InlineData(KpiTimeframe.Days90, DateRange.Days90)]
    public void A_claw_tile_carries_its_timeframe_into_the_claw_table(
        KpiTimeframe timeframe, DateRange expected)
    {
        var definition = KpiTestData.Definition(
            source: KpiSourceId.ClawTreatment, timeframe: timeframe);

        var filter = new ClawTableFilter();
        KpiDrillDown.ApplyTo(filter, new FakeNav(QueryOf(Link(definition))));

        Assert.Equal(expected, filter.Range);
    }

    [Fact]
    public void A_claw_tile_without_a_timeframe_leaves_the_table_alone()
    {
        // "All" writes no range at all, and the page keeps whatever the user had set.
        var filter = new ClawTableFilter { Range = DateRange.Days7 };

        KpiDrillDown.ApplyTo(
            filter,
            new FakeNav(QueryOf(Link(KpiTestData.Definition(source: KpiSourceId.ClawTreatment)))));

        Assert.Equal(DateRange.Days7, filter.Range);
    }

    [Fact]
    public void An_unknown_range_leaves_the_table_alone_instead_of_guessing()
    {
        // The adapters used to read "7 => Days7, 30 => Days30, _ => unchanged". That silently did
        // nothing for a 90 - and would do nothing for whatever comes next.
        var filter = new ClawTableFilter { Range = DateRange.Days30 };

        KpiDrillDown.ApplyTo(filter, new FakeNav("?range=45"));

        Assert.Equal(DateRange.Days30, filter.Range);
    }

    [Theory]
    [InlineData(KpiTimeframe.Days90, DateRange.Days90)]
    [InlineData(KpiTimeframe.Days7, DateRange.Days7)]
    public void A_cow_tile_carries_its_timeframe_into_the_cow_table(
        KpiTimeframe timeframe, DateRange expected)
    {
        var definition = KpiTestData.Definition(timeframe: timeframe);

        var filter = new CowTableFilter();
        KpiDrillDown.ApplyTo(filter, new FakeNav(QueryOf(Link(definition))));

        Assert.Equal(expected, filter.Range);
    }

    [Fact]
    public void A_planned_tile_carries_ninety_days_forward()
    {
        var definition = KpiTestData.Definition(
            source: KpiSourceId.PlannedCowTreatment, timeframe: KpiTimeframe.Days90);

        var filter = new PlannedCowTableFilter();
        KpiDrillDown.ApplyTo(filter, new FakeNav(QueryOf(Link(definition))));

        Assert.Equal(PlannedDateRange.Next90, filter.Range);
    }

    [Fact]
    public void A_reason_filter_reaches_the_cow_tables_reason_chips()
    {
        var definition = KpiTestData.Definition();
        definition.Filters[KpiTagKeys.Reason] = new List<string> { "Mastitis", KpiFlags.NoReason };

        var filter = new CowTableFilter();
        KpiDrillDown.ApplyTo(filter, new FakeNav(QueryOf(Link(definition))));

        Assert.Equal(2, filter.Reasons.Count);
        Assert.Contains("Mastitis", filter.Reasons);
        Assert.Contains(KpiFlags.NoReason, filter.Reasons);
    }

    [Fact]
    public void A_reason_filter_reaches_the_planned_cow_table_too()
    {
        var definition = KpiTestData.Definition(source: KpiSourceId.PlannedCowTreatment);
        definition.Filters[KpiTagKeys.Reason] = new List<string> { "Mastitis" };

        var filter = new PlannedCowTableFilter();
        KpiDrillDown.ApplyTo(filter, new FakeNav(QueryOf(Link(definition))));

        Assert.Contains("Mastitis", filter.Reasons);
    }

    [Fact]
    public void The_claw_range_counts_towards_the_filter_badge()
    {
        // The badge, "reset" and the row counter all read ActiveCount. A range that filtered rows
        // without showing up there would look like a bug in the table.
        var filter = new ClawTableFilter();
        Assert.Equal(0, filter.ActiveCount);

        filter.Range = DateRange.Days30;
        Assert.Equal(1, filter.ActiveCount);

        filter.Reset();
        Assert.Equal(DateRange.All, filter.Range);
    }
}
