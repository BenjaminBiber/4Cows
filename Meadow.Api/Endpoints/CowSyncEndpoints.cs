using Meadow.Api.BackgroundServices;
using Meadow.Api.Infrastructure;
using Meadow.Data.Services;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Der Kuh-Abgleich von aussen angestossen. Bisher gab es dafuer nur den
/// Takt von CowSyncBackgroundService (Voreinstellung: alle 24 Stunden) - wer
/// eine frisch eingestallte Kuh sehen wollte, wartete oder startete den
/// Server neu.
///
/// Zwei Quellen, ein Paar Adressen: bei CowSyncSource = XLink startet hier der
/// Scraper-Lauf (XLinkRunner), bei HorizonAgent wird nur die Anforderung
/// gesetzt, die der Agent beim naechsten Lebenszeichen abholt. XLinkService
/// und HorizonSyncService selbst bleiben unveraendert; was hier dazukommt, ist
/// ausschliesslich die Frage, WER den Lauf startet und wie der Aufrufer
/// erfaehrt, wie es ausging.
/// </summary>
public static class CowSyncEndpoints
{
    public static RouteGroupBuilder MapCowSyncEndpoints(this RouteGroupBuilder api)
    {
        // 202 und nicht 200: angenommen, nicht erledigt. Der Lauf faengt
        // gerade erst an, und diese Antwort sagt ueber sein Ergebnis nichts -
        // das steht danach unter /cowsync/status.
        //
        // Deshalb hier auch kein BumpsOnWrite: zum Zeitpunkt dieser Antwort
        // hat sich an der Kuh-Tabelle noch nichts geaendert. Den Zaehler
        // erhoeht XLinkRunner, wenn der Lauf durch ist; auf dem Horizon-Weg
        // der Snapshot-Endpunkt, wenn der Agent geliefert hat.
        api.MapPost("/cowsync/refresh", (XLinkRunner runner, HorizonSyncService horizon,
            CowSyncOptions cowSync, DemoOptions demo) =>
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
                    detail: "Der Kuh-Abgleich ist im Demo-Modus abgeschaltet. Er wuerde jede Demo-Kuh als abgegangen markieren, weil der Scraper sie nicht kennt.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (cowSync.Source == CowSyncSource.HorizonAgent)
            {
                // Kein 409-Fall: eine zweite Anforderung ueberschreibt nur die
                // erste. Es laeuft hier nichts, was gegeneinander arbeiten
                // koennte - der Abgleich passiert beim Agenten.
                horizon.RequestSync();
                return Results.Accepted(value: new { started = true });
            }

            // 409 und nicht "noch einen starten": zwei Laeufe wuerden auf
            // demselben Kuh-Cache gegeneinander arbeiten. Die Begruendung
            // steht ausfuehrlich am Kopf von XLinkRunner.
            if (!runner.TryStart())
            {
                return Results.Problem(
                    title: "Abgleich laeuft bereits",
                    detail: "Es laeuft bereits ein Kuh-Abgleich. Der Fortschritt steht unter /api/cowsync/status.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            // Ohne Location-Adresse: es entsteht keine Ressource, die man
            // abrufen koennte. Was es gibt, ist der Zustand unter
            // /cowsync/status, und der ist keine Kopie dieses Laufs.
            return Results.Accepted(value: new { started = true });
        }).WithName("CowSyncRefresh");

        // Liest nur, schreibt nichts - beantwortbar auch waehrend ein Lauf
        // laeuft, und genau dafuer da.
        //
        // enabled ist NICHT dasselbe wie running: es sagt, ob in diesem
        // Prozess ueberhaupt abgeglichen wird. Im Demo-Modus laeuft statt
        // CowSyncBackgroundService der DemoResetBackgroundService, und dann
        // sagen lastSyncUtc = null und running = false zusammen genommen sonst
        // faelschlich "aktiv, nur noch nie gelaufen" - denselben Fehlschluss
        // faengt DatabaseInfoDialog.razor heute mit derselben Abfrage ab.
        //
        // running heisst je Quelle etwas anderes, meint aber dasselbe: beim
        // XLink-Weg laeuft gerade ein Lauf, beim Horizon-Weg steht eine
        // Anforderung offen, auf die der Agent noch nicht geantwortet hat.
        api.MapGet("/cowsync/status", (ICowSyncService cowSync, XLinkRunner runner,
            HorizonSyncService horizon, CowSyncOptions options, DemoOptions demo) =>
            Results.Ok(new
            {
                source = options.Source,
                lastSyncUtc = cowSync.LastSyncUtc,
                lastSyncSucceeded = cowSync.LastSyncSucceeded,
                lastSyncError = cowSync.LastSyncError,
                running = options.Source == CowSyncSource.XLink ? runner.IsRunning : horizon.IsSyncPending,
                enabled = !demo.Enabled,

                // Nur der XLink-Weg hat einen Takt und eine Adresse. Auf dem
                // Horizon-Weg bestimmt der Agent, wann er liefert.
                intervalHours = options.Source == CowSyncSource.XLink
                    ? InfrastructureEndpoints.XLinkIntervalHours()
                    : (double?)null,
                url = options.Source == CowSyncSource.XLink ? InfrastructureEndpoints.XLinkUrl() : null,

                agentVersion = cowSync.AgentVersion,
                agentMachineName = cowSync.AgentMachineName,
                lastHeartbeatUtc = cowSync.LastHeartbeatUtc,
                lastSnapshotAnimalCount = cowSync.LastSnapshotAnimalCount,
                agentLastError = cowSync.AgentLastError
            })).WithName("CowSyncStatus");

        return api;
    }
}
