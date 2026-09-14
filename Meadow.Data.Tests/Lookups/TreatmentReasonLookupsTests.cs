using Meadow.Data.Services;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Die Nachschlage- und Vorschlagsregeln der Behandlungsgruende.
///
/// Der Platzhalter kommt hier als Parameter herein und steht nicht als zweite
/// Konstante im Modul - genau das ist der Punkt: das Zeichen, das in der
/// Behandlungstabelle fuer "kein Grund angegeben" steht, gibt es im Projekt
/// nur einmal. Zwei Kopien waeren zwei Zeichen, und im Stall saehe dieselbe
/// Zeile auf Server und Client verschieden aus.
/// </summary>
public class TreatmentReasonLookupsTests
{
    // Gedankenstrich U+2013, nicht der Bindestrich. Als Zeichencode notiert,
    // damit der Unterschied nicht an der Zeichensatzwahl dieser Datei haengt.
    private static readonly string NoReason = ((char)0x2013).ToString();

    private static TreatmentReason R(int id, string name) => new(id, name);

    private static Dictionary<int, TreatmentReason> Reasons(params TreatmentReason[] reasons)
        => reasons.ToDictionary(r => r.TreatmentReasonId);

    [Fact]
    public void An_unknown_reason_is_shown_as_the_placeholder_the_caller_passed()
    {
        Assert.Equal(NoReason, TreatmentReasonLookups.GetNameById(Reasons(), 7, NoReason));
    }

    [Fact]
    public void A_treatment_without_a_reason_is_shown_as_the_placeholder()
    {
        // null ist hier kein Fehler, sondern ein regulaerer Dauerzustand:
        // die Migration hat jede Bestandszeile so bekommen.
        Assert.Equal(NoReason, TreatmentReasonLookups.GetNameById(Reasons(R(1, "Mastitis")), null, NoReason));
    }

    [Fact]
    public void A_known_reason_is_named()
    {
        Assert.Equal("Mastitis", TreatmentReasonLookups.GetNameById(Reasons(R(1, "Mastitis")), 1, NoReason));
    }

    [Fact]
    public void The_placeholder_belongs_to_the_caller_and_is_not_a_second_constant_here()
    {
        // Die Regel, die den spaeteren HTTP-Dienst moeglich macht: das Modul
        // kennt den Text nicht, es reicht ihn nur durch.
        Assert.Equal("ohne Grund", TreatmentReasonLookups.GetNameById(Reasons(), 7, "ohne Grund"));
    }

    [Fact]
    public void The_placeholder_the_app_hands_in_is_the_dash_and_not_the_two_hyphens_of_the_medicines()
    {
        // Gelesen wird nur eine Konstante - kein Dienst, keine Datenbank. Die
        // Behandlungstabelle zeigt beide Zeichen nebeneinander in derselben
        // Zeile, und sie duerfen sich nicht angleichen.
        Assert.Equal(TreatmentReasonService.NoReasonText, NoReason);
        Assert.NotEqual(TreatmentReasonService.NoReasonText,
            MedicineLookups.GetMedicineNameById(new Dictionary<int, Medicine>(), 7));
    }

    // ---- Autocomplete im Behandlungsdialog --------------------------------

    [Fact]
    public void An_empty_term_lists_every_reason_alphabetically()
    {
        var result = TreatmentReasonLookups.Search(new[] { "Mastitis", "Kolik", "Nachgeburt" }, "");

        Assert.Equal(new[] { "Kolik", "Mastitis", "Nachgeburt" }, result);
    }

    [Fact]
    public void A_term_without_a_hit_comes_back_trimmed_so_it_can_be_created()
    {
        // Ein neuer Grund entsteht nebenbei im Dialog und wird beim Speichern
        // angelegt. Eine leere Liste wuerde die Eingabe verschlucken.
        Assert.Equal(new[] { "Kolik" }, TreatmentReasonLookups.Search(new[] { "Mastitis" }, "  Kolik "));
    }

    [Fact]
    public void Matching_ignores_case_and_keeps_only_the_hits()
    {
        var result = TreatmentReasonLookups.Search(new[] { "Mastitis", "Kolik" }, "MAST");

        Assert.Equal("Mastitis", Assert.Single(result));
    }

    [Fact]
    public void Duplicate_reason_names_appear_only_once()
    {
        var names = TreatmentReasonLookups.ReasonNames(new[] { R(1, "Mastitis"), R(2, "Mastitis") });

        Assert.Equal("Mastitis", Assert.Single(names));
    }
}
