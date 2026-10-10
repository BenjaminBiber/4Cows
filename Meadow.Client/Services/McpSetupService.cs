using System.Net.Http.Json;
using System.Text.Json;
using Meadow.Shared;
using Meadow.Shared.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Meadow.Client.Services;

/// <summary>
/// Liest GET /api/mcp/info fuer den Reiter "MCP" der Einstellungen und
/// kopiert Befehle in die Zwischenablage.
///
/// Wie PushService kein Teil der Outbox: die Einrichtung ist reine Anzeige,
/// und ohne Verbindung zum Server laesst sich ohnehin nichts einrichten.
/// </summary>
public sealed class McpSetupService
{
    private static readonly JsonSerializerOptions Json = MeadowJson.CreateOptions();

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly ILogger<McpSetupService> _logger;

    public McpSetupService(HttpClient http, IJSRuntime js, ILogger<McpSetupService> logger)
    {
        _http = http;
        _js = js;
        _logger = logger;
    }

    /// <summary>null, wenn der Server nicht antwortet.</summary>
    public async Task<McpInfo?> GetInfoAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<McpInfo>("api/mcp/info", Json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP-Informationen konnten nicht geladen werden.");
            return null;
        }
    }

    public async Task<bool> CopyAsync(string text)
    {
        try
        {
            return await _js.InvokeAsync<bool>("meadowClipboard.copy", text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kopieren in die Zwischenablage fehlgeschlagen.");
            return false;
        }
    }
}
