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

    [Theory]
    [InlineData("86", "186")]
    [InlineData("86", "860")]
    [InlineData("15", "157")]
    [InlineData("157", "1570")]
    public void Short_number_matches_only_the_exact_collar(string search, string collar)
    {
        // "86" soll nur Halsband 86 finden, nicht 186 oder 860.
        Assert.False(TableSearch.Matches(search, collar, EarTag));
    }

    [Fact]
    public void Three_digit_number_matches_the_exact_collar()
    {
        Assert.True(TableSearch.Matches("157", "157", EarTag));
    }

    [Fact]
    public void Leading_zero_still_matches_the_collar()
    {
        Assert.True(TableSearch.Matches("086", "86", EarTag));
    }

    [Theory]
    [InlineData("77")]
    [InlineData("778")]
    public void Up_to_three_digits_does_not_reach_into_the_ear_tag(string search)
    {
        Assert.False(TableSearch.Matches(search, Collar, EarTag));
    }

    [Fact]
    public void Four_or_more_digits_search_the_ear_tag()
    {
        Assert.True(TableSearch.Matches("7836", Collar, EarTag));
        Assert.True(TableSearch.Matches("77836", Collar, EarTag));
    }

    [Fact]
    public void Long_number_does_not_match_a_collar()
    {
        Assert.False(TableSearch.Matches("78452", "78", EarTag));
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
        Assert.False(TableSearch.Matches("1234", Collar, EarTag, "Klotz 1234"));
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
