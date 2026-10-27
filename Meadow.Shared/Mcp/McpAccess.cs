using System.Security.Cryptography;
using System.Text;

namespace Meadow.Shared.Mcp;

/// <summary>
/// Die eine Regel, ob eine Anfrage an /api/mcp durchdarf.
///
/// Der Token ist OPTIONAL: die uebrige API ist ebenfalls offen und laeuft im
/// Hof-LAN. Wer den MCP-Zugang nach aussen gibt (Caddy, Portweiterleitung),
/// setzt Mcp__Token - dann muss jede Anfrage "Authorization: Bearer &lt;token&gt;"
/// tragen. Steht hier und nicht in der Middleware, damit die Regel ohne
/// ASP.NET pruefbar ist.
/// </summary>
public static class McpAccess
{
    public const string BearerPrefix = "Bearer ";

    /// <summary>Ist ein Token konfiguriert? Leerzeichen zaehlen als "nicht gesetzt".</summary>
    public static bool IsTokenRequired(string? configuredToken)
        => !string.IsNullOrWhiteSpace(configuredToken);

    public static bool IsAuthorized(string? configuredToken, string? authorizationHeader)
    {
        if (!IsTokenRequired(configuredToken))
        {
            return true;
        }

        if (authorizationHeader is null
            || !authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var presented = authorizationHeader[BearerPrefix.Length..].Trim();

        // FixedTimeEquals: ein gewoehnlicher Vergleich bricht beim ersten
        // falschen Zeichen ab und verraet ueber die Laufzeit, wie viel vom
        // Token schon stimmt.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(configuredToken!.Trim()));
    }
}
