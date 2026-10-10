using System.Text;

namespace Meadow.Shared.Mcp;

/// <summary>Ein Werkzeug des MCP-Servers, so wie die Einrichtungsseite es auflistet.</summary>
public sealed record McpToolInfo(string Name, string Title, string Description, bool ReadOnly);

/// <summary>Antwort von GET /api/mcp/info - was die Einstellungsseite anzeigt.</summary>
public sealed record McpInfo(bool Enabled, string Url, bool TokenRequired, IReadOnlyList<McpToolInfo> Tools);

/// <summary>
/// Die Einrichtungsanleitung fuer den MCP-Server, EINMAL formuliert.
///
/// Zwei Leser: die Einstellungsseite im Browser (Reiter "MCP") und Claude
/// selbst, das GET /api/mcp/setup als Markdown abruft. Beide bekommen
/// dieselben Befehle aus denselben Methoden - sonst steht irgendwann in der
/// Oberflaeche ein anderer Befehl als in der Anleitung, die Claude liest.
///
/// Der Token selbst taucht hier NIE auf, nur der Platzhalter
/// <see cref="TokenPlaceholder"/>: die Einstellungsseite ist nicht
/// authentifiziert, und wer sie oeffnen kann, bekaeme ihn sonst mitgeliefert.
/// </summary>
public static class McpSetupGuide
{
    public const string ServerName = "4cows";
    public const string TokenPlaceholder = "<DEIN_TOKEN>";
    public const string SetupPath = "/api/mcp/setup";
    public const string McpPath = "/api/mcp";

    /// <summary>Der Befehl fuer Claude Code.</summary>
    public static string ClaudeCodeCommand(string mcpUrl, bool tokenRequired)
        => tokenRequired
            ? $"claude mcp add --transport http {ServerName} {mcpUrl} --header \"Authorization: Bearer {TokenPlaceholder}\""
            : $"claude mcp add --transport http {ServerName} {mcpUrl}";

    /// <summary>Der Eintrag fuer .mcp.json (Claude Code, Projektebene) und andere Clients mit HTTP-Transport.</summary>
    public static string McpJson(string mcpUrl, bool tokenRequired)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"mcpServers\": {");
        sb.AppendLine($"    \"{ServerName}\": {{");
        sb.AppendLine("      \"type\": \"http\",");
        if (tokenRequired)
        {
            sb.AppendLine($"      \"url\": \"{mcpUrl}\",");
            sb.AppendLine("      \"headers\": {");
            sb.AppendLine($"        \"Authorization\": \"Bearer {TokenPlaceholder}\"");
            sb.AppendLine("      }");
        }
        else
        {
            sb.AppendLine($"      \"url\": \"{mcpUrl}\"");
        }
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// Claude Desktop spricht lokale Server ueber stdio an. mcp-remote
    /// (npm) ueberbrueckt das auf den HTTP-Endpunkt.
    /// </summary>
    public static string ClaudeDesktopJson(string mcpUrl, bool tokenRequired)
    {
        var args = tokenRequired
            ? $"\"-y\", \"mcp-remote\", \"{mcpUrl}\", \"--allow-http\", \"--header\", \"Authorization: Bearer {TokenPlaceholder}\""
            : $"\"-y\", \"mcp-remote\", \"{mcpUrl}\", \"--allow-http\"";

        return "{\n" +
               "  \"mcpServers\": {\n" +
               $"    \"{ServerName}\": {{\n" +
               "      \"command\": \"npx\",\n" +
               $"      \"args\": [{args}]\n" +
               "    }\n" +
               "  }\n" +
               "}";
    }

    /// <summary>Die vollstaendige Anleitung als Markdown - das, was GET /api/mcp/setup liefert.</summary>
    public static string BuildMarkdown(McpInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 4Cows MCP-Server – Einrichtung");
        sb.AppendLine();
        sb.AppendLine("4Cows dokumentiert Kuh- und Klauenbehandlungen. Über diesen MCP-Server kann Claude die Daten lesen und neue Einträge anlegen oder bestehende ändern. Löschen ist bewusst nur in der Oberfläche möglich.");
        sb.AppendLine();

        if (!info.Enabled)
        {
            sb.AppendLine("> **Der MCP-Server ist auf dieser Instanz abgeschaltet** (`Mcp__Enabled=false`).");
            sb.AppendLine();
        }

        sb.AppendLine("## Verbindung");
        sb.AppendLine();
        sb.AppendLine($"- Transport: Streamable HTTP");
        sb.AppendLine($"- URL: `{info.Url}`");
        sb.AppendLine(info.TokenRequired
            ? $"- Authentifizierung: **erforderlich** – Header `Authorization: Bearer {TokenPlaceholder}`. Den Token kennt der Betreiber (Umgebungsvariable `Mcp__Token` des Containers); er wird hier absichtlich nicht angezeigt."
            : "- Authentifizierung: keine (`Mcp__Token` ist nicht gesetzt). Wer den Server über das lokale Netz hinaus erreichbar macht, sollte `Mcp__Token` setzen.");
        sb.AppendLine();

        sb.AppendLine("## Claude Code");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine(ClaudeCodeCommand(info.Url, info.TokenRequired));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Alternativ als `.mcp.json` im Projektordner:");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(McpJson(info.Url, info.TokenRequired));
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Claude Desktop");
        sb.AppendLine();
        sb.AppendLine("In `claude_desktop_config.json` eintragen (Einstellungen → Entwickler → Konfiguration bearbeiten), danach Claude Desktop neu starten. Voraussetzung: Node.js (für `npx`).");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(ClaudeDesktopJson(info.Url, info.TokenRequired));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Ist 4Cows über HTTPS öffentlich erreichbar, kann die URL auch direkt unter Einstellungen → Konnektoren → „Benutzerdefinierten Konnektor hinzufügen“ eingetragen werden (nur ohne Token möglich).");
        sb.AppendLine();

        sb.AppendLine("## Begriffe");
        sb.AppendLine();
        sb.AppendLine("- **Kuh**: wird über Ohrmarke, Cow-ID oder Halsbandnummer angesprochen. Kälber haben noch keine Ohrmarke, nur eine Halsbandnummer.");
        sb.AppendLine("- **Klauen / Euterviertel**: LV = links vorne, RV = rechts vorne, LH = links hinten, RH = rechts hinten.");
        sb.AppendLine("- **Wie/Wo**: Verabreichungsart (z. B. „Euter“, „i.m.“).");
        sb.AppendLine("- Datumsangaben im Format `JJJJ-MM-TT`.");
        sb.AppendLine();

        AppendTools(sb, "Lesen", info.Tools.Where(t => t.ReadOnly));
        AppendTools(sb, "Schreiben (Anlegen/Ändern)", info.Tools.Where(t => !t.ReadOnly));

        return sb.ToString();
    }

    private static void AppendTools(StringBuilder sb, string heading, IEnumerable<McpToolInfo> tools)
    {
        var list = tools.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        if (list.Count == 0)
        {
            return;
        }

        sb.AppendLine($"## Werkzeuge: {heading}");
        sb.AppendLine();
        foreach (var tool in list)
        {
            sb.AppendLine($"- `{tool.Name}` – {tool.Description}");
        }
        sb.AppendLine();
    }
}
