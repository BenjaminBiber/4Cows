using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

public static class PlannedCowTreatmentEndpoints
{
    public static RouteGroupBuilder MapPlannedCowTreatmentEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/planned-cow-treatments", async (IPCowTreatmentService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Treatments.Values.ToList());
        }).WithName("PlannedCowTreatmentList");

        // Der Cache statt GetById: GetById steht auf der Naht als
        // PlannedCowTreatment, liefert im Rumpf aber null - die Nullability ist
        // dort woertlich aus der Implementierung uebernommen und falsch. Ueber
        // den Cache haengt dieser Endpunkt nicht an dieser Zusage.
        api.MapGet("/planned-cow-treatments/{treatmentId:int}", async (int treatmentId, IPCowTreatmentService svc) =>
        {
            var treatment = await EndpointCommon.FindAsync(() => svc.Treatments, treatmentId, svc.GetAllDataAsync);
            return treatment is null
                ? EndpointCommon.NotFound("Geplante Kuhbehandlung", treatmentId)
                : Results.Ok(treatment);
        }).WithName("PlannedCowTreatmentById");

        // Nur InsertRangeAsync auf der Naht - wie bei den Kuhbehandlungen. EF
        // vergibt die Ids in Einfuegereihenfolge und schreibt sie in die
        // uebergebenen Instanzen; zurueck geht dieselbe Liste.
        api.MapPost("/planned-cow-treatments/batch", async (List<PlannedCowTreatment> treatments, IPCowTreatmentService svc) =>
        {
            if (treatments.Count == 0)
            {
                return EndpointCommon.Invalid("treatments", "Der Stapel enthaelt keine Planung.");
            }

            var ok = await svc.InsertRangeAsync(treatments);
            return ok
                ? Results.Created("/api/planned-cow-treatments", treatments)
                : EndpointCommon.WriteFailed($"Die {treatments.Count} geplanten Kuhbehandlungen konnten nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.PlannedCowTreatments).WithName("PlannedCowTreatmentCreateBatch");

        // Anders als bei den vier Nachschlagetabellen prueft RemoveByIDAsync
        // keine Benutzung nach - es loescht und meldet die Zeilenzahl. Ein false
        // nach einem Treffer ist deshalb eine Ausnahme oder eine Zeile, die
        // inzwischen weg ist, und keine Verwendung: 500 statt 409.
        api.MapDelete("/planned-cow-treatments/{treatmentId:int}", async (int treatmentId, IPCowTreatmentService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Treatments, treatmentId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Geplante Kuhbehandlung", treatmentId);
            }

            var ok = await svc.RemoveByIDAsync(treatmentId);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Die geplante Kuhbehandlung {treatmentId} konnte nicht geloescht werden.");
        }).BumpsOnWrite(DataScope.PlannedCowTreatments).WithName("PlannedCowTreatmentDelete");

        // Kein PUT: IPCowTreatmentService hat kein Update.
        return api;
    }
}
