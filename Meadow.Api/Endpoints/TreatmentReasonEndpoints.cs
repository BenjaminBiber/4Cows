using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Rumpf von POST /treatment-reasons/{reasonId}/merge.
/// ITreatmentReasonService.MergeAsync kennt keinen ueberlebenden Namen.
/// </summary>
public sealed record TreatmentReasonMergeRequest(int TargetId);

public static class TreatmentReasonEndpoints
{
    public static RouteGroupBuilder MapTreatmentReasonEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/treatment-reasons", async (ITreatmentReasonService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Reasons.Values.ToList());
        }).WithName("TreatmentReasonList");

        api.MapGet("/treatment-reasons/usage-counts", async (ITreatmentReasonService svc) =>
        {
            var counts = await svc.GetUsageCountsAsync();
            return counts is null
                ? EndpointCommon.UsageCountsUnavailable("Behandlungsgruende")
                : Results.Ok(counts);
        }).WithName("TreatmentReasonUsageCounts");

        api.MapGet("/treatment-reasons/{reasonId:int}", async (int reasonId, ITreatmentReasonService svc) =>
        {
            var reason = await EndpointCommon.FindAsync(() => svc.Reasons, reasonId, svc.GetAllDataAsync);
            return reason is null ? EndpointCommon.NotFound("Behandlungsgrund", reasonId) : Results.Ok(reason);
        }).WithName("TreatmentReasonById");

        api.MapPost("/treatment-reasons", async (TreatmentReason reason, ITreatmentReasonService svc) =>
        {
            var ok = await svc.InsertDataAsync(reason);
            return ok
                ? Results.Created($"/api/treatment-reasons/{reason.TreatmentReasonId}", reason)
                : EndpointCommon.WriteFailed($"Der Behandlungsgrund {reason.TreatmentReasonName} konnte nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.TreatmentReasons).WithName("TreatmentReasonCreate");

        api.MapPut("/treatment-reasons/{reasonId:int}", async (int reasonId, TreatmentReason reason, ITreatmentReasonService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Reasons, reasonId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Behandlungsgrund", reasonId);
            }

            reason.TreatmentReasonId = reasonId;

            var ok = await svc.UpdateDataAsync(reason);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Behandlungsgrund {reasonId} konnte nicht geaendert werden.");
        }).BumpsOnWrite(DataScope.TreatmentReasons).WithName("TreatmentReasonUpdate");

        api.MapDelete("/treatment-reasons/{reasonId:int}", async (int reasonId, ITreatmentReasonService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Reasons, reasonId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Behandlungsgrund", reasonId);
            }

            var ok = await svc.RemoveByIdAsync(reasonId);
            return ok ? Results.NoContent() : EndpointCommon.StillInUse("Behandlungsgrund", reasonId);
        }).BumpsOnWrite(DataScope.TreatmentReasons).WithName("TreatmentReasonDelete");

        // Wie bei Wie/Wo: TreatmentReasonService.MergeAsync prueft die beiden
        // Ids NICHT nach und meldet auch dann true, wenn es das Ziel nicht mehr
        // gibt. Die Pruefung muss deshalb hier stehen.
        api.MapPost("/treatment-reasons/{reasonId:int}/merge", async (int reasonId, TreatmentReasonMergeRequest body, ITreatmentReasonService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Reasons, reasonId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Behandlungsgrund", reasonId);
            }

            if (body.TargetId == reasonId)
            {
                return EndpointCommon.Invalid("targetId", "Quelle und Ziel muessen verschieden sein.");
            }

            if (!await EndpointCommon.ExistsAsync(() => svc.Reasons, body.TargetId, svc.GetAllDataAsync))
            {
                return EndpointCommon.Invalid("targetId", $"Behandlungsgrund {body.TargetId} gibt es nicht.");
            }

            var ok = await svc.MergeAsync(reasonId, body.TargetId);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Behandlungsgrund {reasonId} konnte nicht auf {body.TargetId} verschmolzen werden.");
        }).BumpsOnWrite(DataScope.TreatmentReasons, DataScope.CowTreatments, DataScope.PlannedCowTreatments)
          .WithName("TreatmentReasonMerge");

        return api;
    }
}
