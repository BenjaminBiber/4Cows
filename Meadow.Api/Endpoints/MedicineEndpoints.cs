using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Rumpf von POST /medicines/{medicineId}/merge. survivingName ist optional -
/// beim Zusammenfuehren wird gewaehlt, welcher der beiden Namen bleibt.
/// </summary>
public sealed record MedicineMergeRequest(int TargetId, string? SurvivingName);

/// <summary>Rumpf von POST /medicines/by-name.</summary>
public sealed record MedicineByNameRequest(string? Name);

public static class MedicineEndpoints
{
    public static RouteGroupBuilder MapMedicineEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/medicines", async (IMedicineService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Medicines.Values.ToList());
        }).WithName("MedicineList");

        api.MapGet("/medicines/usage-counts", async (IMedicineService svc) =>
        {
            var counts = await svc.GetUsageCountsAsync();
            return counts is null
                ? EndpointCommon.UsageCountsUnavailable("Medikamente")
                : Results.Ok(counts);
        }).WithName("MedicineUsageCounts");

        api.MapGet("/medicines/{medicineId:int}", async (int medicineId, IMedicineService svc) =>
        {
            var medicine = await EndpointCommon.FindAsync(() => svc.Medicines, medicineId, svc.GetAllDataAsync);
            return medicine is null ? EndpointCommon.NotFound("Medikament", medicineId) : Results.Ok(medicine);
        }).WithName("MedicineById");

        // Zurueck geht DIESELBE Instanz, die der Dienst bekommen hat: EF
        // schreibt die erzeugte Identity beim SaveChanges genau dort hinein.
        // Der deserialisierte Rumpf von vorher traegt noch die 0, und ein Blick
        // in den Cache waere ein zweiter Weg zum selben Wert, der bei jedem
        // Insert aufs Neue stimmen muesste.
        api.MapPost("/medicines", async (Medicine medicine, IMedicineService svc) =>
        {
            var ok = await svc.InsertDataAsync(medicine);
            return ok
                ? Results.Created($"/api/medicines/{medicine.MedicineId}", medicine)
                : EndpointCommon.WriteFailed($"Das Praeparat {medicine.MedicineName} konnte nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.Medicines).WithName("MedicineCreate");

        // Suchen, sonst anlegen - der Weg, den der Behandlungs-Dialog nebenbei
        // geht: wer ein Praeparat tippt, das es noch nicht gibt, legt es beim
        // Speichern mit an. Als eigener Endpunkt, weil der Client sonst erst
        // die ganze Liste holen und selbst vergleichen muesste, und zwar mit
        // GENAU derselben Trim-und-Kleinschreibung wie der Dienst.
        //
        // Der Zaehler steigt auch dann, wenn nichts angelegt wurde, sondern
        // nur gefunden: BumpsOnWrite sieht den Statuscode, nicht die Absicht.
        // Die Richtung stimmt (zu hoch statt zu niedrig), und zu hoch heisst
        // nach dem Kommentar in DataVersionEndpointFilter nur, dass ein Client
        // einmal unnoetig nachlaedt.
        api.MapPost("/medicines/by-name", async (MedicineByNameRequest body, IMedicineService svc) =>
        {
            var name = body.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return EndpointCommon.Invalid("name", "Ein Praeparat ohne Namen ist keines.");
            }

            var id = await svc.GetMedicineIdByName(name);
            return id == int.MinValue
                ? EndpointCommon.UpsertFailed("Das Praeparat", name)
                : Results.Ok(new { id });
        }).BumpsOnWrite(DataScope.Medicines).WithName("MedicineByName");

        api.MapPut("/medicines/{medicineId:int}", async (int medicineId, Medicine medicine, IMedicineService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Medicines, medicineId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Medikament", medicineId);
            }

            // Die Route bestimmt, welche Zeile geschrieben wird. Ein Rumpf mit
            // abweichender Id soll den Schreibvorgang nicht umlenken koennen.
            medicine.MedicineId = medicineId;

            var ok = await svc.UpdateDataAsync(medicine);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Medikament {medicineId} konnte nicht geaendert werden.");
        }).BumpsOnWrite(DataScope.Medicines).WithName("MedicineUpdate");

        api.MapDelete("/medicines/{medicineId:int}", async (int medicineId, IMedicineService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Medicines, medicineId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Medikament", medicineId);
            }

            var ok = await svc.RemoveByIdAsync(medicineId);
            return ok ? Results.NoContent() : EndpointCommon.StillInUse("Medikament", medicineId);
        }).BumpsOnWrite(DataScope.Medicines).WithName("MedicineDelete");

        // alsoBumps ist hier die tragende Angabe: das Verschmelzen haengt
        // Cow_Treatment UND Planned_Cow_Treatment auf die Ziel-Id um. Ohne die
        // beiden zusaetzlichen Zaehler behielte ein Client seine
        // Behandlungszeilen mit der alten Medicine_ID und merkte nichts davon -
        // der Medikamentenzaehler allein bewegt ihn nur dazu, die
        // Medikamentenliste neu zu holen.
        api.MapPost("/medicines/{medicineId:int}/merge", async (int medicineId, MedicineMergeRequest body, IMedicineService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.Medicines, medicineId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Medikament", medicineId);
            }

            if (body.TargetId == medicineId)
            {
                return EndpointCommon.Invalid("targetId", "Quelle und Ziel muessen verschieden sein.");
            }

            if (!await EndpointCommon.ExistsAsync(() => svc.Medicines, body.TargetId, svc.GetAllDataAsync))
            {
                return EndpointCommon.Invalid("targetId", $"Medikament {body.TargetId} gibt es nicht.");
            }

            var ok = await svc.MergeAsync(medicineId, body.TargetId, body.SurvivingName);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Medikament {medicineId} konnte nicht auf {body.TargetId} verschmolzen werden.");
        }).BumpsOnWrite(DataScope.Medicines, DataScope.CowTreatments, DataScope.PlannedCowTreatments)
          .WithName("MedicineMerge");

        return api;
    }
}
