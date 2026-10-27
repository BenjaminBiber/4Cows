using Meadow.Data.Services;

namespace Meadow.Data.Tests.BaseData;

/// <summary>
/// Der Vergleich des Agent-Schluessels. Wichtig ist nicht nur das Ergebnis,
/// sondern dass ein fehlender oder leerer Schluessel nie als Treffer zaehlt -
/// sonst waere eine Instanz ohne Konfiguration fuer jeden offen.
/// </summary>
public class HorizonAgentKeyTests
{
    [Fact]
    public void Matches_returns_true_for_same_key()
    {
        Assert.True(HorizonAgentKey.Matches("test-key", "test-key"));
    }

    [Fact]
    public void Matches_returns_false_for_different_key_of_same_length()
    {
        Assert.False(HorizonAgentKey.Matches("test-key", "tent-key"));
    }

    [Fact]
    public void Matches_returns_false_for_different_length()
    {
        Assert.False(HorizonAgentKey.Matches("test-key", "test-key-2"));
    }

    [Fact]
    public void Matches_is_case_sensitive()
    {
        Assert.False(HorizonAgentKey.Matches("test-key", "TEST-KEY"));
    }

    [Theory]
    [InlineData(null, "test-key")]
    [InlineData("test-key", null)]
    [InlineData("", "test-key")]
    [InlineData("test-key", "")]
    [InlineData("   ", "   ")]
    [InlineData(null, null)]
    public void Matches_returns_false_when_a_side_is_missing(string? configured, string? presented)
    {
        Assert.False(HorizonAgentKey.Matches(configured, presented));
    }
}
