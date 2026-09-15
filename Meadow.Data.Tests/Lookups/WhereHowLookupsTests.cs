using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Die Anzeigeregeln fuer "Wie / Wo" samt angehaengten Eutervierteln.
///
/// Der zusammengesetzte Text steht so in der Behandlungstabelle und im
/// Kuhprofil. Die Klammerschreibweise gibt es genau einmal - waeren es zwei
/// Kopien, stuende auf dem Server "(LV/ RH)" und auf dem Client etwas anderes,
/// ohne dass irgendwo ein Fehler auftritt.
/// </summary>
public class WhereHowLookupsTests
{
    private static WhereHow W(int id, string name) => new(id, name, showDialog: true);

    private static Dictionary<int, WhereHow> WhereHows(params WhereHow[] whereHows)
        => whereHows.ToDictionary(w => w.WhereHowId);

    private static Udder U(int id, bool lv = false, bool lh = false, bool rv = false, bool rh = false)
        => new(id, lv, lh, rv, rh);

    private static Dictionary<int, Udder> Udders(params Udder[] udders)
        => udders.ToDictionary(u => u.UdderId);

    // ---- Fehltreffer ------------------------------------------------------

    [Fact]
    public void An_unknown_where_how_has_an_empty_name_and_no_placeholder()
    {
        // Der Leerstring ist es, den GetWhereHowNamesByIds unten heraussiebt.
        // Ein Platzhalterzeichen kaeme dadurch in die Liste statt heraus.
        Assert.Equal(string.Empty, WhereHowLookups.GetWhereHowNameById(WhereHows(), 7));
    }

    [Fact]
    public void An_unknown_where_how_yields_an_empty_record_and_never_null()
    {
        // Die Aufrufstellen lesen den Namen direkt.
        var whereHow = WhereHowLookups.GetById(WhereHows(), 7);

        Assert.NotNull(whereHow);
        Assert.Equal(string.Empty, whereHow.WhereHowName);
    }

    [Fact]
    public void Names_for_a_list_of_ids_drop_the_ones_the_cache_does_not_know()
    {
        var whereHows = WhereHows(W(1, "Euter"), W(2, "Muskel"));

        var names = WhereHowLookups.GetWhereHowNamesByIds(whereHows, new List<int> { 1, 99, 2 });

        Assert.Equal(new[] { "Euter", "Muskel" }, names);
    }

    [Fact]
    public void A_name_asked_for_twice_appears_only_once()
    {
        var whereHows = WhereHows(W(1, "Euter"));

        Assert.Equal(new[] { "Euter" },
            WhereHowLookups.GetWhereHowNamesByIds(whereHows, new List<int> { 1, 1 }));
    }

    [Fact]
    public void Duplicate_where_how_names_appear_only_once()
    {
        var names = WhereHowLookups.WhereHowNames(new[] { W(1, "Euter"), W(2, "Euter") });

        Assert.Equal("Euter", Assert.Single(names));
    }

    // ---- Viertel in Klammern ----------------------------------------------

    [Fact]
    public void Without_an_udder_the_full_name_stays_the_plain_name()
    {
        var whereHows = WhereHows(W(1, "Muskel"));

        Assert.Equal("Muskel", WhereHowLookups.GetFullWhereHowName(whereHows, Udders(), 1));
    }

    [Fact]
    public void All_four_quarters_are_written_as_one_short_word()
    {
        // Vier einzelne Kuerzel waeren in der Tabellenzelle laenger als die
        // Zelle breit ist.
        Assert.Equal("(Alle 4)", WhereHowLookups.GetUdderString(U(1, lv: true, lh: true, rv: true, rh: true)));
    }

    [Fact]
    public void Two_quarters_keep_the_fixed_order_LV_LH_RV_RH()
    {
        // Die Reihenfolge haengt am Modul, nicht an der Eingabe im Dialog -
        // sonst stuende dieselbe Behandlung mal als LV, mal als RH zuerst da.
        Assert.Equal("(LV/ RH)", WhereHowLookups.GetUdderString(U(1, lv: true, rh: true)));
        Assert.Equal("(LH/ RV)", WhereHowLookups.GetUdderString(U(2, lh: true, rv: true)));
    }

    [Fact]
    public void An_udder_without_a_quarter_gets_no_brackets_at_all()
    {
        Assert.Equal(string.Empty, WhereHowLookups.GetUdderString(U(1)));
    }

    [Fact]
    public void A_chosen_quarter_is_appended_to_the_name()
    {
        var whereHows = WhereHows(W(1, "Euter"));
        var udders = Udders(U(5, lv: true));

        Assert.Equal("Euter (LV)", WhereHowLookups.GetFullWhereHowName(whereHows, udders, 1, 5));
    }

    [Fact]
    public void An_unknown_udder_leaves_a_trailing_blank_behind_the_name()
    {
        // Heutiges Verhalten, festgehalten wie es ist: die Klammer faellt weg,
        // das Leerzeichen davor bleibt stehen.
        var whereHows = WhereHows(W(1, "Euter"));

        Assert.Equal("Euter ", WhereHowLookups.GetFullWhereHowName(whereHows, Udders(), 1, 99));
    }

    [Fact]
    public void An_unknown_where_how_with_a_quarter_leaves_a_leading_blank()
    {
        // Dieselbe Stelle von der anderen Seite: fehlt der Name, beginnt die
        // Zelle mit einem Leerzeichen.
        var udders = Udders(U(5, rh: true));

        Assert.Equal(" (RH)", WhereHowLookups.GetFullWhereHowName(WhereHows(), udders, 99, 5));
    }

    // ---- Id zum Namen, und der Fehlwert dahinter --------------------------
    //
    // Diese vier Tests halten einen Fehler fest, der in Produktion stand: die
    // EF-Fassung von GetWhereHowIDByName schlug mit
    // "(... ?? new WhereHow()).WhereHowId" nach. Der parameterlose Konstruktor
    // von WhereHow setzt this(0, "", true) - ein Fehltreffer kam also als 0
    // heraus, nicht als int.MinValue. Die HTTP-Fassung lieferte int.MinValue.
    //
    // Gepruefft wird an den Aufrufstellen auf int.MinValue. Die 0 rutschte
    // durch und wurde als Wie/Wo-Fremdschluessel an einer Behandlung
    // gespeichert, obwohl es keinen WhereHow mit der Id 0 gibt.

    [Fact]
    public void A_known_name_resolves_to_its_id()
    {
        var whereHows = WhereHows(W(3, "s.c."), W(7, "i.m."));

        Assert.Equal(7, WhereHowLookups.FindIdByName(whereHows, "i.m."));
    }

    [Fact]
    public void An_unknown_name_is_int_MinValue_and_never_zero()
    {
        var whereHows = WhereHows(W(3, "s.c."));

        var id = WhereHowLookups.FindIdByName(whereHows, "gibt es nicht");

        Assert.Equal(int.MinValue, id);
        // Die eigentliche Aussage. 0 waere ein gueltig aussehender
        // Fremdschluessel auf eine Zeile, die es nicht gibt.
        Assert.NotEqual(0, id);
    }

    [Theory]
    [InlineData("  i.m.  ")]
    [InlineData("I.M.")]
    [InlineData("i.M.")]
    public void The_name_is_matched_trimmed_and_case_insensitively(string eingabe)
    {
        var whereHows = WhereHows(W(7, "i.m."));

        Assert.Equal(7, WhereHowLookups.FindIdByName(whereHows, eingabe));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_name_is_int_MinValue_and_not_the_first_row(string? eingabe)
    {
        var whereHows = WhereHows(W(3, "s.c."), W(7, "i.m."));

        Assert.Equal(int.MinValue, WhereHowLookups.FindIdByName(whereHows, eingabe));
    }

    [Fact]
    public void The_empty_where_how_keeps_the_id_that_caused_the_bug()
    {
        // Kein Wunschdenken, sondern die Dokumentation der Asymmetrie: das
        // leere WhereHow traegt 0, das leere Udder traegt int.MinValue. Wer
        // eines der beiden Modelle angleicht, soll hier stolpern und den
        // Kommentar oben lesen, bevor er es tut - an GetById haengt die
        // Anzeige, die mit dem Leerstring rechnet.
        Assert.Equal(0, new WhereHow().WhereHowId);
        Assert.Equal(int.MinValue, new Udder().UdderId);
    }
}
