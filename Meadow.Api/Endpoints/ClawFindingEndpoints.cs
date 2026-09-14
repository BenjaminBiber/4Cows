using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>Rumpf von POST /claw-findings/{findingId}/merge.</summary>
public sealed record ClawFindingMergeRequest(int TargetId, string? SurvivingName);

/// <summary>Rumpf von POST /claw-findings/by-name.</summary>
public sealed record ClawFindingByNameRequest(string? Name);

public static class ClawFindingEndpoints
{
    public static RouteGroupBuilder MapClawFindingEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/claw-findings", async (IClawFindingService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Findings.Values.ToList());
        }).WithName("ClawFindingList");

        api.MapGet("/claw-findings/usage-counts", async (IClawFindingService svc) =>
        {
            var counts = await svc.GetUsageCountsAsync();
            return counts is null
                ? EndpointCommon.UsageCountsUnavailable("Klauenbefunde")
                : Results.Ok(counts);
        }).WithName("ClawFindingUsageCounts");

        api.MapGet("/claw-findings/{findingId:int}", async (int findingId, IClawFindingService svc) =>
        {
            var finding = await EndpointCommon.FindAsync(() => svc.Findings, findingId, svc.GetAllDataAsync);
            return finding is null ? EndpointCommon.NotFound("Klauenbefund", findingId) : Results.Ok(finding);
        }).WithName("ClawFindingById");

        api.MapPost("/claw-findings", async (ClawFinding finding, IClawFindingService svc) =>
        {
            var ok = await svc.InsertDataAsync(finding);
            return ok
                ? Results.Created($"/api/claw-findings/{finding.ClawFindingId}", finding)
                : EndpointCommon.WriteFailed($"Der Klauenbefund {finding.ClawFindingName} konnte nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.ClawFindings).WithName("ClawFindingCreate");

        // Suchen, sonst anlegen; die Begruendung fuer den eigenen Endpunkt
        // steht bei /medicines/by-name.
        //
        // Als einziger der vier faengt dieser den leeren Namen NICHT vorher
        // ab, sondern reicht ihn durch. GetIdByNameAsync unterscheidet hier -
        // anders als bei Medikament, Wie/Wo und Behandlungsgrund - zwei
        // Faelle: null heisst "keine Eingabe" und ist regulaer (im
        // Klauenbehandlungs-Dialog bleiben Befundfelder oft leer), erst
        // ClawFinding.FailedId heisst "ging schief". Die Begruendung dafuer
        // steht am Modell.
        //
        // Deshalb ist {"id":null} hier eine 200 und keine 400: ein
        // Klauenbefundfeld leer zu lassen ist kein Fehler des Aufrufers.
        api.MapPost("/claw-findings/by-name", async (ClawFindingByNameRequest body, IClawFindingService svc) =>
        {
            var id = await svc.GetIdByNameAsync(body.Name);
            return id == ClawFinding.FailedId
                ? EndpointCommon.UpsertFailed("Den Klauenbefund", body.Name?.Trim() ?? string.Empty)
                : Results.Ok(new { id });
        }).BumpsOnWrite(DataScope.ClawFindings).WithName("ClawFindingByName");

        api.MapPut("/claw-findings/{findingId:int}", async (int findingId, ClawFinding finding, IClawFindingService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Findings, findingId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Klauenbefund", findingId);
            }

            finding.ClawFindingId = findingId;

            var ok = await svc.UpdateDataAsync(finding);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Klauenbefund {findingId} konnte nicht geaendert werden.");
        }).BumpsOnWrite(DataScope.ClawFindings).WithName("ClawFindingUpdate");

        api.MapDelete("/claw-findings/{findingId:int}", async (int findingId, IClawFindingService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Findings, findingId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Klauenbefund", findingId);
            }

            var ok = await svc.RemoveByIdAsync(findingId);
            return ok ? Results.NoContent() : EndpointCommon.StillInUse("Klauenbefund", findingId);
        }).BumpsOnWrite(DataScope.ClawFindings).WithName("ClawFindingDelete");

        // Nur EIN alsoBumps: ein Klauenbefund haengt an Claw_Treatment, dort
        // allerdings an vier Spalten derselben Tabelle. Kuhbehandlungen kennen
        // ihn nicht - sie mitzuzaehlen waere falsch und liesse Clients ohne
        // Grund nachladen.
        api.MapPost("/claw-findings/{findingId:int}/merge", async (int findingId, ClawFindingMergeRequest body, IClawFindingService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Findings, findingId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Klauenbefund", findingId);
            }

            if (body.TargetId == findingId)
            {
                return EndpointCommon.Invalid("targetId", "Quelle und Ziel muessen verschieden sein.");
            }

            if (!await EndpointCommon.ExistsAsync(() => svc.Findings, body.TargetId, svc.GetAllDataAsync))
            {
                return EndpointCommon.Invalid("targetId", $"Klauenbefund {body.TargetId} gibt es nicht.");
            }

            var ok = await svc.MergeAsync(findingId, body.TargetId, body.SurvivingName);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Klauenbefund {findingId} konnte nicht auf {body.TargetId} verschmolzen werden.");
        }).BumpsOnWrite(DataScope.ClawFindings, DataScope.ClawTreatments)
          .WithName("ClawFindingMerge");

        return api;
    }
}
