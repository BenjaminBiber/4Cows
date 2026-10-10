using Meadow.Shared.Mcp;

namespace Meadow.Api.Infrastructure;

/// <summary>
/// Die MCP-Konfiguration aus <c>Mcp:Enabled</c>, <c>Mcp:Token</c> und
/// <c>Mcp:PublicUrl</c> (als Umgebungsvariablen <c>Mcp__Enabled</c> usw.).
///
/// Gleiche Haltung wie <see cref="PushOptions"/> und DemoOptions: ein
/// unlesbarer Wert reisst den Start nicht ab, sondern faellt still auf den
/// Standard. Der Standard fuer Enabled ist hier allerdings TRUE - der
/// MCP-Server ist ein Teil der API wie jeder andere Endpunkt.
/// </summary>
public sealed record McpOptions(bool Enabled, string? Token, string? PublicUrl)
{
    public bool TokenRequired => McpAccess.IsTokenRequired(Token);

    public static McpOptions FromConfiguration(IConfiguration configuration)
    {
        var enabled = !bool.TryParse(configuration["Mcp:Enabled"], out var on) || on;
        var publicUrl = configuration["Mcp:PublicUrl"];

        return new McpOptions(
            Enabled: enabled,
            Token: configuration["Mcp:Token"],
            PublicUrl: string.IsNullOrWhiteSpace(publicUrl) ? null : publicUrl.Trim().TrimEnd('/'));
    }

    /// <summary>
    /// Die Adresse, unter der Claude den Server erreicht.
    ///
    /// Ohne PublicUrl aus der Anfrage abgeleitet. Die Anwendung wertet
    /// X-Forwarded-* sonst nirgends aus (kein UseForwardedHeaders), hinter
    /// Caddy (deploy/caddy/Caddyfile setzt beide Header) stuende hier also
    /// "http://" statt "https://". Deshalb werden die beiden Header hier
    /// gelesen - nur fuer die ANZEIGE, keine Entscheidung haengt daran. Wer
    /// einen Proxy ohne diese Header betreibt, setzt Mcp__PublicUrl auf die
    /// aeussere Basisadresse (ohne /api/mcp).
    /// </summary>
    public string McpUrl(HttpRequest request)
    {
        var scheme = First(request.Headers["X-Forwarded-Proto"]) ?? request.Scheme;
        var host = First(request.Headers["X-Forwarded-Host"]) ?? request.Host.ToString();
        var baseUrl = PublicUrl ?? $"{scheme}://{host}{request.PathBase}";
        return baseUrl.TrimEnd('/') + McpSetupGuide.McpPath;

        // Hinter mehreren Proxys steht eine Liste; der erste Eintrag ist der aeusserste.
        static string? First(string? header)
            => string.IsNullOrWhiteSpace(header) ? null : header.Split(',')[0].Trim();
    }
}
