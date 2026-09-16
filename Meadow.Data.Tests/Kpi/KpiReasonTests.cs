using Meadow.Client.Components.Ui;
using Meadow.Shared.Kpi;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Kpi;

/// <summary>
/// The treatment reason as a KPI filter and ranking dimension.
///
/// The interesting part is not that it works, it is the sentinel. "Ohne Grund" is a value every
/// reasonless row carries, NOT an absence - which is the opposite of how a missing udder quarter is
/// treated, and deliberately so: no quarter is a sentinel row that should fall out of a ranking,
/// no reason is an answer.
/// </summary>
public class KpiReasonTests
{
    private static FakeKpiLookups Lookups()
    {
        var lookups = new FakeKpiLookups();
        lookups.Collars["cow-1"] = "101";
        lookups.Collars["cow-2"] = "102";
        lookups.Medicines[1] = "Penicillin";
        lookups.WhereHows[1] = "Euter";
        lookups.Reasons[1] = "Mastitis";
        lookups.Reasons[2] = "Klauenrehe";
        return lookups;
    }

    private static CowTreatment Treatment(string cowId, int? reasonId, int id = 0)
        => new()
        {
            CowTreatmentId = id,
            EarTagNumber = cowId,
            MedicineId = 1,
            WhereHowId = 1,
            AdministrationDate = KpiTestData.Now,
            TreatmentReasonId = reasonId
        };

    private static IReadOnlyList<KpiRow> Rows(FakeKpiLookups lookups)
        => KpiTestData.Source().BuildRows(lookups);

    [Fact]
    public void A_treatment_with_a_reason_carries_its_name()
    {
        var lookups = Lookups();
        lookups.CowTreatmentList.Add(Treatment("cow-1", 1));

        Assert.Equal(new[] { "Mastitis" }, Rows(lookups)[0].TagValues(KpiTagKeys.Reason));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999)] // an id that no longer exists - the nightly demo reset makes this real
    public void A_treatment_without_a_usable_reason_carries_the_collective_value(int? reasonId)
    {
        var lookups = Lookups();
        lookups.CowTreatmentList.Add(Treatment("cow-1", reasonId));

        Assert.Equal(new[] { KpiFlags.NoReason }, Rows(lookups)[0].TagValues(KpiTagKeys.Reason));
    }

    [Fact]
    public void The_collective_value_is_only_offered_when_a_row_actually_lacks_a_reason()
    {
        // Exactly ReasonFilter.Options' rule in the cow table: otherwise the dialog would list an
        // option that is guaranteed to find nothing.
        var withReasons = Lookups();
        withReasons.CowTreatmentList.Add(Treatment("cow-1", 1, id: 1));
        withReasons.CowTreatmentList.Add(Treatment("cow-2", 2, id: 2));

        var options = KpiTestData.Source().OptionsFor(KpiTagKeys.Reason, Rows(withReasons));
        Assert.DoesNotContain(KpiFlags.NoReason, options);

        var mixed = Lookups();
        mixed.CowTreatmentList.Add(Treatment("cow-1", 1, id: 1));
        mixed.CowTreatmentList.Add(Treatment("cow-2", null, id: 2));

        Assert.Contains(KpiFlags.NoReason, KpiTestData.Source().OptionsFor(KpiTagKeys.Reason, Rows(mixed)));
    }

    [Fact]
    public void The_collective_value_is_the_one_the_cow_table_offers()
    {
        // Two constants for one string is how a tile and the table behind it drift apart. Same
        // guard ClawTableFilter.BandageOption has against KpiFlags.Bandage.
        Assert.Equal(ReasonFilter.NoneOption, KpiFlags.NoReason);
    }

    [Fact]
    public void The_base_data_editor_refuses_the_collective_value_as_a_reason_name()
    {
        // Without this, a real reason named "Ohne Grund" would be indistinguishable from the rows
        // that have none, and a stored tile would quietly count both.
        Assert.True(ReasonFilter.IsReservedReasonName("Ohne Grund"));
        Assert.True(ReasonFilter.IsReservedReasonName("  ohne grund "));
        Assert.False(ReasonFilter.IsReservedReasonName("Mastitis"));
    }

    [Fact]
    public void Filtering_by_reason_counts_only_those_treatments()
    {
        var lookups = Lookups();
        lookups.CowTreatmentList.Add(Treatment("cow-1", 1, id: 1));
        lookups.CowTreatmentList.Add(Treatment("cow-2", 2, id: 2));
        lookups.CowTreatmentList.Add(Treatment("cow-2", null, id: 3));

        var definition = KpiTestData.Definition();
        definition.Filters[KpiTagKeys.Reason] = new List<string> { "Mastitis" };

        var result = KpiEvaluator.Evaluate(
            definition, KpiTestData.Source(), Rows(lookups), KpiTestData.Now);

        Assert.Equal(1, result.MatchedRows);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Filtering_by_the_collective_value_finds_the_reasonless_treatments()
    {
        var lookups = Lookups();
        lookups.CowTreatmentList.Add(Treatment("cow-1", 1, id: 1));
        lookups.CowTreatmentList.Add(Treatment("cow-2", null, id: 2));

        var definition = KpiTestData.Definition();
        definition.Filters[KpiTagKeys.Reason] = new List<string> { KpiFlags.NoReason };

        var result = KpiEvaluator.Evaluate(
            definition, KpiTestData.Source(), Rows(lookups), KpiTestData.Now);

        Assert.Equal(1, result.MatchedRows);
    }

    [Fact]
    public void Grouping_by_reason_puts_treatments_without_one_in_their_own_group()
    {
        // Two reasonless rows beat one Mastitis. If the absence were modelled as "no tag value",
        // the ranking would skip them and answer "Mastitis" - which is not what the data says.
        var lookups = Lookups();
        lookups.CowTreatmentList.Add(Treatment("cow-1", 1, id: 1));
        lookups.CowTreatmentList.Add(Treatment("cow-1", null, id: 2));
        lookups.CowTreatmentList.Add(Treatment("cow-2", null, id: 3));

        var definition = KpiTestData.Definition(
            measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.Reason);

        var result = KpiEvaluator.Evaluate(
            definition, KpiTestData.Source(), Rows(lookups), KpiTestData.Now);

        Assert.Equal(KpiFlags.NoReason, result.Label);
    }

    [Fact]
    public void Grouping_by_where_how_ranks_the_treatment_kinds()
    {
        var lookups = Lookups();
        lookups.WhereHows[2] = "Injektion";
        lookups.CowTreatmentList.Add(Treatment("cow-1", 1, id: 1));
        var second = Treatment("cow-2", 1, id: 2);
        second.WhereHowId = 2;
        lookups.CowTreatmentList.Add(second);
        var third = Treatment("cow-2", 1, id: 3);
        third.WhereHowId = 2;
        lookups.CowTreatmentList.Add(third);

        var definition = KpiTestData.Definition(
            measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.WhereHow);

        var result = KpiEvaluator.Evaluate(
            definition, KpiTestData.Source(), Rows(lookups), KpiTestData.Now);

        Assert.Equal("Injektion", result.Label);
    }

    [Fact]
    public void Both_cow_sources_declare_the_reason_tag_and_grouping()
    {
        // Treatment_Reason_ID exists on exactly these two tables, and on no other.
        foreach (var id in new[] { KpiSourceId.CowTreatment, KpiSourceId.PlannedCowTreatment })
        {
            var source = KpiSourceRegistry.Find(id)!;
            Assert.Contains(source.Tags, t => t.Key == KpiTagKeys.Reason);
            Assert.Contains(KpiGroupBy.Reason, source.Groupings);
        }

        foreach (var id in new[] { KpiSourceId.ClawTreatment, KpiSourceId.PlannedClawTreatment, KpiSourceId.Cow })
        {
            var source = KpiSourceRegistry.Find(id)!;
            Assert.DoesNotContain(source.Tags, t => t.Key == KpiTagKeys.Reason);
            Assert.DoesNotContain(KpiGroupBy.Reason, source.Groupings);
        }
    }

    [Fact]
    public void The_tag_label_and_the_grouping_label_are_the_same_words()
    {
        // "Behandlungsgrund" in the filter list and "Grund" in the ranking would read as two
        // different things.
        var source = KpiSourceRegistry.Find(KpiSourceId.CowTreatment)!;
        var tag = source.Tags.First(t => t.Key == KpiTagKeys.Reason);

        Assert.Equal(tag.Label, KpiDefinitionSummary.GroupBy(KpiGroupBy.Reason));
    }

    [Fact]
    public void The_generated_sql_joins_the_reason_table_once_and_folds_null_into_the_collective_value()
    {
        var definition = KpiTestData.Definition();
        definition.Filters[KpiTagKeys.Reason] = new List<string> { "Mastitis", KpiFlags.NoReason };

        var sql = KpiSqlBuilder.Build(definition, KpiTestData.Source()).Sql;

        Assert.Contains("LEFT JOIN Treatment_Reason tr", sql);
        Assert.Single(sql.Split("LEFT JOIN Treatment_Reason tr").Skip(1));
        // Without COALESCE a filter on "Ohne Grund" matches nothing at all, because the column is
        // NULL there and NULL IN (...) is never true.
        Assert.Contains("COALESCE(tr.Treatment_Reason_Name, 'Ohne Grund')", sql);
    }

    [Fact]
    public void Ranking_by_reason_keeps_the_reasonless_treatments_as_their_own_group()
    {
        // The ranking's inner query drops "label IS NOT NULL". Without COALESCE the reasonless rows
        // would silently leave the ranking, and the SQL would disagree with the preview.
        var sql = KpiSqlBuilder.Build(
            KpiTestData.Definition(measure: KpiMeasure.TopValue, groupBy: KpiGroupBy.Reason),
            KpiTestData.Source()).Sql;

        Assert.Contains("COALESCE(tr.Treatment_Reason_Name, 'Ohne Grund') AS label", sql);
    }

    [Fact]
    public void A_reason_filter_is_carried_into_the_drill_down_link()
    {
        var definition = KpiTestData.Definition();
        definition.Filters[KpiTagKeys.Reason] = new List<string> { "Mastitis" };

        var href = KpiDrillDownUrl.Build(new KpiTileModel
        {
            Kpi = new KPI { Kind = (int)KpiKind.Builder, Definition = KpiDefinition.Serialize(definition) },
            Result = new KpiResult { State = KpiResultState.Ok, Display = "1" }
        });

        Assert.Contains("reason=Mastitis", href);
    }
}
