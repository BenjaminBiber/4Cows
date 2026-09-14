using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Die Nachschlage- und Vorschlagsregeln der Klauenbefunde.
///
/// Der Leerstring bei unbekanntem Befund ist hier die eigentliche Regel: leer
/// heisst in der ganzen Klauen-Anzeige "an dieser Klaue nichts erfasst". Ein
/// Platzhalterzeichen wuerde weitergezaehlt und stuende als haeufigster
/// Klauenbefund auf der Kachel - eine Diagnose, die niemand gestellt hat.
/// </summary>
public class ClawFindingLookupsTests
{
    private static ClawFinding F(int id, string name) => new(id, name);

    private static Dictionary<int, ClawFinding> Findings(params ClawFinding[] findings)
        => findings.ToDictionary(f => f.ClawFindingId);

    [Fact]
    public void An_unknown_finding_has_an_empty_name_and_not_a_placeholder()
    {
        Assert.Equal(string.Empty, ClawFindingLookups.GetNameById(Findings(), 7));
    }

    [Fact]
    public void A_claw_with_nothing_recorded_has_an_empty_name()
    {
        // null in der Spalte heisst "an dieser Klaue wurde nichts erfasst".
        Assert.Equal(string.Empty, ClawFindingLookups.GetNameById(Findings(F(1, "Mortellaro")), null));
    }

    [Fact]
    public void A_known_finding_is_named()
    {
        Assert.Equal("Mortellaro", ClawFindingLookups.GetNameById(Findings(F(1, "Mortellaro")), 1));
    }

    [Fact]
    public void The_empty_name_is_deliberately_different_from_the_dash_of_the_treatment_reasons()
    {
        // Beide Nachschlagedienste sehen gleich aus und beantworten den
        // Fehltreffer bewusst verschieden: ein Gedankenstrich hier wuerde in
        // der Klauenauswertung als echter Befund mitgezaehlt.
        var dash = ((char)0x2013).ToString();

        var missingFinding = ClawFindingLookups.GetNameById(Findings(), 7);
        var missingReason = TreatmentReasonLookups.GetNameById(
            new Dictionary<int, TreatmentReason>(), 7, dash);

        Assert.Equal(string.Empty, missingFinding);
        Assert.NotEqual(missingReason, missingFinding);
    }

    // ---- Autocomplete im Klauenbehandlungs-Dialog -------------------------

    [Fact]
    public void An_empty_term_lists_every_finding_alphabetically()
    {
        // Der haeufigste Fall: das Feld wird angeklickt, ohne zu tippen.
        var result = ClawFindingLookups.Search(new[] { "Sohlengeschwuer", "Ballen", "Mortellaro" }, "  ");

        Assert.Equal(new[] { "Ballen", "Mortellaro", "Sohlengeschwuer" }, result);
    }

    [Fact]
    public void A_term_without_a_hit_comes_back_as_itself_so_it_can_be_created()
    {
        // Ein neuer Befund entsteht nebenbei im Dialog. Eine leere Liste wuerde
        // diesen Weg verdecken und die Eingabe beim Speichern verschlucken.
        Assert.Equal(new[] { "Klauenrehe" }, ClawFindingLookups.Search(new[] { "Mortellaro" }, "Klauenrehe"));
    }

    [Fact]
    public void A_term_without_a_hit_comes_back_trimmed()
    {
        Assert.Equal(new[] { "Klauenrehe" }, ClawFindingLookups.Search(new[] { "Mortellaro" }, "  Klauenrehe  "));
    }

    [Fact]
    public void Matching_ignores_case_and_keeps_only_the_hits()
    {
        var result = ClawFindingLookups.Search(new[] { "Mortellaro", "Ballen" }, "MORT");

        Assert.Equal("Mortellaro", Assert.Single(result));
    }

    [Fact]
    public void Duplicate_finding_names_appear_only_once()
    {
        // Zwei gleichnamige Eintraege koennen bis zum Zusammenfuehren
        // nebeneinander stehen.
        var names = ClawFindingLookups.FindingNames(new[] { F(1, "Mortellaro"), F(2, "Mortellaro") });

        Assert.Equal("Mortellaro", Assert.Single(names));
    }
}
