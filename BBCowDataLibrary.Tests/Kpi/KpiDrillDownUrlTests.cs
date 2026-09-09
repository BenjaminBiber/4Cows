using BB_Cow.Class;
using BB_Cow.Kpi;

namespace BBCowDataLibrary.Tests.Kpi;

public class KpiDrillDownUrlTests
{
    private static KpiTileModel Tile(KpiDefinition definition, string? label = null, string url = "")
        => new()
        {
            Kpi = new KPI
            {
                Title = "t",
                Url = url,
                Script = string.Empty,
                Kind = (int)KpiKind.Builder,
                Definition = KpiDefinition.Serialize(definition)
            },
            Result = new KpiResult { State = KpiResultState.Ok, Display = label ?? "1", Label = label }
        };

    [Fact]
    public void An_unfiltered_kpi_links_to_the_bare_route()
    {
        var url = KpiDrillDownUrl.Build(Tile(new KpiDefinition { Source = KpiSourceId.ClawTreatment }));

        Assert.Equal("Klauen_Daten", url);
    }

    [Fact]
    public void Filters_and_timeframe_become_the_query()
    {
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.CowTreatment,
            Timeframe = KpiTimeframe.Days30,
            Filters = { [KpiTagKeys.Medicine] = new List<string> { "Penicillin", "Cefa" } }
        };

        var url = KpiDrillDownUrl.Build(Tile(definition));

        Assert.Equal("Kuh_Daten?medicine=Penicillin,Cefa&range=30", url);
    }

    [Fact]
    public void Round_trips_values_containing_a_comma_and_umlauts()
    {
        // Both are realistic: findings are free text, and a comma would otherwise split one value
        // into two.
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.ClawTreatment,
            Filters = { [KpiTagKeys.ClawFinding] = new List<string> { "Sohlengeschwür, tief", "Mortellaro" } }
        };

        var query = KpiDrillDownUrl.Parse(Query(KpiDrillDownUrl.Build(Tile(definition))));

        Assert.Equal(
            new[] { "Sohlengeschwür, tief", "Mortellaro" },
            KpiDrillDownUrl.Values(query, KpiTagKeys.ClawFinding));
    }

    [Fact]
    public void Round_trips_every_filter_group_independently()
    {
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.PlannedCowTreatment,
            Timeframe = KpiTimeframe.Days7,
            Filters =
            {
                [KpiTagKeys.Medicine] = new List<string> { "Zink" },
                [KpiTagKeys.Found] = new List<string> { KpiFlags.Found },
                [KpiTagKeys.Treated] = new List<string> { KpiFlags.NotTreated }
            }
        };

        var query = KpiDrillDownUrl.Parse(Query(KpiDrillDownUrl.Build(Tile(definition))));

        Assert.Equal(new[] { "Zink" }, KpiDrillDownUrl.Values(query, KpiTagKeys.Medicine));
        Assert.Equal(new[] { KpiFlags.Found }, KpiDrillDownUrl.Values(query, KpiTagKeys.Found));
        Assert.Equal(new[] { KpiFlags.NotTreated }, KpiDrillDownUrl.Values(query, KpiTagKeys.Treated));
        Assert.Equal(7, KpiDrillDownUrl.Range(query));
    }

    [Fact]
    public void An_empty_filter_group_is_left_out_of_the_url()
    {
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.CowTreatment,
            Filters = { [KpiTagKeys.Medicine] = new List<string>() }
        };

        Assert.Equal("Kuh_Daten", KpiDrillDownUrl.Build(Tile(definition)));
    }

    [Fact]
    public void A_top_one_cow_kpi_filters_the_table_to_that_exact_cow()
    {
        // As the ordinary "cow" filter group, NOT as a search term. The search matches substrings,
        // so "104" also hits ear tag "...1042" and the table showed unrelated animals - 16 rows
        // behind a tile that said 8.
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.CowTreatment,
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.Cow
        };

        var url = KpiDrillDownUrl.Build(Tile(definition, label: "300"));

        Assert.Equal("Kuh_Daten?cow=300", url);
    }

    [Fact]
    public void An_explicit_cow_filter_is_not_overwritten_by_the_ranking_winner()
    {
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.CowTreatment,
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.Cow,
            Filters = { [KpiTagKeys.Cow] = new List<string> { "7", "9" } }
        };

        Assert.Equal("Kuh_Daten?cow=7,9", KpiDrillDownUrl.Build(Tile(definition, label: "9")));
    }

    [Fact]
    public void A_ranking_by_something_other_than_a_cow_needs_no_search_term()
    {
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.CowTreatment,
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.UdderQuarter
        };

        Assert.Equal("Kuh_Daten", KpiDrillDownUrl.Build(Tile(definition, label: "LV/ RH")));
    }

    [Fact]
    public void A_sql_kpi_keeps_its_hand_typed_target()
    {
        var kpi = new KPI
        {
            Title = "alt",
            Url = "Klauen_Daten",
            Script = "SELECT 1 AS value",
            Kind = (int)KpiKind.Sql
        };

        var tile = new KpiTileModel
        {
            Kpi = kpi,
            Result = new KpiResult { State = KpiResultState.Ok, Display = "1" }
        };

        Assert.Equal("Klauen_Daten", KpiDrillDownUrl.Build(tile));
    }

    [Fact]
    public void An_unreadable_definition_falls_back_to_the_stored_url()
    {
        var tile = new KpiTileModel
        {
            Kpi = new KPI
            {
                Title = "kaputt",
                Url = "/Settings",
                Script = string.Empty,
                Kind = (int)KpiKind.Builder,
                Definition = "{ not json"
            },
            Result = KpiResult.Failed("unlesbar")
        };

        Assert.Equal("/Settings", KpiDrillDownUrl.Build(tile));
    }

    [Fact]
    public void The_add_tile_keeps_pointing_at_the_settings()
        => Assert.Equal("/Settings", KpiDrillDownUrl.Build(KpiTileModel.AddTile()));

    [Fact]
    public void The_cow_source_links_to_the_cow_list()
    {
        // This test used to assert the opposite: the cow source had no page, so its tile got no
        // link at all - not even a filtered one to some other page, because the reader would see
        // a list of something else and take it for the number they clicked. /Kuehe removed that
        // dead end. The !HasDrillDown branch in Build stays as a guard but is now unreachable
        // through a real source, since Build looks the source up in the registry.
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.Cow,
            Filters = { [KpiTagKeys.Herd] = new List<string> { KpiFlags.InHerd } }
        };

        Assert.Equal("Kuehe?herd=Im%20Bestand", KpiDrillDownUrl.Build(Tile(definition, url: "Kuh_Daten")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("?")]
    [InlineData("?=nokey")]
    [InlineData("?medicine=")]
    public void Parsing_junk_yields_nothing_rather_than_throwing(string? query)
        => Assert.Empty(KpiDrillDownUrl.Parse(query));

    [Fact]
    public void Range_reports_zero_when_absent_or_unparseable()
    {
        Assert.Equal(0, KpiDrillDownUrl.Range(KpiDrillDownUrl.Parse("?medicine=Zink")));
        Assert.Equal(0, KpiDrillDownUrl.Range(KpiDrillDownUrl.Parse("?range=irgendwas")));
        Assert.Equal(30, KpiDrillDownUrl.Range(KpiDrillDownUrl.Parse("?range=30")));
    }

    private static string Query(string url)
    {
        var index = url.IndexOf('?');
        return index < 0 ? string.Empty : url[index..];
    }
}
