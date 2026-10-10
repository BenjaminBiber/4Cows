using Meadow.Shared.Mcp;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Mcp;

/// <summary>
/// Wie aus Claudes Angaben Zeilen werden. Ein Fehlgriff hier schreibt eine
/// Behandlung an die falsche Kuh oder legt ein Medikament mit Tippfehler an.
/// </summary>
public class McpLookupTests
{
    private static readonly Dictionary<string, Cow> Herd = new[]
    {
        new Cow("DE 01 0000 0001", 12, false),
        new Cow("DE 01 0000 0002", 13, true),
        new Cow("calf-guid", null, 14, isCalv: true, isGone: false),
        new Cow("DE 01 0000 0003", 13, false),
        // Eine Ohrmarke, die wie eine Halsbandnummer aussieht.
        new Cow("12345", 99, false)
    }.ToDictionary(c => c.CowId, StringComparer.Ordinal);

    [Theory]
    [InlineData("DE 01 0000 0001", "DE 01 0000 0001")]
    [InlineData("de 01 0000 0001", "DE 01 0000 0001")]
    [InlineData(" calf-guid ", "calf-guid")]
    [InlineData("12", "DE 01 0000 0001")]
    [InlineData("14", "calf-guid")]
    public void Resolves_cow_by_id_ear_tag_or_collar(string query, string expected)
        => Assert.Equal(expected, McpLookup.ResolveCow(Herd, query)?.CowId);

    [Fact]
    public void Collar_number_ignores_gone_cows()
        => Assert.Equal("DE 01 0000 0003", McpLookup.ResolveCow(Herd, "13")?.CowId);

    [Fact]
    public void Exact_ear_tag_beats_collar_number()
        => Assert.Equal("12345", McpLookup.ResolveCow(Herd, "12345")?.CowId);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DE 01")]
    [InlineData("777")]
    public void Unknown_cow_is_null(string? query)
        => Assert.Null(McpLookup.ResolveCow(Herd, query));

    private static readonly Dictionary<int, Medicine> Medicines = new()
    {
        [1] = new Medicine(1, "Cobactan"),
        [2] = new Medicine(2, " Metacam ")
    };

    [Theory]
    [InlineData("cobactan", 1)]
    [InlineData("Metacam", 2)]
    [InlineData("2", 2)]
    public void Resolves_base_data_by_name_or_id(string query, int expected)
        => Assert.Equal(expected, McpLookup.ResolveId(Medicines, query, m => m.MedicineName));

    [Theory]
    [InlineData("Penicilin")]
    [InlineData("3")]
    [InlineData("")]
    public void Unknown_base_data_is_null_and_never_created(string query)
        => Assert.Null(McpLookup.ResolveId(Medicines, query, m => m.MedicineName));

    [Fact]
    public void Parses_positions()
    {
        var set = McpLookup.ParsePositions("lv, RH;lv");

        Assert.NotNull(set);
        Assert.Equal(new[] { HoofPosition.LV, HoofPosition.RH }.OrderBy(p => p), set!.OrderBy(p => p));
        Assert.Empty(McpLookup.ParsePositions(null)!);
        Assert.Empty(McpLookup.ParsePositions(" ")!);
    }

    [Theory]
    [InlineData("LV, XX")]
    [InlineData("1")]
    [InlineData("links vorne")]
    public void Invalid_positions_are_null(string positions)
        => Assert.Null(McpLookup.ParsePositions(positions));
}
