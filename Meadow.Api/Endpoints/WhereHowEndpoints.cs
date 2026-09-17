using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Rumpf von POST /where-hows/{whereHowId}/merge. survivingName ist optional -
/// fehlt er, behaelt das Ziel seinen Namen. Gleiche Form wie beim Medikament
/// und beim Klauenbefund.
/// </summary>
public sealed record WhereHowMergeRequest(int TargetId, string? SurvivingName);

/// <summary>
/// Rumpf von POST /where-hows/by-name.
///
/// showDialog ist NULLABLE und nicht bool: fehlt das Feld, soll der Default
/// des Dienstes gelten (true), und ein nicht gesetztes bool waere hier
/// stillschweigend false. Der Unterschied ist keine Kleinigkeit - der
/// Kommentar an WhereHowService.GetWhereHowIDByName beschreibt, was ein
/// falsch geratener Wert anrichtet.
/// </summary>
public sealed record WhereHowByNameRequest(string? Name, bool? ShowDialog);

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

        // Suchen, sonst anlegen; die Begruendung fuer den eigenen Endpunkt
        // steht bei /medicines/by-name.
        //
        // Der leere Name wird hier abgefangen und nicht im Dienst:
        // GetWhereHowIDByName ruft name.ToLower() ohne Pruefung auf und liefe
        // bei null in eine NullReferenceException, aus der eine nackte 500
        // ohne Rumpf wuerde. Die Signatur des Dienstes bleibt unangetastet.
        api.MapPost("/where-hows/by-name", async (WhereHowByNameRequest body, IWhereHowService svc) =>
        {
            var name = body.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return EndpointCommon.Invalid("name", "Ein Wie/Wo ohne Namen ist keines.");
            }

            var id = await svc.GetWhereHowIDByName(name, body.ShowDialog ?? true);
            return id == int.MinValue
                ? EndpointCommon.UpsertFailed("Das Wie/Wo", name)
                : Results.Ok(new { id });
        }).BumpsOnWrite(DataScope.WhereHows).WithName("WhereHowByName");

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

            var ok = await svc.MergeAsync(whereHowId, body.TargetId, body.SurvivingName);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Wie/Wo {whereHowId} konnte nicht auf {body.TargetId} verschmolzen werden.");
            // Medicines kommt dazu, seit der Merge auch Default_WhereHow_ID
            // umhaengt - ohne den Bump zeigten andere Clients danach weiter auf
            // die geloeschte Id.
        }).BumpsOnWrite(DataScope.WhereHows, DataScope.CowTreatments, DataScope.PlannedCowTreatments,
                        DataScope.Medicines)
          .WithName("WhereHowMerge");

        return api;
    }
}
