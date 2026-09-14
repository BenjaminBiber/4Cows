using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Die Fehltrefferregeln des Medikamenten-Cache.
///
/// Die zwei Bindestriche sind das, was in der Behandlungstabelle steht, wenn
/// ein Praeparat nicht mehr da ist - oder wenn der Client seinen Cache nicht
/// nachgezogen hat. Beides sieht gleich aus, und genau deshalb muss das
/// Zeichen eines bleiben: ein leeres Feld liesse die Zeile wie eine Behandlung
/// ohne Medikament aussehen, und eine Zahl waere gar nicht als Fehlen zu
/// erkennen.
/// </summary>
public class MedicineLookupsTests
{
    private static Medicine M(int id, string name, string? unit = null)
        => new(id, name) { DosageUnit = unit };

    private static Dictionary<int, Medicine> Cabinet(params Medicine[] medicines)
        => medicines.ToDictionary(m => m.MedicineId);

    [Fact]
    public void An_unknown_medicine_is_shown_as_two_hyphens()
    {
        Assert.Equal("--", MedicineLookups.GetMedicineNameById(Cabinet(), 7));
    }

    [Fact]
    public void A_known_medicine_is_shown_with_its_name()
    {
        Assert.Equal("Metacam", MedicineLookups.GetMedicineNameById(Cabinet(M(7, "Metacam")), 7));
    }

    [Fact]
    public void An_unknown_medicine_has_no_record_at_all()
    {
        // Kein Ersatzobjekt wie beim Euterviertel: die Aufrufstellen pruefen
        // hier selbst auf null, bevor sie Felder lesen.
        Assert.Null(MedicineLookups.GetById(Cabinet(), 7));
    }

    // ---- Dosiereinheit ----------------------------------------------------

    [Fact]
    public void A_medicine_without_a_dosage_unit_keeps_the_unit_the_caller_brought()
    {
        // Der Rueckfallwert bleibt Sache des Aufrufers, damit die Tabellen
        // unveraendert ihr bisheriges "ml" anzeigen.
        Assert.Equal("ml", MedicineLookups.GetDosageUnit(Cabinet(M(7, "Metacam")), 7, "ml"));
    }

    [Fact]
    public void A_dosage_unit_of_blanks_counts_as_none()
    {
        Assert.Equal("ml", MedicineLookups.GetDosageUnit(Cabinet(M(7, "Metacam", "   ")), 7, "ml"));
    }

    [Fact]
    public void A_stored_dosage_unit_wins_over_the_fallback()
    {
        // Der Grund fuer die Einheit am Praeparat: eine Tablette darf nicht
        // als Milliliter in der Tabelle stehen.
        Assert.Equal("Stueck", MedicineLookups.GetDosageUnit(Cabinet(M(7, "Bolus", "Stueck")), 7, "ml"));
    }

    [Fact]
    public void An_unknown_medicine_also_falls_back_to_the_unit_of_the_caller()
    {
        Assert.Equal("ml", MedicineLookups.GetDosageUnit(Cabinet(), 7, "ml"));
    }

    // ---- Namenslisten -----------------------------------------------------

    [Fact]
    public void Names_for_a_list_of_ids_drop_the_ids_the_cache_does_not_know()
    {
        // Ein geloeschtes Praeparat soll die Liste kuerzen, nicht mit
        // Platzhaltern auffuellen.
        var cabinet = Cabinet(M(1, "Metacam"), M(2, "Ubrolexin"));

        var names = MedicineLookups.GetMedicineNamesByIds(cabinet, new List<int> { 1, 99 });

        Assert.Equal(new[] { "Metacam" }, names);
    }

    [Fact]
    public void A_repeated_id_yields_its_name_only_once()
    {
        // Zweimal derselbe Text in einer Zelle waere nicht auseinanderzuhalten.
        var cabinet = Cabinet(M(1, "Metacam"));

        Assert.Equal(new[] { "Metacam" },
            MedicineLookups.GetMedicineNamesByIds(cabinet, new List<int> { 1, 1 }));
    }
}
