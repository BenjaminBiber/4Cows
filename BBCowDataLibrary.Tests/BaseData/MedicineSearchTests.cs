using BB_Cow.Class;

namespace BBCowDataLibrary.Tests.BaseData;

/// <summary>
/// Die Rangfolge des Medikamenten-Autocomplete. Sie zaehlt, weil das Feld nur
/// die ersten Treffer anzeigt - die Sortierung entscheidet also, ob im Stall
/// das gesuchte Praeparat oben steht.
/// </summary>
public class MedicineSearchTests
{
    private static Medicine M(string name) => new(0, name);

    [Fact]
    public void A_match_at_the_start_of_the_name_beats_a_match_in_the_middle()
    {
        // Wer "ubro" tippt, meint "Ubrolexin" und nicht ein Praeparat, das die
        // Zeichenfolge irgendwo in der Mitte traegt.
        var result = MedicineSearch.Rank(
            new[] { M("Kombi-Ubro-Mittel"), M("Ubrolexin") }, "ubro").ToList();

        Assert.Equal("Ubrolexin", result[0]);
    }

    [Fact]
    public void The_shorter_name_comes_first_among_equals()
    {
        // Dasselbe Praeparat, zwei Schreibweisen - die kurze ist im Stall die
        // brauchbarere.
        var result = MedicineSearch.Rank(
            new[]
            {
                M("Ubrolexin 100 mg/ml Injektionssuspension für Rinder"),
                M("Ubrolexin")
            }, "ubro").ToList();

        Assert.Equal("Ubrolexin", result[0]);
    }

    [Fact]
    public void An_empty_term_lists_the_shorter_name_first()
    {
        // Der haeufigste Fall: das Feld wird angeklickt, ohne zu tippen. Ohne
        // Suchbegriff traegt allein die Laenge, danach das Alphabet.
        var result = MedicineSearch.Rank(
            new[] { M("Zzz-Hausmittel"), M("Metacam") }, "").ToList();

        Assert.Equal("Metacam", result[0]);
    }

    [Fact]
    public void The_result_is_capped_so_a_keystroke_never_sorts_the_whole_list()
    {
        // Ohne Obergrenze laege bei jedem Tastendruck die ganze Liste im
        // Speicher des Circuits.
        var many = Enumerable.Range(0, 500).Select(i => M($"Präparat {i:000}")).ToList();

        Assert.Equal(10, MedicineSearch.Rank(many, "präparat", limit: 10).Count());
        Assert.Equal(MedicineSearch.DefaultLimit, MedicineSearch.Rank(many, "").Count());
    }

    [Fact]
    public void Matching_ignores_case()
    {
        Assert.Equal("Ubrolexin", Assert.Single(MedicineSearch.Rank(new[] { M("Ubrolexin") }, "UBRO")));
    }

    [Fact]
    public void A_term_with_no_match_yields_nothing()
    {
        // Wichtig fuer CoerceValue: findet das Feld nichts, bleibt die Eingabe
        // stehen und wird beim Speichern als neues Medikament angelegt. Eine
        // Ersatzliste wuerde diesen Weg verdecken.
        Assert.Empty(MedicineSearch.Rank(new[] { M("Metacam") }, "ubrolexin"));
    }

    [Fact]
    public void Duplicate_names_appear_only_once()
    {
        // Zwei gleichnamige Eintraege koennen bis zum Zusammenfuehren
        // nebeneinander stehen. Zweimal derselbe Text in der Vorschlagsliste
        // waere nicht auseinanderzuhalten.
        var result = MedicineSearch.Rank(new[] { M("Ubrolexin"), M("Ubrolexin") }, "ubro");

        Assert.Single(result);
    }

    [Fact]
    public void An_empty_source_list_yields_nothing()
    {
        // Der Zustand einer frischen Installation: Medicine startet leer.
        Assert.Empty(MedicineSearch.Rank(Array.Empty<Medicine>(), ""));
    }
}
