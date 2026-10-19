using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

public static class TreatmentPlanCancellationEndpoints
{
    public static RouteGroupBuilder MapTreatmentPlanCancellationEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/treatment-plan-cancellations", async (ITreatmentPlanCancellationService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Cancellations.Values.ToList());
        }).WithName("TreatmentPlanCancellationList");

        // Idempotent, und zwar mit 200 statt 201: ein zweites POST auf dieselbe
        // Start-ClientId liefert den BESTEHENDEN Datensatz. Der Abbruch wird auf
        // einer Kuh-Seite getippt, die mehrere Leute offen haben koennen - und
        // ein Doppeltipp darf keine zweite Zeile und keinen neuen Grund
        // erzeugen.
        api.MapPost("/treatment-plan-cancellations", async (TreatmentPlanCancellation cancellation, ITreatmentPlanCancellationService svc) =>
        {
            if (string.IsNullOrWhiteSpace(cancellation.CowId))
            {
                return EndpointCommon.Invalid("cowId", "Ein Abbruch ohne Kuh ist keiner.");
            }

            // Guid.Empty ist kein gueltiger Schluessel, sondern ein vergessener -
            // dieselbe Haltung wie in ClientIdUpsert.PrepareAsync. Durchgelassen
            // wuerde sie zum Abbruch fuer "den Durchlauf ohne erste Gabe", und
            // der zweite solche Rumpf liefe in den Index statt in diese Antwort.
            if (cancellation.StartTreatmentClientId == Guid.Empty)
            {
                return EndpointCommon.Invalid(
                    "startTreatmentClientId",
                    "Jeder Abbruch braucht die ClientId der ersten Gabe; Guid.Empty ist keine.");
            }

            var ok = await svc.CancelAsync(cancellation);
            if (!ok)
            {
                return EndpointCommon.WriteFailed(
                    $"Der Durchlauf {cancellation.StartTreatmentClientId} konnte nicht abgebrochen werden.");
            }

            // Aus dem CACHE und nicht aus dem Rumpf: bei einer Wiederholung ist
            // das der stehende Datensatz mit seinem urspruenglichen Datum, und
            // genau den soll der Aufrufer sehen.
            return svc.Cancellations.TryGetValue(cancellation.StartTreatmentClientId, out var stored)
                ? Results.Ok(stored)
                : Results.Ok(cancellation);
        }).BumpsOnWrite(DataScope.TreatmentPlans).WithName("TreatmentPlanCancellationCreate");

        // Kein Existenztest davor: RevokeAsync ist idempotent, und ein DELETE
        // auf etwas, das es nicht mehr gibt, hat sein Ziel erreicht. Eine 404
        // waere hier nur eine Einladung, sie im Client wegzufiltern.
        api.MapDelete("/treatment-plan-cancellations/{startTreatmentClientId:guid}", async (Guid startTreatmentClientId, ITreatmentPlanCancellationService svc) =>
        {
            var ok = await svc.RevokeAsync(startTreatmentClientId);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Der Abbruch des Durchlaufs {startTreatmentClientId} konnte nicht zurueckgenommen werden.");
        }).BumpsOnWrite(DataScope.TreatmentPlans).WithName("TreatmentPlanCancellationDelete");

        return api;
    }
}
