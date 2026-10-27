using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Welche Nummer der Plus-Knopf einer Behandlungstabelle aus der Suche in den
/// Dialog mitnimmt.
/// </summary>
public class AddPrefillTests
{
    private static Cow Tagged(string earTag, int collar, bool isGone = false)
        => new(earTag, collar, isGone);

    private static Dictionary<string, Cow> Herd(params Cow[] cows)
        => cows.ToDictionary(c => c.CowId, StringComparer.Ordinal);

    private static readonly Dictionary<string, Cow> Cows = Herd(
        Tagged("DE 0815 2301", 86),
        Tagged("DE 0815 2302", 186),
        Tagged("DE 0999 7777", 12));

    [Fact]
    public void Empty_search_prefills_nothing()
        => Assert.Null(AddPrefill.FromSearch("  ", new[] { "DE 0815 2301" }, Cows));

    [Fact]
    public void One_found_cow_searched_by_collar_is_prefilled()
    {
        var prefill = AddPrefill.FromSearch("86", new[] { "DE 0815 2301", "DE 0815 2301" }, Cows);

        Assert.Equal(new AddPrefill("DE 0815 2301", 86), prefill);
    }

    [Fact]
    public void One_found_cow_searched_by_part_of_the_ear_tag_is_prefilled()
    {
        var prefill = AddPrefill.FromSearch("7777", new[] { "DE 0999 7777" }, Cows);

        Assert.Equal(new AddPrefill("DE 0999 7777", 12), prefill);
    }

    [Fact]
    public void Free_text_that_happens_to_hit_one_cow_prefills_nothing()
        => Assert.Null(AddPrefill.FromSearch("Mortellaro", new[] { "DE 0815 2301" }, Cows));

    [Fact]
    public void Several_cows_prefill_nothing()
        => Assert.Null(AddPrefill.FromSearch("0815", new[] { "DE 0815 2301", "DE 0815 2302" }, Cows));

    [Fact]
    public void No_rows_but_a_known_collar_prefills_that_cow()
    {
        // Die Kuh hat nur noch keine Behandlung in dieser Tabelle.
        var prefill = AddPrefill.FromSearch("186", Array.Empty<string>(), Cows);

        Assert.Equal(new AddPrefill("DE 0815 2302", 186), prefill);
    }

    [Fact]
    public void No_rows_and_an_unknown_collar_still_prefills_the_number()
    {
        var prefill = AddPrefill.FromSearch("404", Array.Empty<string>(), Cows);

        Assert.Equal(new AddPrefill(null, 404), prefill);
    }

    [Fact]
    public void No_rows_but_a_unique_ear_tag_prefills_that_cow()
    {
        var prefill = AddPrefill.FromSearch("0999 7777", Array.Empty<string>(), Cows);

        Assert.Equal(new AddPrefill("DE 0999 7777", 12), prefill);
    }

    [Fact]
    public void No_rows_and_an_ambiguous_ear_tag_prefills_nothing()
        => Assert.Null(AddPrefill.FromSearch("0815", Array.Empty<string>(), Cows));

    [Fact]
    public void A_gone_cow_is_not_offered_for_its_old_collar()
    {
        var herd = Herd(Tagged("DE 1", 50, isGone: true));

        Assert.Equal(new AddPrefill(null, 50), AddPrefill.FromSearch("50", Array.Empty<string>(), herd));
    }
}
