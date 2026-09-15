using Meadow.Api.Infrastructure;
using Meadow.Shared.Kpi;
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
            // KpiScriptGuard lief bisher nur beim AUSFUEHREN. Ueber die Oberflaeche
            // reichte das, weil dort nichts anderes gespeichert werden konnte.
            // Ueber HTTP ist das Speichern eine eigene Tuer: ein abgelehntes Skript
            // laege sonst in der Datenbank und wartete darauf, dass jemand die
            // Kachel anschaut. Abgelehnt wird, was mehr als ein Statement ist,
            // nicht mit SELECT oder WITH beginnt, oder in eine Datei schreibt.
            var rejection = RejectScript(kpi);
            if (rejection is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["script"] = [rejection]
                });
            }

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

            var rejection = RejectScript(kpi);
            if (rejection is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["script"] = [rejection]
                });
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

    /// <summary>
    /// Der Waechter beim SPEICHERN. Die Regel selbst steht in
    /// KpiScriptGuard.RejectForKind, damit sie pruefbar ist und nicht zweimal
    /// existiert - POST und PUT brauchen sie beide.
    ///
    /// Aufgefallen ist die fehlende Fallunterscheidung erst, als der Nachweis
    /// zu Falle 6 eine Kennzahl wirklich ueber den Dialog angelegt hat. Die
    /// vier vorhandenen Baukasten-Kennzahlen tragen ein Skript, weil der Seeder
    /// es mitschreibt - an ihnen war nichts zu sehen.
    /// </summary>
    private static string? RejectScript(KPI kpi)
        => KpiScriptGuard.RejectForKind((KpiKind)kpi.Kind, kpi.Script);
}
