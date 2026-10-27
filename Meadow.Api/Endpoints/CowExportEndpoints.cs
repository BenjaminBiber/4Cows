using Meadow.Api.Export;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

public static class CowExportEndpoints
{
    public static RouteGroupBuilder MapCowExportEndpoints(this RouteGroupBuilder api)
    {
        // Wie beim Klauen-Export: der Rumpf traegt die Ids der gerade
        // SICHTBAREN Zeilen, nicht den Filter. POST wegen der Id-Menge.
        api.MapPost("/cow-treatments/export", async (
            CowExportRequest request,
            ICowTreatmentService cowTreatments,
            ICowService cows,
            IMedicineService medicines,
            IWhereHowService whereHows,
            IUdderService udders,
            ITreatmentReasonService reasons,
            ISettingsService settings) =>
        {
            if (request.Ids is null || request.Ids.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ids"] = ["Es wurde keine Zeile zum Exportieren uebergeben."]
                });
            }

            await cowTreatments.GetAllDataAsync();
            await cows.GetAllDataAsync();
            await medicines.GetAllDataAsync();
            await whereHows.GetAllDataAsync();
            await udders.GetAllDataAsync();
            await reasons.GetAllDataAsync();
            await settings.GetAllDataAsync();

            var known = cowTreatments.Treatments;
            // Unbekannte Ids werden uebersprungen statt abgelehnt - siehe
            // ClawExportEndpoints.
            var rows = request.Ids
                .Where(known.ContainsKey)
                .Select(id => known[id])
                .ToList();

            var bytes = CowExcelExporter.Build(rows, cows, medicines, whereHows, udders, reasons, settings);

            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Kuhbehandlungen Export {DateTime.Now:dd-MM-yyyy}.xlsx");
        }).WithName("CowTreatmentExport");

        return api;
    }

    public sealed record CowExportRequest(IReadOnlyList<int>? Ids);
}
