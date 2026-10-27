using System.ComponentModel;
using System.Reflection;
using Meadow.Api.Infrastructure;
using Meadow.Api.Mcp;
using Meadow.Shared.Mcp;
using ModelContextProtocol.Server;

namespace Meadow.Api.Endpoints;

public static class McpEndpoints
{
    /// <summary>
    /// Die Werkzeuge, wie die Einrichtungsseite sie auflistet - per Reflection
    /// aus denselben Attributen, aus denen das SDK sie Claude anbietet. Eine
    /// zweite, handgeschriebene Liste liefe beim naechsten neuen Werkzeug
    /// auseinander.
    /// </summary>
    internal static readonly IReadOnlyList<McpToolInfo> Tools =
        new[] { typeof(MeadowReadTools), typeof(MeadowWriteTools) }
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(m => (Method: m, Tool: m.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(x => x.Tool is not null)
            .Select(x => new McpToolInfo(
                x.Tool!.Name ?? x.Method.Name,
                x.Tool.Title ?? x.Tool.Name ?? x.Method.Name,
                x.Method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty,
                x.Tool.ReadOnly))
            .OrderBy(t => t.ReadOnly ? 0 : 1)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>Was Claude beim Verbinden als Server-Anweisung mitbekommt.</summary>
    internal const string ServerInstructions =
        "4Cows dokumentiert Kuh- und Klauenbehandlungen eines Milchviehbetriebs. " +
        "Kühe werden über Ohrmarke, Cow-ID oder Halsbandnummer angesprochen; Kälber haben nur eine Halsbandnummer. " +
        "Klauen und Euterviertel: LV = links vorne, RV = rechts vorne, LH = links hinten, RH = rechts hinten. " +
        "Medikamente, Wie/Wo, Behandlungsgründe und Klauenbefunde müssen in den Basisdaten existieren (list_base_data). " +
        "Datumsangaben im Format JJJJ-MM-TT. Einstieg: get_overview. Löschen ist über MCP nicht möglich. " +
        "Vor dem Schreiben die Angaben mit dem Nutzer abgleichen.";

    public static RouteGroupBuilder MapMcpEndpoints(this RouteGroupBuilder api, IEndpointRouteBuilder app, McpOptions options)
    {
        // Beide ohne Token: sie enthalten nichts Geheimes (der Token selbst
        // steht nie darin), und /setup ist genau die Seite, die Claude lesen
        // soll, BEVOR es den Token hat.
        api.MapGet("/mcp/setup", (HttpRequest request) =>
            Results.Text(McpSetupGuide.BuildMarkdown(Info(request, options)), "text/markdown; charset=utf-8"))
            .WithName("McpSetup");

        api.MapGet("/mcp/info", (HttpRequest request) => Results.Ok(Info(request, options)))
            .WithName("McpInfo");

        if (options.Enabled)
        {
            // Auf app und nicht auf der Gruppe: MapMcp haengt eigene
            // Unterrouten an das Muster, und die Gruppe bringt nichts mit,
            // was der Transport braucht.
            app.MapMcp(McpSetupGuide.McpPath);
        }

        return api;
    }

    /// <summary>
    /// Token-Pruefung nur fuer den Transport selbst, nicht fuer /setup und
    /// /info. Als Middleware statt Endpunktfilter: MapMcp registriert
    /// RequestDelegate-Endpunkte, an denen Endpunktfilter nicht greifen.
    /// </summary>
    public static IApplicationBuilder UseMcpAccess(this IApplicationBuilder app, McpOptions options)
        => app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            var isTransport = path.StartsWithSegments(McpSetupGuide.McpPath, StringComparison.OrdinalIgnoreCase)
                              && !path.StartsWithSegments(McpSetupGuide.SetupPath, StringComparison.OrdinalIgnoreCase)
                              && !path.StartsWithSegments("/api/mcp/info", StringComparison.OrdinalIgnoreCase);

            if (isTransport
                && !McpAccess.IsAuthorized(options.Token, context.Request.Headers.Authorization.ToString()))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.Response.WriteAsync("MCP-Zugang erfordert 'Authorization: Bearer <token>' (Mcp__Token).");
                return;
            }

            await next(context);
        });

    private static McpInfo Info(HttpRequest request, McpOptions options)
        => new(options.Enabled, options.McpUrl(request), options.TokenRequired, Tools);
}
