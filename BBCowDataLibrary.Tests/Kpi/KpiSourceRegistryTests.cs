using BB_Cow.Class;
using BB_Cow.Kpi;

namespace BBCowDataLibrary.Tests.Kpi;

/// <summary>
/// Tests for the row projection - the one place that knows the schema's traps. Every case here
/// exists because getting it wrong produces a plausible-looking wrong number rather than a crash.
/// </summary>
public class KpiSourceRegistryTests
{
    private static readonly DateTime Date = new(2026, 6, 1);

    private static IReadOnlyList<KpiRow> Build(KpiSourceId id, IKpiLookups lookups)
        => KpiSourceRegistry.Find(id)!.BuildRows(lookups);

    [Fact]
    public void Cow_treatment_resolves_the_collar_through_Cow_ID_not_the_ear_tag()
    {
        // THE trap. Cow_Treatment.Ear_Tag_Number actually stores Cow.Cow_ID; for a calf that is a
        // GUID and never equals an ear tag. Joining on the ear tag - which the column name invites -
        // silently drops every calf, so the KPI would be quietly too low rather than broken.
        var calfId = Guid.NewGuid().ToString();
        var lookups = new FakeKpiLookups();
        lookups.Collars[calfId] = "77";
        lookups.Medicines[1] = "Penicillin";
        lookups.WhereHows[2] = "Euter";
        lookups.Udders[3] = "LV/ RH";
        lookups.CowTreatmentList.Add(new CowTreatment(1, calfId, 1, Date, 12.5f, 2, 3));

        var row = Assert.Single(Build(KpiSourceId.CowTreatment, lookups));

        Assert.Equal(calfId, row.CowId);
        Assert.Equal("77", row.CowLabel);
        Assert.Equal(Date, row.Date);
        Assert.Equal(12.5, row.Dosage);
        Assert.Equal(new[] { "Penicillin" }, row.TagValues(KpiTagKeys.Medicine));
        Assert.Equal(new[] { "Euter" }, row.TagValues(KpiTagKeys.WhereHow));
        Assert.Equal(new[] { "LV/ RH" }, row.TagValues(KpiTagKeys.UdderQuarter));
        Assert.Equal(new[] { "77" }, row.TagValues(KpiTagKeys.Cow));
    }

    [Fact]
    public void The_sentinel_udder_row_produces_no_quarter_value_at_all()
    {
        // Replaces "WHERE c.UDDER_ID != 16" without knowing any id: GetUdderString returns "" when
        // no quarter is set, and an empty value is dropped from the tags entirely.
        var lookups = new FakeKpiLookups();
        lookups.Collars["cow1"] = "1";
        lookups.Udders[16] = string.Empty;
        lookups.CowTreatmentList.Add(new CowTreatment(1, "cow1", 1, Date, 1f, 1, 16));

        var row = Assert.Single(Build(KpiSourceId.CowTreatment, lookups));

        Assert.Empty(row.TagValues(KpiTagKeys.UdderQuarter));
        Assert.False(row.Tags.ContainsKey(KpiTagKeys.UdderQuarter));
    }

    [Fact]
    public void An_unknown_lookup_never_becomes_a_filter_option()
    {
        // The lookup services return the literal "--" for an unknown id. That is a display
        // placeholder; if it leaked into the tags it would show up as a selectable medicine.
        var lookups = new FakeKpiLookups();
        lookups.Collars["cow1"] = "1";
        lookups.Medicines[1] = "--";
        lookups.CowTreatmentList.Add(new CowTreatment(1, "cow1", 1, Date, 1f, 99, 99));

        var row = Assert.Single(Build(KpiSourceId.CowTreatment, lookups));

        Assert.Empty(row.TagValues(KpiTagKeys.Medicine));
        Assert.Empty(row.TagValues(KpiTagKeys.WhereHow));
    }

    [Fact]
    public void Claw_findings_carry_the_bandage_and_block_states_in_the_same_group()
    {
        // Same group on purpose: Claw_Table offers findings and the two states in ONE multi-select,
        // so they are OR-ed there. A separate group would AND them and the drill-down would show a
        // different set than the tile.
        var lookups = new FakeKpiLookups();
        lookups.Collars["cow1"] = "5";
        lookups.ClawTreatmentList.Add(new ClawTreatment(
            1, "cow1", Date,
            clawFindingLV: "Mortellaro", bandageLV: true, blockLV: false,
            clawFindingLH: "", bandageLH: false, blockLH: false,
            clawFindingRV: "", bandageRV: false, blockRV: true,
            clawFindingRH: "", bandageRH: false, blockRH: false,
            isBandageRemoved: false));

        var row = Assert.Single(Build(KpiSourceId.ClawTreatment, lookups));

        Assert.Equal(
            new[] { "Mortellaro", KpiFlags.Bandage, KpiFlags.Block },
            row.TagValues(KpiTagKeys.ClawFinding));
    }

    [Fact]
    public void A_removed_bandage_is_not_a_bandage()
    {
        // Mirrors Claw_Table.MatchesFinding: "!r.IsBandageRemoved && Any(GetBandage)". A block has
        // no such condition, so it survives.
        var lookups = new FakeKpiLookups();
        lookups.Collars["cow1"] = "5";
        lookups.ClawTreatmentList.Add(new ClawTreatment(
            1, "cow1", Date,
            clawFindingLV: "Mortellaro", bandageLV: true, blockLV: true,
            clawFindingLH: "", bandageLH: false, blockLH: false,
            clawFindingRV: "", bandageRV: false, blockRV: false,
            clawFindingRH: "", bandageRH: false, blockRH: false,
            isBandageRemoved: true));

        var row = Assert.Single(Build(KpiSourceId.ClawTreatment, lookups));

        Assert.DoesNotContain(KpiFlags.Bandage, row.TagValues(KpiTagKeys.ClawFinding));
        Assert.Contains(KpiFlags.Block, row.TagValues(KpiTagKeys.ClawFinding));
    }

    [Fact]
    public void The_same_finding_on_several_claws_counts_as_one_value()
    {
        // Otherwise a single treatment would count four times in a Top-1 ranking.
        var lookups = new FakeKpiLookups();
        lookups.Collars["cow1"] = "5";
        lookups.ClawTreatmentList.Add(new ClawTreatment(
            1, "cow1", Date,
            clawFindingLV: "Mortellaro", bandageLV: false, blockLV: false,
            clawFindingLH: " mortellaro ", bandageLH: false, blockLH: false,
            clawFindingRV: "", bandageRV: false, blockRV: false,
            clawFindingRH: "", bandageRH: false, blockRH: false,
            isBandageRemoved: false));

        var row = Assert.Single(Build(KpiSourceId.ClawTreatment, lookups));

        Assert.Equal(new[] { "Mortellaro" }, row.TagValues(KpiTagKeys.ClawFinding));
    }

    [Fact]
    public void Planned_claw_treatments_expose_the_ticked_claws()
    {
        // Planned findings are four booleans, not names, so the only thing this table records about
        // a finding is which claw it concerns.
        var lookups = new FakeKpiLookups();
        lookups.Collars["cow1"] = "9";
        lookups.PlannedClawTreatmentList.Add(new PlannedClawTreatment(
            1, "cow1", Date, "Kontrolle",
            clawFindingLV: true, clawFindingLH: false, clawFindingRV: false, clawFindingRH: true));

        var row = Assert.Single(Build(KpiSourceId.PlannedClawTreatment, lookups));

        Assert.Equal(new[] { "LV", "RH" }, row.TagValues(KpiTagKeys.ClawPosition));
    }

    [Fact]
    public void Planned_cow_treatments_expose_found_and_treated_as_separate_groups()
    {
        // Separate groups so they AND: "found but not yet treated" is the whole point of that
        // table, and a single OR-ed group could never express it.
        var lookups = new FakeKpiLookups();
        lookups.Collars["cow1"] = "9";
        lookups.PlannedCowTreatmentList.Add(new PlannedCowTreatment(
            1, "cow1", 1, Date, 3f, 1, isFound: true, isTreatet: false, udderId: 1));

        var row = Assert.Single(Build(KpiSourceId.PlannedCowTreatment, lookups));

        Assert.Equal(new[] { KpiFlags.Found }, row.TagValues(KpiTagKeys.Found));
        Assert.Equal(new[] { KpiFlags.NotTreated }, row.TagValues(KpiTagKeys.Treated));
    }

    [Fact]
    public void Cows_have_no_date_and_expose_calf_and_herd_state()
    {
        var lookups = new FakeKpiLookups();
        lookups.CowList.Add(Cow.CreateCalf(42));
        lookups.CowList.Add(new Cow("DE123", 7, isGone: true));

        var rows = Build(KpiSourceId.Cow, lookups);

        Assert.All(rows, r => Assert.Null(r.Date));

        var calf = rows.Single(r => r.CowLabel == "42");
        Assert.Equal(new[] { KpiFlags.Calf }, calf.TagValues(KpiTagKeys.Calf));
        Assert.Equal(new[] { KpiFlags.InHerd }, calf.TagValues(KpiTagKeys.Herd));

        var gone = rows.Single(r => r.CowLabel == "7");
        Assert.Equal(new[] { KpiFlags.NotCalf }, gone.TagValues(KpiTagKeys.Calf));
        Assert.Equal(new[] { KpiFlags.Gone }, gone.TagValues(KpiTagKeys.Herd));
    }

    [Fact]
    public void Options_come_from_the_values_that_actually_occur()
    {
        // The rule Cow_Table already documents: not the lookup table, which carries auto-created
        // names nobody ever used.
        var source = KpiSourceRegistry.Find(KpiSourceId.CowTreatment)!;
        var rows = new[]
        {
            KpiTestData.Row(tags: KpiTestData.Tag(KpiTagKeys.Medicine, "Zink")),
            KpiTestData.Row(tags: KpiTestData.Tag(KpiTagKeys.Medicine, "Aspirin")),
            KpiTestData.Row(tags: KpiTestData.Tag(KpiTagKeys.Medicine, "aspirin"))
        };

        Assert.Equal(new[] { "Aspirin", "Zink" }, source.OptionsFor(KpiTagKeys.Medicine, rows));
    }

    [Fact]
    public void Every_source_is_declared_exactly_once()
    {
        Assert.Equal(
            Enum.GetValues<KpiSourceId>().Length,
            KpiSourceRegistry.All.Select(s => s.Id).Distinct().Count());

        Assert.All(KpiSourceRegistry.All, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Label));
            Assert.NotEmpty(s.Measures);
        });
    }

    [Fact]
    public void Every_source_has_a_page_to_drill_into()
    {
        // The cow source used to be the exception: nothing listed animals, so it declared no route
        // and its tile was not a link. Since /Kuehe exists it has one like everyone else - and
        // "Kuehe", not "Kuh_Daten", which is the cow TREATMENTS table and would show 120
        // treatments behind a tile counting 37 animals.
        Assert.Equal("Kuehe", KpiSourceRegistry.Find(KpiSourceId.Cow)!.Route);

        Assert.All(KpiSourceRegistry.All, s => Assert.True(s.HasDrillDown));
    }

    [Fact]
    public void Only_dated_sources_offer_a_timeframe()
    {
        Assert.True(KpiSourceRegistry.Find(KpiSourceId.CowTreatment)!.SupportsTimeframe);
        Assert.True(KpiSourceRegistry.Find(KpiSourceId.PlannedCowTreatment)!.SupportsTimeframe);
        Assert.False(KpiSourceRegistry.Find(KpiSourceId.Cow)!.SupportsTimeframe);
    }
}
