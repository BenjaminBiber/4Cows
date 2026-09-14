using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Die Jahresachse und die Vorschlagslisten der Kuhbehandlungen.
///
/// Die Achse zaehlt doppelt: sie las bisher die Uhr des Servers und nimmt das
/// heutige Datum jetzt als Parameter. Damit ist die Rechnung ueberhaupt erst
/// pruefbar - und auf einem Client in einer anderen Zeitzone dieselbe. Der
/// leere Jahrgang ist der zweite Punkt: er riss nach der Erstinstallation den
/// Circuit ab, weil Min() ueber nichts wirft.
/// </summary>
public class CowTreatmentLookupsTests
{
    // Festes "heute" fuer jeden Test, damit die Grenzen behauptbar sind.
    private static readonly DateTime Now = new(2026, 6, 15);

    private static CowTreatment T(DateTime date, int medicineId = 1)
        => new(0, "DE0815", medicineId, date, 1.0f, 0, int.MinValue);

    // ---- Jahresachse ------------------------------------------------------

    [Fact]
    public void The_year_axis_of_an_empty_treatment_list_is_the_year_handed_in()
    {
        // Genau der Zustand direkt nach der Erstinstallation: der
        // ChartDateDialog fragt nach dem aeltesten Jahr, es gibt noch keine
        // Behandlung, und Min() ueber nichts riss die Seite ab.
        Assert.Equal(2026, CowTreatmentLookups.GetMinYear(Array.Empty<CowTreatment>(), Now));
    }

    [Fact]
    public void The_year_axis_starts_at_the_oldest_treatment()
    {
        var treatments = new[] { T(new DateTime(2024, 11, 2)), T(new DateTime(2026, 1, 9)) };

        Assert.Equal(2024, CowTreatmentLookups.GetMinYear(treatments, Now));
    }

    // ---- Monatswerte ------------------------------------------------------

    [Fact]
    public void Without_a_chosen_year_the_chart_follows_the_date_handed_in_and_not_the_clock()
    {
        var treatments = new[]
        {
            T(new DateTime(2026, 3, 4)),
            T(new DateTime(2026, 3, 20)),
            T(new DateTime(2025, 3, 10))
        };

        var chart = CowTreatmentLookups.GetCowTreatmentChartData(treatments, Now);

        Assert.Equal(2, chart[2]);
        Assert.Equal(2, chart.Sum());
    }

    [Fact]
    public void A_chosen_year_wins_over_the_date_handed_in()
    {
        var treatments = new[] { T(new DateTime(2026, 3, 4)), T(new DateTime(2025, 3, 10)) };

        var chart = CowTreatmentLookups.GetCowTreatmentChartData(treatments, Now, year: 2025);

        Assert.Equal(1, chart[2]);
        Assert.Equal(1, chart.Sum());
    }

    [Fact]
    public void Every_month_keeps_its_slot_even_without_a_single_treatment()
    {
        // Zwoelf Werte, damit die Achse nicht je nach Datenlage verrutscht.
        var chart = CowTreatmentLookups.GetCowTreatmentChartData(Array.Empty<CowTreatment>(), Now);

        Assert.Equal(12, chart.Length);
        Assert.All(chart, month => Assert.Equal(0, month));
    }

    [Fact]
    public void The_medicine_chart_counts_only_the_chosen_preparation()
    {
        var treatments = new[]
        {
            T(new DateTime(2026, 1, 5), medicineId: 1),
            T(new DateTime(2026, 1, 6), medicineId: 2)
        };

        var chart = CowTreatmentLookups.GetCowTreatmentMedicineChartData(treatments, Now, medicine: 1);

        Assert.Equal(1, chart[0]);
        Assert.Equal(1, chart.Sum());
    }

    [Fact]
    public void The_medicine_chart_also_follows_the_date_handed_in()
    {
        var treatments = new[] { T(new DateTime(2025, 1, 5)), T(new DateTime(2026, 1, 5)) };

        Assert.Equal(1, CowTreatmentLookups.GetCowTreatmentMedicineChartData(treatments, Now, medicine: 1).Sum());
        Assert.Equal(1, CowTreatmentLookups
            .GetCowTreatmentMedicineChartData(treatments, Now, medicine: 1, year: 2025).Sum());
    }

    // ---- Autocomplete "Wie / Wo" ------------------------------------------

    [Fact]
    public void An_empty_term_lists_every_where_how_in_the_order_it_arrived()
    {
        // Anders als die Gruende und die Klauenbefunde wird hier NICHT
        // alphabetisch sortiert - festgehalten, wie es heute ist.
        var names = new[] { "Muskel", "Euter" };

        Assert.Equal(names, CowTreatmentLookups.SearchCowTreatmentWhereHow(names, ""));
    }

    [Fact]
    public void A_term_without_a_hit_comes_back_trimmed_so_it_can_be_created()
    {
        Assert.Equal(new[] { "Klaue" },
            CowTreatmentLookups.SearchCowTreatmentWhereHow(new[] { "Euter" }, "  Klaue "));
    }

    [Fact]
    public void Matching_ignores_case_and_keeps_only_the_hits()
    {
        var result = CowTreatmentLookups.SearchCowTreatmentWhereHow(new[] { "Euter", "Muskel" }, "EUT");

        Assert.Equal("Euter", Assert.Single(result));
    }

    [Fact]
    public void On_a_fresh_installation_the_where_how_search_answers_with_nothing()
    {
        // Heutiges Verhalten, festgehalten wie es ist: bei noch leerer
        // Wie/Wo-Liste kommt die Eingabe NICHT zurueck - anders als bei den
        // Behandlungsgruenden und den Klauenbefunden, wo genau das der Weg
        // ist, einen neuen Eintrag nebenbei anzulegen.
        Assert.Empty(CowTreatmentLookups.SearchCowTreatmentWhereHow(Array.Empty<string>(), "Euter"));
    }
}
