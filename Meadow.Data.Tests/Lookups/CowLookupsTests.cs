using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Die Anzeige- und Fehltrefferregeln rund um den Kuh-Cache.
///
/// Sie zaehlen, weil keine von ihnen einen Fehler erzeugt, wenn sie
/// auseinanderdriftet. Ein Kalb stuende mit seiner GUID in der
/// Ohrmarkenspalte, eine Behandlung haenge an der falschen Halsbandnummer,
/// eine Tabelle waere leer statt falsch - im Stall ist nichts davon als
/// Fehler zu erkennen, es sieht nur aus wie ein anderer Tag.
/// </summary>
public class CowLookupsTests
{
    // Eine identifizierte Kuh: die Ohrmarke ist zugleich ihre Cow_ID.
    private static Cow Tagged(string earTag, int collar, bool isGone = false)
        => new(earTag, collar, isGone);

    // Ein Kalb: GUID als Cow_ID, noch keine Ohrmarke.
    private static Cow Calf(string cowId, int collar, bool isGone = false)
        => new(cowId, null, collar, isCalv: true, isGone: isGone);

    // Ein Kalb, das eine Ohrmarke bekommen hat und seine GUID behaelt.
    private static Cow Promoted(string cowId, string earTag, int collar)
        => new(cowId, earTag, collar, isCalv: false, isGone: false);

    private static Dictionary<string, Cow> Herd(params Cow[] cows)
        => cows.ToDictionary(c => c.CowId, StringComparer.Ordinal);

    // ---- Ohrmarkenspalte -------------------------------------------------

    [Fact]
    public void An_unknown_cow_id_is_shown_as_Kalb_and_never_as_the_raw_id()
    {
        // Der Fall eines Caches, der die Kuh nicht kennt: in der Spalte
        // stuende sonst eine GUID, die im Stall niemandem etwas sagt.
        Assert.Equal("Kalb", CowLookups.GetEarTagDisplay(Herd(), "guid-1"));
    }

    [Fact]
    public void A_cow_without_an_ear_tag_is_shown_as_Kalb()
    {
        var herd = Herd(Calf("guid-1", 12));

        Assert.Equal("Kalb", CowLookups.GetEarTagDisplay(herd, "guid-1"));
    }

    [Fact]
    public void An_ear_tag_of_blanks_counts_as_none()
    {
        // Aus der Basisdatenpflege kann ein Leerzeichen in der Spalte stehen.
        var herd = Herd(new Cow("guid-1", "   ", 12, isCalv: false, isGone: false));

        Assert.Equal("Kalb", CowLookups.GetEarTagDisplay(herd, "guid-1"));
    }

    [Fact]
    public void An_empty_cow_id_is_shown_as_Kalb()
    {
        Assert.Equal("Kalb", CowLookups.GetEarTagDisplay(Herd(), string.Empty));
    }

    [Fact]
    public void An_identified_cow_is_shown_with_her_ear_tag()
    {
        var herd = Herd(Tagged("DE0815", 12));

        Assert.Equal("DE0815", CowLookups.GetEarTagDisplay(herd, "DE0815"));
    }

    // ---- Label in den Behandlungsdialogen ---------------------------------

    [Fact]
    public void The_label_of_a_calf_names_the_collar_number_because_it_has_no_other_mark()
    {
        var herd = Herd(Calf("guid-1", 42));

        Assert.Equal("Kalb (42)", CowLookups.GetDisplayLabel(herd, "guid-1"));
    }

    [Fact]
    public void The_label_of_a_cow_without_an_ear_tag_also_names_the_collar_number()
    {
        // Nicht als Kalb gefuehrt, aber ohne Ohrmarke - die Halsbandnummer
        // bleibt auch hier das einzige Merkmal.
        var herd = Herd(new Cow("guid-1", "", 7, isCalv: false, isGone: false));

        Assert.Equal("Kalb (7)", CowLookups.GetDisplayLabel(herd, "guid-1"));
    }

    [Fact]
    public void The_label_of_an_identified_cow_is_her_ear_tag_alone()
    {
        // Die Halsbandnummer steht im Dialog in ihrem eigenen Feld daneben
        // und wird deshalb nicht wiederholt.
        var herd = Herd(Tagged("DE0815", 12));

        Assert.Equal("DE0815", CowLookups.GetDisplayLabel(herd, "DE0815"));
    }

    [Fact]
    public void The_label_of_an_unknown_id_is_the_id_itself()
    {
        // Anders als die Ohrmarkenspalte: hier steht der Rohwert, damit im
        // Dialog ueberhaupt etwas zu sehen ist.
        Assert.Equal("guid-1", CowLookups.GetDisplayLabel(Herd(), "guid-1"));
    }

    [Fact]
    public void The_label_of_null_is_empty()
    {
        Assert.Equal(string.Empty, CowLookups.GetDisplayLabel(Herd(), null));
    }

    // ---- Halsbandnummer ---------------------------------------------------

    [Fact]
    public void A_cow_the_cache_does_not_know_has_the_collar_number_int_MinValue()
    {
        // Nicht 0: die Null ist eine gueltige Halsbandnummer, der Fehltreffer
        // muss davon unterscheidbar bleiben.
        Assert.Equal(int.MinValue, CowLookups.GetCollarNumberByCowId(Herd(), "guid-1"));
    }

    [Fact]
    public void An_empty_cow_id_also_yields_int_MinValue()
    {
        Assert.Equal(int.MinValue, CowLookups.GetCollarNumberByCowId(Herd(), string.Empty));
    }

    [Fact]
    public void A_known_cow_yields_her_collar_number()
    {
        var herd = Herd(Tagged("DE0815", 12));

        Assert.Equal(12, CowLookups.GetCollarNumberByCowId(herd, "DE0815"));
    }

    // ---- Filter der Tabellen ----------------------------------------------

    [Fact]
    public void The_filter_drops_every_row_while_the_cow_cache_is_still_cold()
    {
        // Die teuerste Regel des Projekts: zwei Seiten hatten den Kuh-Cache
        // nicht geladen, und der Filter raeumte beim Direktaufruf die ganze
        // Tabelle leer, statt falsche Zeilen zu zeigen. Der Kommentar in
        // MeadowDataLoader beschreibt genau diesen Vorfall.
        Assert.False(CowLookups.FilterFuncCow(Herd(), "guid-1", string.Empty));
        Assert.False(CowLookups.FilterFuncCow(Herd(), "guid-1", "DE"));
    }

    [Fact]
    public void A_known_cow_passes_the_filter_when_nothing_is_typed()
    {
        var herd = Herd(Tagged("DE0815", 12));

        Assert.True(CowLookups.FilterFuncCow(herd, "DE0815", "   "));
    }

    [Fact]
    public void A_short_term_has_to_be_the_whole_collar_number()
    {
        // Bei ein bis zwei Zeichen wuerde die Ohrmarkensuche halbe Tabellen
        // stehen lassen - deshalb zaehlt hier nur die genaue Halsbandnummer.
        var herd = Herd(Tagged("DE0012", 12));

        Assert.True(CowLookups.FilterFuncCow(herd, "DE0012", "12"));
        Assert.False(CowLookups.FilterFuncCow(herd, "DE0012", "00"));
    }

    [Fact]
    public void From_three_characters_on_the_term_may_sit_anywhere_in_the_ear_tag()
    {
        var herd = Herd(Tagged("DE0012", 12));

        Assert.True(CowLookups.FilterFuncCow(herd, "DE0012", "001"));
    }

    [Fact]
    public void The_collar_number_keeps_matching_exactly_even_for_longer_terms()
    {
        // Vier Ziffern treffen die Halsbandnummer nur ganz, nie zur Haelfte.
        var herd = Herd(Tagged("DE0815", 1234));

        Assert.True(CowLookups.FilterFuncCow(herd, "DE0815", "1234"));
        Assert.False(CowLookups.FilterFuncCow(herd, "DE0815", "123"));
    }

    [Fact]
    public void A_calf_without_an_ear_tag_still_passes_by_its_collar_number()
    {
        var herd = Herd(Calf("guid-1", 42));

        Assert.True(CowLookups.FilterFuncCow(herd, "guid-1", "42"));
    }

    // ---- Nachschlagen ueber Ohrmarke und Halsband -------------------------

    [Fact]
    public void A_promoted_calf_is_found_by_ear_tag_but_not_under_it_as_a_key()
    {
        // Ein Kalb behaelt seine GUID, wenn es die Ohrmarke bekommt, damit die
        // Behandlungshistorie haengen bleibt. Deshalb geht die Ohrmarkensuche
        // ueber das Feld und nicht ueber den Schluessel.
        var promoted = Promoted("guid-5", "DE0005", 5);
        var herd = Herd(promoted);

        Assert.Same(promoted, CowLookups.GetByEarTagNumber(herd, "DE0005"));
        Assert.Null(CowLookups.GetById(herd, "DE0005"));
        Assert.Same(promoted, CowLookups.GetById(herd, "guid-5"));
    }

    [Fact]
    public void An_empty_ear_tag_number_finds_nobody()
    {
        // Sonst faende die Suche das erste beste Kalb, dessen Ohrmarke leer ist.
        var herd = Herd(Calf("guid-1", 42));

        Assert.Null(CowLookups.GetByEarTagNumber(herd, "  "));
    }

    [Fact]
    public void A_cow_that_left_the_farm_still_answers_the_ear_tag_lookup_by_default()
    {
        // Alte Behandlungen muessen weiter eine Ohrmarke anzeigen koennen.
        var herd = Herd(Tagged("DE0009", 9, isGone: true));

        Assert.Equal("DE0009", CowLookups.GetEarTagNumberByCollarNumber(herd, 9));
        Assert.Equal(string.Empty,
            CowLookups.GetEarTagNumberByCollarNumber(herd, 9, searchContainsLeavage: false));
    }

    [Fact]
    public void Resolving_a_collar_to_a_cow_id_skips_cows_that_left_by_default()
    {
        // Umgekehrte Voreinstellung als bei der Ohrmarkensuche daneben: ein
        // neuer Eintrag darf nicht an einer abgegangenen Kuh haengen.
        var herd = Herd(Tagged("DE0009", 9, isGone: true));

        Assert.Equal(string.Empty, CowLookups.GetCowIdByCollarNumber(herd, 9));
        Assert.Equal("DE0009", CowLookups.GetCowIdByCollarNumber(herd, 9, includeGone: true));
    }

    [Fact]
    public void A_collar_without_a_cow_resolves_to_an_empty_cow_id()
    {
        Assert.Equal(string.Empty, CowLookups.GetCowIdByCollarNumber(Herd(), 3));
    }

    [Fact]
    public void Only_a_calf_that_still_has_no_ear_tag_answers_the_calf_lookup()
    {
        // Sobald die Ohrmarke da ist, ist es im Dialog keine Kalb-Zeile mehr.
        var promoted = Promoted("guid-5", "DE0005", 5);
        Assert.Null(CowLookups.GetCalfByCollarNumber(Herd(promoted), 5));

        var calf = Calf("guid-6", 6);
        Assert.Same(calf, CowLookups.GetCalfByCollarNumber(Herd(calf), 6));
    }

    [Fact]
    public void A_collar_number_is_free_again_once_its_cow_has_left()
    {
        var gone = Herd(Tagged("DE0004", 4, isGone: true));

        Assert.False(CowLookups.IsCollarInUse(gone, 4));
        Assert.True(CowLookups.IsCollarInUse(Herd(Tagged("DE0004", 4)), 4));
    }

    // ---- Autocomplete der Behandlungsdialoge ------------------------------

    [Fact]
    public void The_search_skips_cows_that_left_and_orders_by_collar_number()
    {
        var herd = Herd(
            Tagged("DE0003", 30),
            Tagged("DE0001", 10),
            Tagged("DE0002", 20, isGone: true));

        Assert.Equal(new[] { "DE0001", "DE0003" }, CowLookups.SearchCows(herd, string.Empty).ToList());
    }

    [Fact]
    public void The_search_also_matches_the_cow_id_so_a_calf_is_findable_without_an_ear_tag()
    {
        var herd = Herd(Calf("guid-77", 77), Tagged("DE0001", 10));

        Assert.Equal(new[] { "guid-77" }, CowLookups.SearchCows(herd, "guid").ToList());
    }

    [Fact]
    public void The_search_returns_cow_ids_because_that_is_what_a_treatment_stores()
    {
        // Getippt wird die Ohrmarke, gespeichert wird die Cow_ID.
        var herd = Herd(Promoted("guid-5", "DE0005", 5));

        Assert.Equal(new[] { "guid-5" }, CowLookups.SearchCows(herd, "DE0005").ToList());
    }
}
