using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Rumpf von POST /where-hows/{whereHowId}/merge. IWhereHowService.MergeAsync
/// kennt keinen ueberlebenden Namen - anders als beim Medikament.
/// </summary>
public sealed record WhereHowMergeRequest(int TargetId);

public static class WhereHowEndpoints
{
    public static RouteGroupBuilder MapWhereHowEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/where-hows", async (IWhereHowService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.WhereHows.Values.ToList());
        }).WithName("WhereHowList");

        api.MapGet("/where-hows/usage-counts", async (IWhereHowService svc) =>
        {
            var counts = await svc.GetUsageCountsAsync();
            return counts is null
                ? EndpointCommon.UsageCountsUnavailable("Wie/Wo-Eintraege")
                : Results.Ok(counts);
        }).WithName("WhereHowUsageCounts");

        api.MapGet("/where-hows/{whereHowId:int}", async (int whereHowId, IWhereHowService svc) =>
        {
            var whereHow = await EndpointCommon.FindAsync(() => svc.WhereHows, whereHowId, svc.GetAllDataAsync);
            return whereHow is null ? EndpointCommon.NotFound("Wie/Wo", whereHowId) : Results.Ok(whereHow);
        }).WithName("WhereHowById");

        api.MapPost("/where-hows", async (WhereHow whereHow, IWhereHowService svc) =>
        {
            var ok = await svc.InsertDataAsync(whereHow);
            return ok
                ? Results.Created($"/api/where-hows/{whereHow.WhereHowId}", whereHow)
                : EndpointCommon.WriteFailed($"Wie/Wo {whereHow.WhereHowName} konnte nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.WhereHows).WithName("WhereHowCreate");

        api.MapPut("/where-hows/{whereHowId:int}", async (int whereHowId, WhereHow whereHow, IWhereHowService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.WhereHows, whereHowId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Wie/Wo", whereHowId);
            }

            whereHow.WhereHowId = whereHowId;

            var ok = await svc.UpdateDataAsync(whereHow);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Wie/Wo {whereHowId} konnte nicht geaendert werden.");
        }).BumpsOnWrite(DataScope.WhereHows).WithName("WhereHowUpdate");

        api.MapDelete("/where-hows/{whereHowId:int}", async (int whereHowId, IWhereHowService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.WhereHows, whereHowId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Wie/Wo", whereHowId);
            }

            var ok = await svc.RemoveByIdAsync(whereHowId);
            return ok ? Results.NoContent() : EndpointCommon.StillInUse("Wie/Wo", whereHowId);
        }).BumpsOnWrite(DataScope.WhereHows).WithName("WhereHowDelete");

        // Die Pruefung auf Quelle und Ziel steht hier nicht der Form halber:
        // WhereHowService.MergeAsync hat - anders als MedicineService und
        // ClawFindingService - KEINE Existenzpruefung im Rumpf. Es haengt die
        // Behandlungen um und meldet true, auch wenn es die Ziel-Id gar nicht
        // gibt; danach zeigen die Zeilen auf eine verwaiste WhereHow_ID.
        //
        // alsoBumps wie beim Medikament: umgehaengt werden Cow_Treatment und
        // Planned_Cow_Treatment.
        api.MapPost("/where-hows/{whereHowId:int}/merge", async (int whereHowId, WhereHowMergeRequest body, IWhereHowService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.WhereHows, whereHowId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Wie/Wo", whereHowId);
            }

            if (body.TargetId == whereHowId)
            {
                return EndpointCommon.Invalid("targetId", "Quelle und Ziel muessen verschieden sein.");
            }

            if (!await EndpointCommon.ExistsAsync(() => svc.WhereHows, body.TargetId, svc.GetAllDataAsync))
            {
                return EndpointCommon.Invalid("targetId", $"Wie/Wo {body.TargetId} gibt es nicht.");
            }

            var ok = await svc.MergeAsync(whereHowId, body.TargetId);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Wie/Wo {whereHowId} konnte nicht auf {body.TargetId} verschmolzen werden.");
        }).BumpsOnWrite(DataScope.WhereHows, DataScope.CowTreatments, DataScope.PlannedCowTreatments)
          .WithName("WhereHowMerge");

        return api;
    }
}
