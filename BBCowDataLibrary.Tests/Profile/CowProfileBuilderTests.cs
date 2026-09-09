using BB_Cow.Class;
using BB_Cow.Kpi;
using BB_Cow.Profile;
using static BBCowDataLibrary.Tests.Profile.CowProfileTestData;

namespace BBCowDataLibrary.Tests.Profile;

public class CowProfileBuilderTests
{
    // ---- Which rows belong to this animal ------------------------------

    [Fact]
    public void Only_treatments_of_this_cow_are_counted()
    {
        var profile = Build(
            cow: new[] { Treatment(), Treatment(cowId: Other) },
            claw: new[] { Claw(HoofPosition.LV), Claw(HoofPosition.LV, cowId: Other) });

        Assert.Equal(1, profile.TotalCowTreatments);
        Assert.Equal(1, profile.TotalClawTreatments);
        Assert.Equal(2, profile.TotalTreatments);
    }

    [Fact]
    public void A_cow_id_that_is_a_prefix_of_another_does_not_leak()
    {
        // Ear tags share long prefixes, and for identified animals the ear tag IS the Cow_ID.
        // A StartsWith or Contains slip in the filter would show a neighbour's treatments here.
        var profile = Build(
            cow: new[] { Treatment(cowId: "DE 08 1523 100"), Treatment(cowId: "DE 08 1523 1001") },
            cowId: "DE 08 1523 100");

        Assert.Equal(1, profile.TotalCowTreatments);
    }

    [Fact]
    public void Cows_are_grouped_by_id_not_by_collar_number()
    {
        // Collar numbers are re-issued once an animal leaves (IsCollarInUse only reserves the
        // numbers of cows still in the herd), so two Cow rows genuinely can share collar 142.
        var profile = Build(cow: new[] { Treatment(), Treatment(cowId: Other), Treatment(cowId: Other) });

        Assert.Equal(1, profile.TotalCowTreatments);
    }

    [Fact]
    public void Treatments_are_sorted_newest_first()
    {
        var profile = Build(cow: new[]
        {
            Treatment(date: Today.AddDays(-30), id: 1),
            Treatment(date: Today, id: 2),
            Treatment(date: Today.AddDays(-5), id: 3)
        });

        Assert.Equal(new[] { 2, 3, 1 }, profile.CowTreatments.Select(t => t.CowTreatmentId));
    }

    [Fact]
    public void The_last_treatment_is_the_newest_of_both_kinds()
    {
        var profile = Build(
            cow: new[] { Treatment(date: Today.AddDays(-10)) },
            claw: new[] { Claw(HoofPosition.LV, date: Today.AddDays(-2)) });

        Assert.Equal(Today.AddDays(-2), profile.LastTreatment);
    }

    [Fact]
    public void A_cow_with_no_treatments_yields_an_empty_profile_with_twelve_zero_months()
    {
        var profile = CowProfileBuilder.Empty(Mine, Today);

        Assert.False(profile.HasAnyTreatment);
        Assert.Null(profile.LastTreatment);
        Assert.Null(profile.DeltaDisplay);
        Assert.Equal(KpiTrend.None, profile.Trend);
        Assert.Equal(12, profile.Months.Count);
        Assert.All(profile.Months, m => Assert.Equal(0, m.Total));
        Assert.Equal(4, profile.Hoofs.Count);
        Assert.Empty(profile.Findings);
        Assert.Equal(0, profile.OpenBandages);
        Assert.Equal(0, profile.OpenPlanned);
    }

    // ---- The rolling windows -------------------------------------------

    [Fact]
    public void The_rolling_window_has_twelve_months_ending_in_the_current_one()
    {
        var profile = Build();

        Assert.Equal(12, profile.Months.Count);
        Assert.Equal((2025, 7), (profile.Months[0].Year, profile.Months[0].Month));
        Assert.Equal((2026, 6), (profile.Months[11].Year, profile.Months[11].Month));
    }

    [Fact]
    public void Months_separate_cow_from_claw_treatments()
    {
        var profile = Build(
            cow: new[] { Treatment(date: new DateTime(2026, 3, 2)), Treatment(date: new DateTime(2026, 3, 28)) },
            claw: new[] { Claw(HoofPosition.LV, date: new DateTime(2026, 3, 15)) });

        var march = profile.Months.Single(m => m is { Year: 2026, Month: 3 });

        Assert.Equal(2, march.CowCount);
        Assert.Equal(1, march.ClawCount);
        Assert.Equal(3, march.Total);
    }

    [Fact]
    public void Treatments_outside_the_rolling_window_count_in_the_total_but_in_no_month()
    {
        // Pre-dating is allowed by the dialogs, so a treatment can sit past the end of the window.
        var profile = Build(cow: new[]
        {
            Treatment(date: Today.AddYears(1)),
            Treatment(date: new DateTime(2020, 1, 1))
        });

        Assert.Equal(2, profile.TotalTreatments);
        Assert.All(profile.Months, m => Assert.Equal(0, m.Total));
    }

    [Fact]
    public void The_last_window_is_the_twelve_months_up_to_and_including_today()
    {
        var profile = Build(cow: new[]
        {
            Treatment(date: Today),                       // last day, inside
            Treatment(date: new DateTime(2025, 6, 16)),   // first day, inside
            Treatment(date: new DateTime(2025, 6, 15))    // one day early, previous window
        });

        Assert.Equal(2, profile.Last12Months);
        Assert.Equal(1, profile.Previous12Months);
    }

    [Fact]
    public void The_previous_window_is_the_twelve_months_before_that()
    {
        var profile = Build(cow: new[]
        {
            Treatment(date: new DateTime(2024, 6, 16)),   // first day of the previous window
            Treatment(date: new DateTime(2024, 6, 15))    // one day early, outside both
        });

        Assert.Equal(0, profile.Last12Months);
        Assert.Equal(1, profile.Previous12Months);
    }

    [Fact]
    public void A_previous_period_of_zero_still_yields_a_delta_when_something_happened()
    {
        var profile = Build(cow: new[] { Treatment(), Treatment(), Treatment() });

        // Absolute, not percent: against a previous period of nothing, "+300 %" would be a lie
        // dressed as precision. The plain count says the same thing honestly.
        Assert.Equal("+3", profile.DeltaDisplay);
        Assert.Equal(KpiTrend.Up, profile.Trend);
    }

    [Fact]
    public void Two_empty_windows_yield_no_delta_at_all()
    {
        var profile = Build(cow: new[] { Treatment(date: new DateTime(2020, 1, 1)) });

        Assert.Null(profile.DeltaDisplay);
        Assert.Equal(KpiTrend.None, profile.Trend);
    }

    [Fact]
    public void A_falling_delta_carries_a_real_minus_sign()
    {
        var profile = Build(cow: new[]
        {
            Treatment(date: Today),
            Treatment(date: new DateTime(2025, 1, 10)),
            Treatment(date: new DateTime(2025, 2, 10)),
            Treatment(date: new DateTime(2025, 3, 10))
        });

        // U+2212, not the hyphen: the hyphen already means "no value" everywhere else in this app.
        Assert.Equal("−2", profile.DeltaDisplay);
        Assert.Equal(KpiTrend.Down, profile.Trend);
    }

    [Fact]
    public void An_unchanged_count_is_flat_and_says_so()
    {
        var profile = Build(cow: new[]
        {
            Treatment(date: Today),
            Treatment(date: new DateTime(2025, 3, 10))
        });

        Assert.Equal("±0", profile.DeltaDisplay);
        Assert.Equal(KpiTrend.Flat, profile.Trend);
    }

    // ---- Claws ----------------------------------------------------------

    [Fact]
    public void Empty_claw_findings_never_become_a_finding()
    {
        // The "Pflege" fallback from AppSetting exists for display only. If it entered the
        // aggregation, every cow's most frequent finding would be "Pflege".
        var profile = Build(claw: new[] { Claw() });

        Assert.Empty(profile.Findings);
        Assert.All(profile.Hoofs, h => Assert.Empty(h.Findings));
    }

    [Fact]
    public void Findings_are_grouped_case_insensitively()
    {
        var profile = Build(claw: new[]
        {
            Claw(HoofPosition.LV, "Mortellaro"),
            Claw(HoofPosition.RH, "mortellaro"),
            Claw(HoofPosition.LH, "Sohlengeschwür")
        });

        var top = Assert.IsType<CowProfileTally>(profile.TopFinding);
        Assert.Equal("Mortellaro", top.Key);
        Assert.Equal(2, top.Count);
        Assert.Equal(2, profile.Findings.Count);
    }

    [Fact]
    public void A_hoof_counts_a_treatment_when_anything_at_all_was_recorded_there()
    {
        var profile = Build(claw: new[]
        {
            Claw(HoofPosition.LV, "Mortellaro"),
            Claw(HoofPosition.LV, finding: "", bandage: true),
            Claw(HoofPosition.RV, "Pflegeschnitt")
        });

        var lv = profile.Hoofs.Single(h => h.Position == HoofPosition.LV);
        Assert.Equal(2, lv.TreatmentCount);
        Assert.Single(lv.Findings);
    }

    [Fact]
    public void Open_bandages_count_claws_not_treatments()
    {
        // One treatment, three bandaged claws - the Verbaende table shows three rows for it,
        // so the tile has to say three too.
        var three = Claw()
            .With(HoofPosition.LV, bandage: true)
            .With(HoofPosition.RV, bandage: true)
            .With(HoofPosition.LH, bandage: true);

        Assert.Equal(3, Build(claw: new[] { three }).OpenBandages);
    }

    [Fact]
    public void A_removed_bandage_is_no_longer_open()
    {
        var removed = Claw(bandageRemoved: true)
            .With(HoofPosition.LV, bandage: true)
            .With(HoofPosition.RV, bandage: true);

        var profile = Build(claw: new[] { removed });

        Assert.Equal(0, profile.OpenBandages);
        // The historic count survives - "this claw was bandaged twice" stays true.
        Assert.Equal(1, profile.Hoofs.Single(h => h.Position == HoofPosition.LV).BandageCount);
    }

    [Fact]
    public void A_block_survives_bandage_removal()
    {
        // The exact display bug ClawSummary's header comment records: IsBandageRemoved is set
        // treatment-wide and used to make the block disappear with the bandage.
        var treatment = Claw(bandageRemoved: true).With(HoofPosition.RH, bandage: true, block: true);

        var rh = Build(claw: new[] { treatment }).Hoofs.Single(h => h.Position == HoofPosition.RH);

        Assert.Equal(0, rh.OpenBandageCount);
        Assert.Equal(1, rh.BlockCount);
    }

    [Fact]
    public void Hoofs_come_back_in_the_drawing_order_of_the_grid()
    {
        Assert.Equal(HoofPositions.All, Build().Hoofs.Select(h => h.Position));
    }

    // ---- Udder quarters --------------------------------------------------

    [Fact]
    public void Udder_quarters_count_every_quarter_a_treatment_touches()
    {
        var profile = Build(cow: new[]
        {
            Treatment(udderId: 1),   // LV
            Treatment(udderId: 2)    // alle vier
        });

        Assert.Equal(2, profile.UdderQuarters[HoofPosition.LV]);
        Assert.Equal(1, profile.UdderQuarters[HoofPosition.RH]);
        // Two treatments, five quarter hits - that is why the page has to say
        // "Mehrfachnennung moeglich".
        Assert.Equal(2, profile.UdderTreatments);
        Assert.Equal(5, profile.UdderQuarters.Values.Sum());
    }

    [Fact]
    public void Udder_quarters_ignore_the_sentinel_the_all_false_row_and_unknown_ids()
    {
        var profile = Build(cow: new[]
        {
            Treatment(udderId: int.MinValue),   // nothing chosen in the dialog
            Treatment(udderId: 9),              // the "no particular quarter" combination
            Treatment(udderId: 4711)            // an id no lookup row has
        });

        Assert.Equal(0, profile.UdderTreatments);
        Assert.All(profile.UdderQuarters.Values, count => Assert.Equal(0, count));
        // They are still treatments - only their location is missing.
        Assert.Equal(3, profile.TotalCowTreatments);
    }

    // ---- Tallies ---------------------------------------------------------

    [Fact]
    public void Medicines_are_ordered_by_count_descending()
    {
        var profile = Build(cow: new[]
        {
            Treatment(medicineId: 7),
            Treatment(medicineId: 3),
            Treatment(medicineId: 7)
        });

        Assert.Equal(new[] { 7, 3 }, profile.Medicines.Select(m => m.Id));
        Assert.Equal(2, profile.Medicines[0].Count);
    }

    [Fact]
    public void A_tie_is_broken_by_id_so_the_top_tile_does_not_flicker()
    {
        var profile = Build(cow: new[] { Treatment(medicineId: 9), Treatment(medicineId: 4) });

        Assert.Equal(new[] { 4, 9 }, profile.Medicines.Select(m => m.Id));
    }

    [Fact]
    public void Treatments_with_no_reason_land_in_the_no_reason_bucket()
    {
        var profile = Build(cow: new[]
        {
            Treatment(reasonId: 2),
            Treatment(reasonId: null),
            Treatment(reasonId: null)
        });

        var none = profile.Reasons.Single(r => r.Id == CowProfile.NoReasonId);
        Assert.Equal(2, none.Count);
        // Nothing is dropped: the bars have to add up to the treatment count.
        Assert.Equal(3, profile.Reasons.Sum(r => r.Count));
    }

    // ---- Planned ---------------------------------------------------------

    [Fact]
    public void Open_planned_counts_untreated_cow_plans_plus_all_claw_plans()
    {
        var profile = Build(
            plannedCow: new[] { PlannedCow(), PlannedCow(isTreated: true) },
            plannedClaw: new[] { PlannedClaw(), PlannedClaw() });

        // PlannedClawTreatment has no completion flag at all - Complete deletes the row - so
        // every claw plan that still exists is still open.
        Assert.Equal(3, profile.OpenPlanned);
    }

    [Fact]
    public void Planned_treatments_of_other_cows_are_not_counted()
    {
        var profile = Build(
            plannedCow: new[] { PlannedCow(cowId: Other) },
            plannedClaw: new[] { PlannedClaw(cowId: Other) });

        Assert.Equal(0, profile.OpenPlanned);
        Assert.Empty(profile.PlannedCowTreatments);
        Assert.Empty(profile.PlannedClawTreatments);
    }
}
