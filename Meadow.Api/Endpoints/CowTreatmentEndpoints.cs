using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;
using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

public static class CowTreatmentEndpoints
{
    public static RouteGroupBuilder MapCowTreatmentEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/cow-treatments", async (ICowTreatmentService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Treatments.Values.ToList());
        }).WithName("CowTreatmentList");

        // GetByIdAsync und nicht der Cache-Blick aus EndpointCommon: der Dienst
        // liest selbst erst den Cache und faellt auf die Datenbank zurueck.
        //
        // "Gibt es nicht" meldet er allerdings mit einer FRISCHEN Instanz statt
        // mit null. Erkennbar ist der Fall nur daran, dass die Id der Antwort
        // nicht die angefragte ist - ein Vergleich auf 0 wuerde bei einer
        // Anfrage nach 0 selbst danebengreifen.
        api.MapGet("/cow-treatments/{treatmentId:int}", async (int treatmentId, ICowTreatmentService svc) =>
        {
            var treatment = await svc.GetByIdAsync(treatmentId);
            return treatment.CowTreatmentId == treatmentId
                ? Results.Ok(treatment)
                : EndpointCommon.NotFound("Kuhbehandlung", treatmentId);
        }).WithName("CowTreatmentById");

        // Nur InsertRangeAsync auf der Naht, also nur diese eine Schreibroute.
        // Das ist kein Mangel: der Dienst speichert die Serie in EINER
        // Transaktion, und ein Einzel-POST muesste sie darum trotzdem als Liste
        // von eins fuehren.
        //
        // EF vergibt die Ids in Einfuegereihenfolge und schreibt sie in die
        // uebergebenen Instanzen zurueck. Zurueck geht deshalb DIESELBE Liste,
        // in Anfragereihenfolge und mit gefuellten Ids.
        api.MapPost("/cow-treatments/batch", async (
            List<CowTreatment> treatments,
            ICowTreatmentService svc,
            IDbContextFactory<DatabaseContext> factory) =>
        {
            // Ein leerer Stapel quittiert im Dienst mit true, ohne etwas zu tun.
            // Als 201 mit leerem Array waere das eine Erfolgsmeldung fuer nichts,
            // und der Versionszaehler stiege ohne Aenderung.
            if (treatments.Count == 0)
            {
                return EndpointCommon.Invalid("treatments", "Der Stapel enthaelt keine Behandlung.");
            }

            Task<List<CowTreatment>> Find(DatabaseContext c, List<Guid> ids) =>
                c.CowTreatments.AsNoTracking().Where(t => ids.Contains(t.ClientId)).ToListAsync();

            var decision = await ClientIdUpsert.PrepareAsync(
                factory, treatments, t => t.ClientId, Find, "Kuhbehandlung");
            if (decision.Answer is not null) { return decision.Answer; }

            var ok = await svc.InsertRangeAsync(treatments);

            // Location auf die Sammlung: einen Stapel gibt es hinterher nicht
            // mehr als Ganzes, seine Zeilen stehen einzeln unter
            // /api/cow-treatments/{id}.
            if (ok) { return Results.Created("/api/cow-treatments", treatments); }

            return await ClientIdUpsert.RaceWinnerAsync(factory, treatments, t => t.ClientId, Find)
                   ?? EndpointCommon.WriteFailed($"Die {treatments.Count} Kuhbehandlungen konnten nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.CowTreatments).WithName("CowTreatmentCreateBatch");

        // Wortgleich zum Klauenpendant. Die Id kommt aus der Route und
        // ueberschreibt, was im Rumpf steht: sonst liesse sich mit einem
        // abweichenden Rumpf eine ANDERE Behandlung ueberschreiben, als die
        // Route benennt.
        api.MapPut("/cow-treatments/{treatmentId:int}", async (int treatmentId, CowTreatment treatment, ICowTreatmentService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Treatments, treatmentId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Kuhbehandlung", treatmentId);
            }

            treatment.CowTreatmentId = treatmentId;

            var ok = await svc.UpdateDataAsync(treatment);
            return ok
                ? Results.NoContent()
                : await EndpointCommon.WriteFailedOrGoneAsync(
                    () => svc.Treatments, treatmentId, svc.GetAllDataAsync, "Kuhbehandlung",
                    $"Kuhbehandlung {treatmentId} konnte nicht geaendert werden.");
        }).BumpsOnWrite(DataScope.CowTreatments).WithName("CowTreatmentUpdate");

        // DeleteDataAsync gibt ein blankes Task zurueck und faengt jede Ausnahme
        // in eine Logzeile ab - "geloescht", "gab es nie" und "ist
        // fehlgeschlagen" kommen hier gleich an. Die 204 ist damit eine Zusage
        // ueber den Aufruf, nicht ueber sein Ergebnis.
        //
        // Aus demselben Grund steht davor keine 404-Pruefung: sie saehe aus, als
        // koennte sie etwas garantieren, waehrend der Aufruf danach still
        // scheitern darf. So bleibt das Loeschen wenigstens ehrlich idempotent.
        api.MapDelete("/cow-treatments/{treatmentId:int}", async (int treatmentId, ICowTreatmentService svc) =>
        {
            await svc.DeleteDataAsync(treatmentId);
            return Results.NoContent();
        }).BumpsOnWrite(DataScope.CowTreatments).WithName("CowTreatmentDelete");

        return api;
    }
}
