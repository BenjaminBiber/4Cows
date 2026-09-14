using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>Rumpf von PUT /cows/{cowId}/collar-number.</summary>
public sealed record CollarNumberUpdate(int CollarNumber);

/// <summary>Rumpf von PUT /cows/{cowId}/is-gone.</summary>
public sealed record IsGoneUpdate(bool IsGone);

/// <summary>Rumpf von PUT /cows/{cowId}/ear-tag.</summary>
public sealed record EarTagAssignment(string? EarTagNumber);

public static class CowEndpoints
{
    public static RouteGroupBuilder MapCowEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/cows", async (ICowService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Cows.Values.ToList());
        }).WithName("CowList");

        api.MapGet("/cows/{cowId}", async (string cowId, ICowService svc) =>
        {
            var cow = await EndpointCommon.FindAsync(() => svc.Cows, cowId, svc.GetAllDataAsync);
            return cow is null ? EndpointCommon.NotFound("Kuh", cowId) : Results.Ok(cow);
        }).WithName("CowById");

        // Cow_ID ist der einzige vom Client vergebene Schluessel im Datenmodell;
        // jede andere Tabelle bekommt ihre Id von der Datenbank. Deshalb steht
        // hier als einzigem Insert eine Vorabpruefung: ohne sie liefe ein
        // zweiter POST desselben Tieres in den doppelten Primaerschluessel, und
        // aus einem benennbaren Fall wuerde eine 500.
        api.MapPost("/cows", async (Cow cow, ICowService svc) =>
        {
            if (string.IsNullOrWhiteSpace(cow.CowId))
            {
                return EndpointCommon.Invalid("cowId", "Cow_ID ist Pflicht - die Datenbank vergibt sie hier nicht.");
            }

            if (await EndpointCommon.ExistsAsync(() => svc.Cows, cow.CowId, svc.GetAllDataAsync))
            {
                return Results.Problem(
                    title: "Kuh gibt es schon",
                    detail: $"Kuh {cow.CowId} ist bereits angelegt.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var ok = await svc.InsertDataAsync(cow);
            return ok
                ? Results.Created($"/api/cows/{cow.CowId}", cow)
                : EndpointCommon.WriteFailed($"Kuh {cow.CowId} konnte nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.Cows).WithName("CowCreate");

        // ICowService hat kein allgemeines Update, sondern drei benannte
        // Aenderungen - je eine Handvoll Spalten, je ein ExecuteUpdate. Eine
        // gesammelte PUT /cows/{cowId}-Route gibt es deshalb nicht: sie muesste
        // sich aus diesen dreien zusammensetzen und koennte auf halbem Weg
        // stehenbleiben, ohne dass der Aufrufer erfaehrt, welche Haelfte
        // geschrieben wurde.
        api.MapPut("/cows/{cowId}/collar-number", async (string cowId, CollarNumberUpdate body, ICowService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Cows, cowId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Kuh", cowId);
            }

            var ok = await svc.UpdateCollarNumberAsync(cowId, body.CollarNumber);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Die Halsbandnummer von Kuh {cowId} konnte nicht geaendert werden.");
        }).BumpsOnWrite(DataScope.Cows).WithName("CowUpdateCollarNumber");

        api.MapPut("/cows/{cowId}/is-gone", async (string cowId, IsGoneUpdate body, ICowService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Cows, cowId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Kuh", cowId);
            }

            var ok = await svc.UpdateIsGoneAsync(cowId, body.IsGone);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Der Abgangsvermerk von Kuh {cowId} konnte nicht geaendert werden.");
        }).BumpsOnWrite(DataScope.Cows).WithName("CowUpdateIsGone");

        // PromoteCalfAsync setzt die Ohrmarke UND loescht das Kalb-Kennzeichen,
        // ohne die Cow_ID anzufassen - an der haengt die Behandlungshistorie.
        // Die Route heisst deshalb nach dem, was der Aufrufer uebergibt, und
        // nicht nach dem Kalb.
        api.MapPut("/cows/{cowId}/ear-tag", async (string cowId, EarTagAssignment body, ICowService svc) =>
        {
            if (string.IsNullOrWhiteSpace(body.EarTagNumber))
            {
                return EndpointCommon.Invalid("earTagNumber", "Ohne Ohrmarke bleibt das Tier ein Kalb.");
            }

            if (!await EndpointCommon.ExistsAsync(() => svc.Cows, cowId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Kuh", cowId);
            }

            var ok = await svc.PromoteCalfAsync(cowId, body.EarTagNumber);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Kuh {cowId} konnte die Ohrmarke nicht zugewiesen bekommen.");
        }).BumpsOnWrite(DataScope.Cows).WithName("CowAssignEarTag");

        // Anders als bei den vier Nachschlagetabellen prueft
        // CowService.RemoveByIdAsync NICHT, ob das Tier noch benutzt wird - es
        // loescht und meldet die Zeilenzahl. Ein false nach einem Treffer
        // bedeutet hier also keine Benutzung, sondern eine Ausnahme oder eine
        // Zeile, die inzwischen weg ist: 500 statt 409.
        api.MapDelete("/cows/{cowId}", async (string cowId, ICowService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Cows, cowId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Kuh", cowId);
            }

            var ok = await svc.RemoveByIdAsync(cowId);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Kuh {cowId} konnte nicht geloescht werden.");
        }).BumpsOnWrite(DataScope.Cows).WithName("CowDelete");

        return api;
    }
}
