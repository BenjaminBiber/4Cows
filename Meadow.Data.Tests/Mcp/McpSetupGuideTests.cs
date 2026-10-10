using System.Text.Json;
using Meadow.Shared.Mcp;

namespace Meadow.Data.Tests.Mcp;

/// <summary>
/// Die Einrichtungstexte, die Einstellungsseite und /api/mcp/setup teilen.
/// Ein kaputtes JSON-Snippet merkt der Nutzer erst, wenn Claude Desktop beim
/// Start die Konfiguration verwirft.
/// </summary>
public class McpSetupGuideTests
{
    private const string Url = "http://192.168.50.20:5750/api/mcp";

    private static McpInfo Info(bool tokenRequired) => new(true, Url, tokenRequired, new[]
    {
        new McpToolInfo("list_cows", "Kühe", "Listet Kühe.", ReadOnly: true),
        new McpToolInfo("add_claw_treatment", "Klaue", "Erfasst eine Klauenbehandlung.", ReadOnly: false)
    });

    [Fact]
    public void Command_without_token_has_no_header()
    {
        var cmd = McpSetupGuide.ClaudeCodeCommand(Url, tokenRequired: false);

        Assert.Equal($"claude mcp add --transport http 4cows {Url}", cmd);
    }

    [Fact]
    public void Command_with_token_uses_the_placeholder()
    {
        var cmd = McpSetupGuide.ClaudeCodeCommand(Url, tokenRequired: true);

        Assert.Contains(Url, cmd);
        Assert.Contains($"--header \"Authorization: Bearer {McpSetupGuide.TokenPlaceholder}\"", cmd);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Json_snippets_are_valid_json_with_the_url(bool tokenRequired)
    {
        using var mcpJson = JsonDocument.Parse(McpSetupGuide.McpJson(Url, tokenRequired));
        var server = mcpJson.RootElement.GetProperty("mcpServers").GetProperty(McpSetupGuide.ServerName);
        Assert.Equal("http", server.GetProperty("type").GetString());
        Assert.Equal(Url, server.GetProperty("url").GetString());
        Assert.Equal(tokenRequired, server.TryGetProperty("headers", out _));

        using var desktop = JsonDocument.Parse(McpSetupGuide.ClaudeDesktopJson(Url, tokenRequired));
        var args = desktop.RootElement.GetProperty("mcpServers").GetProperty(McpSetupGuide.ServerName)
            .GetProperty("args").EnumerateArray().Select(a => a.GetString()).ToList();
        Assert.Contains(Url, args);
        Assert.Equal(tokenRequired, args.Contains("--header"));
    }

    [Fact]
    public void Markdown_lists_url_and_tools_separated_by_kind()
    {
        var md = McpSetupGuide.BuildMarkdown(Info(tokenRequired: false));

        Assert.Contains(Url, md);
        var read = md.IndexOf("Werkzeuge: Lesen", StringComparison.Ordinal);
        var write = md.IndexOf("Werkzeuge: Schreiben", StringComparison.Ordinal);
        Assert.True(read >= 0 && write > read);
        Assert.True(md.IndexOf("`list_cows`", StringComparison.Ordinal) is var l && l > read && l < write);
        Assert.True(md.IndexOf("`add_claw_treatment`", StringComparison.Ordinal) > write);
    }

    [Fact]
    public void Markdown_mentions_the_token_requirement_but_never_a_token()
    {
        var md = McpSetupGuide.BuildMarkdown(Info(tokenRequired: true));

        Assert.Contains("erforderlich", md);
        Assert.Contains(McpSetupGuide.TokenPlaceholder, md);
    }

    [Fact]
    public void Disabled_server_is_called_out()
    {
        var md = McpSetupGuide.BuildMarkdown(Info(false) with { Enabled = false });

        Assert.Contains("abgeschaltet", md);
    }
}
