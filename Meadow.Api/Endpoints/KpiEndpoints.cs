using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Das reine CRUD der Kennzahlentabelle.
///
/// GetKPIValue, GetDashboardAsync und alles weitere Rechnende gehoeren
/// ausdruecklich NICHT hierher: sie fuehren hinterlegtes SQL aus und brauchen
/// dafuer eine eigene Betrachtung, angefangen bei KpiScriptGuard. Hier wird
/// eine Kennzahl nur gelesen, angelegt, geaendert und geloescht.
/// </summary>
public static class KpiEndpoints
{
    public static RouteGroupBuilder MapKpiEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/kpis", async (IKPIService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.KPIs.Values.ToList());
        }).WithName("KpiList");

        api.MapGet("/kpis/{kpiId:int}", async (int kpiId, IKPIService svc) =>
        {
            var kpi = await EndpointCommon.FindAsync(() => svc.KPIs, kpiId, svc.GetAllDataAsync);
            return kpi is null ? EndpointCommon.NotFound("Kennzahl", kpiId) : Results.Ok(kpi);
        }).WithName("KpiById");

        api.MapPost("/kpis", async (KPI kpi, IKPIService svc) =>
        {
            var ok = await svc.InsertDataAsync(kpi);
            return ok
                ? Results.Created($"/api/kpis/{kpi.KPIId}", kpi)
                : EndpointCommon.WriteFailed($"Die Kennzahl {kpi.Title} konnte nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.Kpis).WithName("KpiCreate");

        api.MapPut("/kpis/{kpiId:int}", async (int kpiId, KPI kpi, IKPIService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.KPIs, kpiId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Kennzahl", kpiId);
            }

            kpi.KPIId = kpiId;

            var ok = await svc.UpdateDataAsync(kpi);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Kennzahl {kpiId} konnte nicht geaendert werden.");
        }).BumpsOnWrite(DataScope.Kpis).WithName("KpiUpdate");

        // DeleteDataAsync prueft keine Benutzung nach - eine Kennzahl haengt an
        // nichts. Ein false nach einem Treffer ist deshalb eine Ausnahme oder
        // eine Zeile, die inzwischen weg ist: 500 statt 409.
        api.MapDelete("/kpis/{kpiId:int}", async (int kpiId, IKPIService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.KPIs, kpiId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Kennzahl", kpiId);
            }

            var ok = await svc.DeleteDataAsync(kpiId);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Kennzahl {kpiId} konnte nicht geloescht werden.");
        }).BumpsOnWrite(DataScope.Kpis).WithName("KpiDelete");

        return api;
    }
}
