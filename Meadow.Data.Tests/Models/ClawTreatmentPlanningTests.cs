using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Models;

/// <summary>
/// Die Auswahl der Sammelplanung (<see cref="ClawTreatmentPlanning.SelectCowsForPlanning"/>).
/// </summary>
public class ClawTreatmentPlanningTests
{
    private static readonly DateTime Today = new(2026, 6, 15);
    private const int Days = 30;

    private static ClawTreatment Claw(string cowId, DateTime date)
        => new(0, cowId, date,
            null, false, false,
            null, false, false,
            null, false, false,
            null, false, false,
            false);

    private static IReadOnlyList<string> Select(
        IEnumerable<Cow> cows,
        IEnumerable<ClawTreatment>? treatments = null,
        IEnumerable<PlannedClawTreatment>? planned = null,
        bool onlyActive = true)
        => ClawTreatmentPlanning.SelectCowsForPlanning(
                cows, treatments ?? [], planned ?? [], Days, onlyActive, Today)
            .Select(c => c.CowId)
            .ToList();

    [Fact]
    public void A_cow_never_treated_is_selected()
    {
        Assert.Equal(["DE1"], Select([new Cow("DE1", 1, false)]));
    }

    [Fact]
    public void Exactly_the_day_limit_counts_as_due()
    {
        var cows = new[] { new Cow("DE1", 1, false) };

        Assert.Equal(["DE1"], Select(cows, [Claw("DE1", Today.AddDays(-Days).AddHours(15))]));
        Assert.Empty(Select(cows, [Claw("DE1", Today.AddDays(-Days + 1))]));
    }

    [Fact]
    public void Only_the_latest_treatment_counts()
    {
        var cows = new[] { new Cow("DE1", 1, false) };
        var treatments = new[] { Claw("DE1", Today.AddDays(-100)), Claw("DE1", Today.AddDays(-3)) };

        Assert.Empty(Select(cows, treatments));
    }

    [Fact]
    public void Gone_cows_are_skipped_only_when_only_active_is_set()
    {
        var cows = new[] { new Cow("DE1", 1, true) };

        Assert.Empty(Select(cows, onlyActive: true));
        Assert.Equal(["DE1"], Select(cows, onlyActive: false));
    }

    [Fact]
    public void Calves_are_never_selected()
    {
        Assert.Empty(Select([Cow.CreateCalf(5)], onlyActive: false));
    }

    [Fact]
    public void A_cow_already_planned_is_skipped()
    {
        var planned = new PlannedClawTreatment(1, "DE1", Today.AddDays(5), "x", false, false, false, false);

        Assert.Equal(["DE2"], Select([new Cow("DE1", 1, false), new Cow("DE2", 2, false)], planned: [planned]));
    }

    [Fact]
    public void Result_is_ordered_by_collar_number()
    {
        Assert.Equal(["DE1", "DE2"], Select([new Cow("DE2", 9, false), new Cow("DE1", 3, false)]));
    }
}
