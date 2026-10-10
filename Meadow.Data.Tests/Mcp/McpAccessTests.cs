using Meadow.Shared.Mcp;

namespace Meadow.Data.Tests.Mcp;

/// <summary>
/// Der optionale Token vor /api/mcp. Ein Fehler hier faellt im Betrieb nicht
/// auf: entweder ist der Server still offen, obwohl ein Token gesetzt ist,
/// oder Claude bekommt 401, obwohl der Nutzer alles richtig eingetragen hat.
/// </summary>
public class McpAccessTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_configured_token_everything_is_allowed(string? token)
    {
        Assert.False(McpAccess.IsTokenRequired(token));
        Assert.True(McpAccess.IsAuthorized(token, null));
        Assert.True(McpAccess.IsAuthorized(token, "Bearer irgendwas"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("geheim")]
    [InlineData("Basic geheim")]
    [InlineData("Bearer falsch")]
    [InlineData("Bearer geheim2")]
    [InlineData("Bearer gehei")]
    public void With_token_wrong_or_missing_headers_are_rejected(string? header)
    {
        Assert.True(McpAccess.IsTokenRequired("geheim"));
        Assert.False(McpAccess.IsAuthorized("geheim", header));
    }

    [Theory]
    [InlineData("Bearer geheim")]
    [InlineData("bearer geheim")]
    [InlineData("Bearer  geheim ")]
    public void With_token_the_correct_bearer_is_accepted(string header)
        => Assert.True(McpAccess.IsAuthorized("geheim", header));

    [Fact]
    public void Configured_token_is_trimmed()
        => Assert.True(McpAccess.IsAuthorized(" geheim\n", "Bearer geheim"));
}
