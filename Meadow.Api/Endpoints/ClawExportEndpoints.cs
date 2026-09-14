using Meadow.Api.Export;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

public static class ClawExportEndpoints
{
    public static RouteGroupBuilder MapClawExportEndpoints(this RouteGroupBuilder api)
    {
        // Der Rumpf traegt die Ids der gerade SICHTBAREN Zeilen, nicht den
        // Filter. Den Filter serverseitig nachzubauen waere eine zweite Wahrheit
        // darueber, was "die aktuelle Ansicht" ist - und die faellt erst auf,
        // wenn die Datei anders aussieht als die Tabelle.
        //
        // POST und nicht GET: bei ein paar tausend Klauenbehandlungen sprengen
        // die Ids jede vertretbare URL-Laenge.
        api.MapPost("/claw-treatments/export", async (
            ClawExportRequest request,
            IClawTreatmentService clawTreatments,
            ICowService cows,
            IClawFindingService findings) =>
        {
            if (request.Ids is null || request.Ids.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ids"] = ["Es wurde keine Zeile zum Exportieren uebergeben."]
                });
            }

            await clawTreatments.GetAllDataAsync();
            await cows.GetAllDataAsync();
            await findings.GetAllDataAsync();

            var known = clawTreatments.Treatments;
            // Unbekannte Ids werden ueberprungen statt abgelehnt: zwischen dem
            // Aufbau der Tabelle und dem Klick auf Export kann jemand anders
            // eine Zeile geloescht haben. Eine 404 wuerde den ganzen Export
            // verweigern, weil eine von 800 Zeilen fehlt.
            var rows = request.Ids
                .Where(known.ContainsKey)
                .Select(id => known[id])
                .ToList();

            var bytes = ClawExcelExporter.Build(rows, cows, findings);

            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Klauenbehandlungen Export {DateTime.Now:dd-MM-yyyy}.xlsx");
        }).WithName("ClawTreatmentExport");

        return api;
    }

    public sealed record ClawExportRequest(IReadOnlyList<int>? Ids);
}
