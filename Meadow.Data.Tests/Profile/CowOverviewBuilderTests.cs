using BB_Cow.Class;
using BB_Cow.Profile;
using static BBCowDataLibrary.Tests.Profile.CowProfileTestData;

namespace BBCowDataLibrary.Tests.Profile;

public class CowOverviewBuilderTests
{
    [Fact]
    public void Every_cow_gets_a_row_even_without_treatments()
    {
        var rows = CowOverviewBuilder.Build(
            new[] { Cow(), Cow(Other, collar: 102) },
            Array.Empty<CowTreatment>(),
            Array.Empty<ClawTreatment>());

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(0, r.TotalTreatments));
        Assert.All(rows, r => Assert.Null(r.LastTreatment));
    }

    [Fact]
    public void Both_kinds_are_counted_separately()
    {
        var rows = CowOverviewBuilder.Build(
            new[] { Cow() },
            new[] { Treatment(), Treatment() },
            new[] { Claw(HoofPosition.LV) });

        var row = Assert.Single(rows);
        Assert.Equal(2, row.CowTreatments);
        Assert.Equal(1, row.ClawTreatments);
        Assert.Equal(3, row.TotalTreatments);
    }

    [Fact]
    public void Treatments_of_an_unknown_cow_are_dropped()
    {
        // Ear_Tag_Number holds the Cow_ID. A row pointing at an animal that no longer exists
        // cannot be shown anywhere, so it must not inflate anyone else's count either.
        var rows = CowOverviewBuilder.Build(
            new[] { Cow() },
            new[] { Treatment(cowId: "gibt es nicht") },
            Array.Empty<ClawTreatment>());

        Assert.Equal(0, Assert.Single(rows).TotalTreatments);
    }

    [Fact]
    public void The_last_treatment_is_the_newest_of_both_kinds()
    {
        var claw = CowOverviewBuilder.Build(
            new[] { Cow() },
            new[] { Treatment(date: Today.AddDays(-10)) },
            new[] { Claw(HoofPosition.LV, date: Today.AddDays(-1)) });

        var cow = CowOverviewBuilder.Build(
            new[] { Cow() },
            new[] { Treatment(date: Today.AddDays(-1)) },
            new[] { Claw(HoofPosition.LV, date: Today.AddDays(-10)) });

        Assert.Equal(Today.AddDays(-1), Assert.Single(claw).LastTreatment);
        Assert.Equal(Today.AddDays(-1), Assert.Single(cow).LastTreatment);
    }

    [Fact]
    public void Collar_numbers_shared_by_a_gone_and_a_present_cow_stay_separate()
    {
        // IsCollarInUse only reserves the numbers of cows still in the herd, so 142 really can
        // belong to two Cow rows. Grouping by collar would merge two animals' histories.
        var rows = CowOverviewBuilder.Build(
            new[] { Cow(collar: 142, isGone: true), Cow(Other, collar: 142) },
            new[] { Treatment(), Treatment(cowId: Other), Treatment(cowId: Other) },
            Array.Empty<ClawTreatment>());

        Assert.Equal(1, rows.Single(r => r.CowId == Mine).CowTreatments);
        Assert.Equal(2, rows.Single(r => r.CowId == Other).CowTreatments);
    }

    [Fact]
    public void A_calf_has_no_ear_tag_but_still_has_a_row()
    {
        var calf = BB_Cow.Class.Cow.CreateCalf(203);

        var row = Assert.Single(CowOverviewBuilder.Build(
            new[] { calf },
            new[] { Treatment(cowId: calf.CowId) },
            Array.Empty<ClawTreatment>()));

        Assert.True(row.IsCalf);
        Assert.Null(row.EarTagNumber);
        Assert.Equal(203, row.CollarNumber);
        Assert.Equal(1, row.CowTreatments);
    }
}
