using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

public static class PlannedClawTreatmentEndpoints
{
    public static RouteGroupBuilder MapPlannedClawTreatmentEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/planned-claw-treatments", async (IPClawTreatmentService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Treatments.Values.ToList());
        }).WithName("PlannedClawTreatmentList");

        // Der Cache statt GetById - dieselbe falsche Nullability wie beim
        // geplanten Kuhpendant.
        api.MapGet("/planned-claw-treatments/{treatmentId:int}", async (int treatmentId, IPClawTreatmentService svc) =>
        {
            var treatment = await EndpointCommon.FindAsync(() => svc.Treatments, treatmentId, svc.GetAllDataAsync);
            return treatment is null
                ? EndpointCommon.NotFound("Geplante Klauenbehandlung", treatmentId)
                : Results.Ok(treatment);
        }).WithName("PlannedClawTreatmentById");

        // Einzelner Insert, kein Stapel: IPClawTreatmentService hat
        // InsertDataAsync und kein InsertRangeAsync. Zurueck geht die Instanz,
        // in die EF die Identity geschrieben hat.
        api.MapPost("/planned-claw-treatments", async (PlannedClawTreatment clawTreatment, IPClawTreatmentService svc) =>
        {
            var ok = await svc.InsertDataAsync(clawTreatment);
            return ok
                ? Results.Created($"/api/planned-claw-treatments/{clawTreatment.PlannedClawTreatmentId}", clawTreatment)
                : EndpointCommon.WriteFailed("Die geplante Klauenbehandlung konnte nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.PlannedClawTreatments).WithName("PlannedClawTreatmentCreate");

        api.MapDelete("/planned-claw-treatments/{treatmentId:int}", async (int treatmentId, IPClawTreatmentService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Treatments, treatmentId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Geplante Klauenbehandlung", treatmentId);
            }

            var ok = await svc.RemoveByIDAsync(treatmentId);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Die geplante Klauenbehandlung {treatmentId} konnte nicht geloescht werden.");
        }).BumpsOnWrite(DataScope.PlannedClawTreatments).WithName("PlannedClawTreatmentDelete");

        // Kein PUT: IPClawTreatmentService hat kein Update.
        return api;
    }
}
