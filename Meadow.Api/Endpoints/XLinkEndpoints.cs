using Meadow.Api.Components.Services;
using Meadow.Api.Infrastructure;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Der XLink-Abgleich von aussen angestossen. Bisher gab es dafuer nur den
/// Takt von CowSyncBackgroundService (Voreinstellung: alle 24 Stunden) - wer
/// eine frisch eingestallte Kuh sehen wollte, wartete oder startete den
/// Server neu.
///
/// XLinkService selbst bleibt unveraendert. Was hier dazukommt, ist
/// ausschliesslich die Frage, WER den Lauf startet und wie der Aufrufer
/// erfaehrt, wie es ausging; das haelt XLinkRunner.
/// </summary>
public static class XLinkEndpoints
{
    public static RouteGroupBuilder MapXLinkEndpoints(this RouteGroupBuilder api)
    {
        // 202 und nicht 200: angenommen, nicht erledigt. Der Lauf faengt
        // gerade erst an, und diese Antwort sagt ueber sein Ergebnis nichts -
        // das steht danach unter /xlink/status.
        //
        // Deshalb hier auch kein BumpsOnWrite: zum Zeitpunkt dieser Antwort
        // hat sich an der Kuh-Tabelle noch nichts geaendert. Den Zaehler
        // erhoeht XLinkRunner, wenn der Lauf durch ist.
        api.MapPost("/xlink/refresh", (XLinkRunner runner, DemoSettings demo) =>
        {
            // 403 im Demo-Modus, und zwar bevor irgendetwas startet.
            //
            // Program.cs registriert dort den CowSyncBackgroundService gar
            // nicht erst, und der Grund dafuer steht am Kopf von
            // DemoResetBackgroundService: der Scraper liefert die Demo-Kuehe
            // nicht, also markierte XLinkService.SaveCowData jede einzelne als
            // IsGone. Danach waere jede Kuh-Auswahl in jedem Dialog leer -
            // bis zum naechtlichen Reset, der den Bestand wiederherstellt.
            //
            // Ein Endpunkt, der genau das von Hand ausloesen kann, waere die
            // Hintertuer zu einer Entscheidung, die beim Start bewusst
            // gefallen ist.
            if (demo.Enabled)
            {
                return Results.Problem(
                    title: "Im Demo-Modus nicht verfuegbar",
                    detail: "Der XLink-Abgleich ist im Demo-Modus abgeschaltet. Er wuerde jede Demo-Kuh als abgegangen markieren, weil der Scraper sie nicht kennt.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            // 409 und nicht "noch einen starten": zwei Laeufe wuerden auf
            // demselben Kuh-Cache gegeneinander arbeiten. Die Begruendung
            // steht ausfuehrlich am Kopf von XLinkRunner.
            if (!runner.TryStart())
            {
                return Results.Problem(
                    title: "Abgleich laeuft bereits",
                    detail: "Es laeuft bereits ein XLink-Abgleich. Der Fortschritt steht unter /api/xlink/status.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            // Ohne Location-Adresse: es entsteht keine Ressource, die man
            // abrufen koennte. Was es gibt, ist der Zustand unter
            // /xlink/status, und der ist keine Kopie dieses Laufs.
            return Results.Accepted(value: new { started = true });
        }).WithName("XLinkRefresh");

        // Liest nur, schreibt nichts - beantwortbar auch waehrend ein Lauf
        // laeuft, und genau dafuer da.
        //
        // enabled ist NICHT dasselbe wie running: es sagt, ob in diesem
        // Prozess ueberhaupt abgeglichen wird. Im Demo-Modus laeuft statt
        // CowSyncBackgroundService der DemoResetBackgroundService, und dann
        // sagen lastSyncUtc = null und running = false zusammen genommen sonst
        // faelschlich "aktiv, nur noch nie gelaufen" - denselben Fehlschluss
        // faengt DatabaseInfoDialog.razor heute mit derselben Abfrage ab.
        api.MapGet("/xlink/status", (IXLinkService xLink, XLinkRunner runner, DemoSettings demo) =>
            Results.Ok(new
            {
                lastSyncUtc = xLink.LastSyncUtc,
                lastSyncSucceeded = xLink.LastSyncSucceeded,
                lastSyncError = xLink.LastSyncError,
                running = runner.IsRunning,
                enabled = !demo.Enabled,
                intervalHours = InfrastructureEndpoints.XLinkIntervalHours(),
                url = InfrastructureEndpoints.XLinkUrl()
            })).WithName("XLinkStatus");

        return api;
    }
}
