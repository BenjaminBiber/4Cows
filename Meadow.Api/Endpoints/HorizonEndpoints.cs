using Meadow.Api.BackgroundServices;
using Meadow.Api.Infrastructure;
using Meadow.Data.Services;
using Meadow.Shared.Models.Horizon;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Die beiden Adressen, mit denen der Horizon-Agent spricht: er liefert seinen
/// Bestand (snapshot) und meldet sich regelmaessig (heartbeat), um zu erfahren,
/// ob jemand einen Abgleich angefordert hat.
///
/// Anders als der Rest der API spricht hier kein Browser, sondern ein Dienst auf
/// dem Horizon-PC. Deshalb der eigene Schluessel im Header X-Agent-Key: die
/// uebrige API ist im Hausnetz offen, aber dieser Weg schreibt den kompletten
/// Kuhbestand und darf nicht jedem offenstehen, der die Adresse kennt.
/// </summary>
public static class HorizonEndpoints
{
    /// <summary>Header, in dem der Agent seinen Schluessel mitschickt.</summary>
    public const string KeyHeader = "X-Agent-Key";

    public static RouteGroupBuilder MapHorizonEndpoints(this RouteGroupBuilder api)
    {
        // Kein BumpsOnWrite: gebumpt wird nur, wenn der Snapshot wirklich etwas
        // geaendert hat. Ein Agent, der alle paar Minuten denselben Bestand
        // schickt, wuerde sonst jedem Client unnoetig ein Neuladen aufdruecken.
        api.MapPost("/horizon/snapshot", async (HorizonSnapshot body, HttpRequest request,
            HorizonSyncService horizon, HorizonAgentOptions agent, CowSyncOptions cowSync,
            DemoOptions demo, IDataVersion version, CancellationToken cancellationToken) =>
        {
            var blocked = Guard(request, agent, demo);
            if (blocked is not null)
            {
                return blocked;
            }

            // 409 und nicht einfach annehmen: steht der Abgleich auf XLink,
            // schreibt dort der Scraper denselben Bestand. Zwei Quellen auf
            // denselben Kuehen wuerden einander ueberschreiben - der Agent soll
            // merken, dass er hier falsch ist, statt still gegen den Scraper zu
            // arbeiten.
            if (cowSync.Source != CowSyncSource.HorizonAgent)
            {
                return Results.Problem(
                    title: "Falsche Quelle",
                    detail: "Kuh-Abgleich steht auf XLink – der Snapshot wird nicht übernommen.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var result = await horizon.ApplySnapshotAsync(body, cancellationToken);

            // 422 und nicht 400: der Snapshot war syntaktisch in Ordnung, die
            // Schutzregel hat ihn inhaltlich abgelehnt (etwa ein leerer
            // Bestand, der jede Kuh als abgegangen markiert haette).
            // Geschrieben wurde dann nichts.
            if (!result.Accepted)
            {
                return Results.Problem(
                    title: "Snapshot abgelehnt",
                    detail: result.RejectedReason,
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            if (result.Created + result.Updated + result.Linked + result.Promoted > 0)
            {
                version.Bump(DataScope.Cows);
            }

            return Results.Ok(result);
        }).WithName("HorizonSnapshot");

        // Das Lebenszeichen. Liefert nebenbei die Antwort auf die einzige Frage,
        // die der Agent hat: soll ich jetzt lesen?
        //
        // Auch bei Source = XLink angenommen, denn der Status (Version, Rechner,
        // letzter Fehler) ist auch dann nuetzlich. SyncRequested bleibt dort von
        // selbst false, weil RequestSync nur auf dem Horizon-Weg aufgerufen wird.
        api.MapPost("/horizon/heartbeat", (AgentHeartbeat body, HttpRequest request,
            HorizonSyncService horizon, HorizonAgentOptions agent, DemoOptions demo) =>
        {
            var blocked = Guard(request, agent, demo);
            return blocked ?? Results.Ok(horizon.RecordHeartbeat(body));
        }).WithName("HorizonHeartbeat");

        return api;
    }

    /// <summary>
    /// Die Pruefung vor beiden Endpunkten. Liefert null, wenn der Aufruf
    /// durchgelassen wird, sonst die Antwort, mit der abgelehnt wird.
    /// </summary>
    private static IResult? Guard(HttpRequest request, HorizonAgentOptions agent, DemoOptions demo)
    {
        // 404 und nicht 401: ohne konfigurierten Schluessel gibt es diesen Weg
        // auf dieser Instanz schlicht nicht. Ein 401 wuerde verraten, dass es
        // den Endpunkt gibt und nur der Schluessel fehlt.
        if (!agent.IsConfigured)
        {
            return Results.NotFound();
        }

        // 403 aus demselben Grund wie bei /api/cowsync/refresh: der Agent kennt
        // die Demo-Kuehe nicht, ein Snapshot wuerde sie alle als abgegangen
        // markieren.
        if (demo.Enabled)
        {
            return Results.Problem(
                title: "Im Demo-Modus nicht verfuegbar",
                detail: "Der Kuh-Abgleich ist im Demo-Modus abgeschaltet. Er wuerde jede Demo-Kuh als abgegangen markieren, weil der Agent sie nicht kennt.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var presented = request.Headers[KeyHeader].ToString();
        if (!HorizonAgentKey.Matches(agent.ApiKey, presented))
        {
            return Results.Unauthorized();
        }

        return null;
    }
}
