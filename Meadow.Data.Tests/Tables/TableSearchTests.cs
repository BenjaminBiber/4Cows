using Meadow.Client.Components.Ui;

namespace Meadow.Data.Tests.Tables;

/// <summary>
/// Die Ziffernregel der Tabellensuche. Sie zaehlt, weil eine reine Ziffernsuche
/// an Halsband- und Ohrmarkennummer sonst mehrdeutig ist: eine kurze Zahl wie
/// "52" ist eine Halsbandnummer, steckt aber als Teilstring in fast jeder
/// Ohrmarke - und zog so fremde Tiere in die Liste.
/// </summary>
public class TableSearchTests
{
    // Realistische Werte aus dem Screenshot: kurze Halsbandnummer, lange Ohrmarke.
    private const string Collar = "52";
    private const string EarTag = "DE 0989778364";

    [Fact]
    public void Two_digit_number_matches_the_collar()
    {
        Assert.True(TableSearch.Matches("52", Collar, EarTag));
    }

    [Fact]
    public void Two_digit_number_does_not_reach_into_the_ear_tag()
    {
        // "77" steckt in der Ohrmarke, ist aber nicht die Halsbandnummer - eine
        // zweistellige Suche soll es trotzdem nicht finden.
        Assert.False(TableSearch.Matches("77", Collar, EarTag));
    }

    [Fact]
    public void Three_digit_number_also_searches_the_ear_tag()
    {
        // Erst ab drei Ziffern meint die Zahl gezielt eine Ohrmarke.
        Assert.True(TableSearch.Matches("778", Collar, EarTag));
    }

    [Fact]
    public void Three_digit_number_still_matches_the_collar()
    {
        Assert.True(TableSearch.Matches("989", "989", EarTag));
    }

    [Fact]
    public void A_short_number_ignores_the_free_text()
    {
        // "2" soll Halsband 2 finden, nicht jede Zeile mit "2 Verbände".
        Assert.False(TableSearch.Matches("2", "5", EarTag, "Geschwür · 2 Verbände"));
    }

    [Fact]
    public void A_long_number_ignores_the_free_text_too()
    {
        // Auch dreistellig bleibt die reine Ziffernsuche an den Nummern - der
        // Freitext zaehlt nur bei nicht-numerischer Suche.
        Assert.False(TableSearch.Matches("123", Collar, EarTag, "Klotz 123"));
    }

    [Fact]
    public void Text_search_still_matches_the_free_text()
    {
        // Wortsuchen sind von der Ziffernregel unberuehrt und durchsuchen wie
        // bisher alle Felder.
        Assert.True(TableSearch.Matches("Geschwür", Collar, EarTag, "Geschwür · Pflege"));
    }

    [Fact]
    public void Text_search_still_matches_collar_and_ear_tag()
    {
        Assert.True(TableSearch.Matches("de", Collar, EarTag, "Pflege"));
    }

    [Theory]
    [InlineData("52", true)]
    [InlineData("778", true)]
    [InlineData("de", false)]
    [InlineData("12a", false)]
    [InlineData("", false)]
    public void IsNumeric_recognises_only_pure_digit_searches(string search, bool expected)
    {
        Assert.Equal(expected, TableSearch.IsNumeric(search));
    }
}
