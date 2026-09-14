using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Die Jahresachse der Klauenbehandlungen und die Liste der noch liegenden
/// Verbaende.
///
/// Die Verbandsliste ist die Arbeitsliste im Stall: was hier fehlt, wird nicht
/// abgenommen. Das heutige Datum ist Parameter statt Uhr im Rumpf, damit die
/// Achse pruefbar bleibt und auf einem Client mit anderer Zeitzone dieselbe
/// ist.
/// </summary>
public class ClawTreatmentLookupsTests
{
    // Festes "heute" fuer jeden Test, damit die Grenzen behauptbar sind.
    private static readonly DateTime Now = new(2026, 6, 15);

    private static ClawTreatment Claw(
        DateTime date,
        bool lv = false,
        bool lh = false,
        bool rv = false,
        bool rh = false,
        bool removed = false)
        => new(0, "DE0815", date,
            null, lv, false,
            null, lh, false,
            null, rv, false,
            null, rh, false,
            removed);

    // ---- Monatswerte ------------------------------------------------------

    [Fact]
    public void Without_a_chosen_year_the_chart_follows_the_date_handed_in_and_not_the_clock()
    {
        var treatments = new[]
        {
            Claw(new DateTime(2026, 9, 1)),
            Claw(new DateTime(2026, 9, 30)),
            Claw(new DateTime(2025, 9, 15))
        };

        var chart = ClawTreatmentLookups.GetClawTreatmentChartData(treatments, Now);

        Assert.Equal(2, chart[8]);
        Assert.Equal(2, chart.Sum());
    }

    [Fact]
    public void A_chosen_year_wins_over_the_date_handed_in()
    {
        var treatments = new[] { Claw(new DateTime(2026, 9, 1)), Claw(new DateTime(2025, 9, 15)) };

        var chart = ClawTreatmentLookups.GetClawTreatmentChartData(treatments, Now, year: 2025);

        Assert.Equal(1, chart[8]);
        Assert.Equal(1, chart.Sum());
    }

    [Fact]
    public void A_year_without_treatments_stays_twelve_zeros()
    {
        var chart = ClawTreatmentLookups.GetClawTreatmentChartData(
            new[] { Claw(new DateTime(2019, 4, 1)) }, Now);

        Assert.Equal(12, chart.Length);
        Assert.All(chart, month => Assert.Equal(0, month));
    }

    // ---- Liegende Verbaende -----------------------------------------------

    [Fact]
    public void A_bandage_on_any_single_claw_is_enough_to_be_listed()
    {
        // Vier Klauen, jede einzeln - die Liste darf keine davon uebersehen.
        Assert.Single(ClawTreatmentLookups.GetClawTreatmentsWithBandage(new[] { Claw(Now, lv: true) }));
        Assert.Single(ClawTreatmentLookups.GetClawTreatmentsWithBandage(new[] { Claw(Now, lh: true) }));
        Assert.Single(ClawTreatmentLookups.GetClawTreatmentsWithBandage(new[] { Claw(Now, rv: true) }));
        Assert.Single(ClawTreatmentLookups.GetClawTreatmentsWithBandage(new[] { Claw(Now, rh: true) }));
    }

    [Fact]
    public void A_treatment_without_any_bandage_never_reaches_the_list()
    {
        Assert.Empty(ClawTreatmentLookups.GetClawTreatmentsWithBandage(new[] { Claw(Now) }));
    }

    [Fact]
    public void A_bandage_already_taken_off_drops_out_of_the_list()
    {
        Assert.Empty(ClawTreatmentLookups.GetClawTreatmentsWithBandage(
            new[] { Claw(Now, lv: true, removed: true) }));
    }

    [Fact]
    public void The_list_runs_oldest_first_so_the_longest_worn_bandage_stands_on_top()
    {
        var young = Claw(new DateTime(2026, 6, 10), lv: true);
        var old = Claw(new DateTime(2026, 5, 2), rh: true);

        var result = ClawTreatmentLookups.GetClawTreatmentsWithBandage(new[] { young, old });

        Assert.Equal(new[] { old, young }, result);
    }
}
