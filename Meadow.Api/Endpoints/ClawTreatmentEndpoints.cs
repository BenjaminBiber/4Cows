using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;
using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>Rumpf von POST /claw-treatments/bandages-removed.</summary>
public sealed record BandageRemovalRequest(IReadOnlyList<int>? Ids);

public static class ClawTreatmentEndpoints
{
    public static RouteGroupBuilder MapClawTreatmentEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/claw-treatments", async (IClawTreatmentService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Treatments.Values.ToList());
        }).WithName("ClawTreatmentList");

        // GetByIDAsync liest den Cache und faellt auf die Datenbank zurueck.
        // Wie beim Kuhpendant meldet er "gibt es nicht" mit einer frischen
        // Instanz, nicht mit null - deshalb der Vergleich auf die angefragte Id.
        api.MapGet("/claw-treatments/{treatmentId:int}", async (int treatmentId, IClawTreatmentService svc) =>
        {
            var treatment = await svc.GetByIDAsync(treatmentId);
            return treatment.ClawTreatmentId == treatmentId
                ? Results.Ok(treatment)
                : EndpointCommon.NotFound("Klauenbehandlung", treatmentId);
        }).WithName("ClawTreatmentById");

        // Der Fall, an dem der Cache bisher zerbrach: InsertDataAsync legt die
        // Zeile unter clawTreatment.ClawTreatmentId ab. Die Id kommt von EF und
        // steht nach dem Aufruf in DIESER Instanz - gaebe der Endpunkt den
        // deserialisierten Rumpf von vorher zurueck, stuende beim Client
        // zweimal die 0, und der zweite Insert ersetzte im Cache den ersten.
        api.MapPost("/claw-treatments", async (
            ClawTreatment clawTreatment,
            IClawTreatmentService svc,
            IDbContextFactory<DatabaseContext> factory) =>
        {
            var rows = new[] { clawTreatment };
            Task<List<ClawTreatment>> Find(DatabaseContext c, List<Guid> ids) =>
                c.ClawTreatments.AsNoTracking().Where(t => ids.Contains(t.ClientId)).ToListAsync();

            var decision = await ClientIdUpsert.PrepareAsync(
                factory, rows, t => t.ClientId, Find, "Klauenbehandlung");
            if (decision.Answer is not null) { return decision.Answer; }

            var ok = await svc.InsertDataAsync(clawTreatment);
            if (ok)
            {
                return Results.Created($"/api/claw-treatments/{clawTreatment.ClawTreatmentId}", clawTreatment);
            }

            return await ClientIdUpsert.RaceWinnerAsync(factory, rows, t => t.ClientId, Find)
                   ?? EndpointCommon.WriteFailed("Die Klauenbehandlung konnte nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.ClawTreatments).WithName("ClawTreatmentCreate");

        api.MapPut("/claw-treatments/{treatmentId:int}", async (int treatmentId, ClawTreatment clawTreatment, IClawTreatmentService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Treatments, treatmentId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Klauenbehandlung", treatmentId);
            }

            clawTreatment.ClawTreatmentId = treatmentId;

            var ok = await svc.UpdateDataAsync(clawTreatment);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Klauenbehandlung {treatmentId} konnte nicht geaendert werden.");
        }).BumpsOnWrite(DataScope.ClawTreatments).WithName("ClawTreatmentUpdate");

        // Eigene Route, weil der Verband kein Feld der Behandlung ist, das man
        // nebenbei mitschreibt: RemoveBandageAsync setzt genau ein Kennzeichen
        // und laesst den Rest der Zeile in Ruhe.
        api.MapPut("/claw-treatments/{treatmentId:int}/bandage-removed", async (int treatmentId, IClawTreatmentService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Treatments, treatmentId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Klauenbehandlung", treatmentId);
            }

            var ok = await svc.RemoveBandageAsync(treatmentId);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Der Verband an Klauenbehandlung {treatmentId} liess sich nicht als entfernt vermerken.");
        }).BumpsOnWrite(DataScope.ClawTreatments).WithName("ClawTreatmentRemoveBandage");

        // Die Mengenvariante ist keine Schleife ueber die Route darueber: der
        // Dienst legt das Kennzeichen in EINEM Update um, ein Fehler in der
        // Mitte hinterliesse sonst einen halb abgeraeumten Stapel. Sie
        // antwortet mit der Zahl der betroffenen Behandlungen - unbekannte Ids
        // zaehlen dabei nicht mit, ohne dass der Aufruf scheitert, und genau
        // deshalb ist die Zahl die Antwort und kein 204.
        api.MapPost("/claw-treatments/bandages-removed", async (BandageRemovalRequest body, IClawTreatmentService svc) =>
        {
            if (body.Ids is null || body.Ids.Count == 0)
            {
                return EndpointCommon.Invalid("ids", "Ohne Ids gibt es nichts abzunehmen.");
            }

            var removed = await svc.RemoveBandagesAsync(body.Ids);
            return Results.Ok(new { removed });
        }).BumpsOnWrite(DataScope.ClawTreatments).WithName("ClawTreatmentRemoveBandages");

        // Wie beim Kuhpendant: DeleteDataAsync gibt ein blankes Task zurueck und
        // verschluckt jeden Fehler in eine Logzeile. Die 204 sagt nur, dass der
        // Aufruf stattgefunden hat.
        api.MapDelete("/claw-treatments/{treatmentId:int}", async (int treatmentId, IClawTreatmentService svc) =>
        {
            await svc.DeleteDataAsync(treatmentId);
            return Results.NoContent();
        }).BumpsOnWrite(DataScope.ClawTreatments).WithName("ClawTreatmentDelete");

        return api;
    }
}
