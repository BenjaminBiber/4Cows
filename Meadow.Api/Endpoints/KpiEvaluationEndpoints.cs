using Meadow.Shared.Kpi;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Die rechnenden Kennzahl-Endpunkte. Getrennt vom CRUD, weil hier
/// hinterlegtes SQL ausgefuehrt wird und das eine andere Betrachtung braucht.
/// </summary>
public static class KpiEvaluationEndpoints
{
    public static RouteGroupBuilder MapKpiEvaluationEndpoints(this RouteGroupBuilder api)
    {
        // Der Wert einer gespeicherten Kennzahl. Der Rumpf ist LEER - das Skript
        // kommt aus der Datenbankzeile, nie aus der Anfrage. Wer eine andere
        // Abfrage rechnen will, muss sie erst speichern, und dort greift der
        // Guard beim Schreiben.
        api.MapPost("/kpi/{kpiId:int}/value", async (int kpiId, IKPIService svc) =>
        {
            if (!await EndpointCommon.ExistsAsync(() => svc.KPIs, kpiId, svc.GetAllDataAsync))
            {
                return EndpointCommon.NotFound("Kennzahl", kpiId);
            }

            var kpi = svc.KPIs[kpiId];
            var value = await svc.GetKPIValue(kpi);

            // "--" ist der dokumentierte Fehlwert des Dienstes und bedeutet
            // "leeres Ergebnis ODER Fehler" - die beiden lassen sich auf diesem
            // Weg nicht unterscheiden, siehe den Kommentar in KPIService.
            // Unveraendert durchgereicht, damit die Kachel dasselbe zeigt wie
            // heute.
            return Results.Ok(new { kpiId, value });
        }).WithName("KpiValue");

        // Die "SQL testen"-Schaltflaeche im Kennzahl-Dialog. Der EINZIGE
        // Endpunkt, der SQL aus dem Anfragerumpf ausfuehrt.
        //
        // Deshalb doppelt verriegelt. Die Anwendung hat keine Authentifizierung,
        // und das dokumentierte Docker-Setup verbindet als root - KpiScriptGuard
        // ist das Einzige zwischen einem Rumpf und der Datenbank. Ueber die
        // Oberflaeche brauchte ein Angreifer Zugang zum Bildschirm; als Endpunkt
        // ist es eine Zeile curl von jedem Rechner, der den Port sieht.
        //
        // Aus auf der oeffentlichen Demo, und ansonsten nur an, wenn es jemand
        // bewusst einschaltet. Die Vorgabe ist aus.
        api.MapPost("/kpi/validate", async (
            ScriptValidationRequest request,
            IKPIService svc,
            IConfiguration configuration) =>
        {
            var demoEnabled = bool.TryParse(configuration["Demo:Enabled"], out var demo) && demo;
            var allowed = bool.TryParse(configuration["Kpi:AllowScriptValidation"], out var flag) && flag;

            if (demoEnabled || !allowed)
            {
                return Results.Problem(
                    title: "Skriptpruefung ist abgeschaltet",
                    detail: demoEnabled
                        ? "Im Demo-Betrieb wird kein SQL aus dem Anfragerumpf ausgefuehrt."
                        : "Kpi:AllowScriptValidation ist nicht gesetzt.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var rejection = KpiScriptGuard.Reject(request.Script);
            if (rejection is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["script"] = [rejection]
                });
            }

            // throwError: true, weil der Dialog den Grund anzeigen soll. Der
            // Kachel-Pfad oben schluckt ihn bewusst und zeigt "--".
            var probe = new KPI { Script = request.Script, Title = "Pruefung", Url = string.Empty };
            try
            {
                var value = await svc.GetKPIValue(probe, throwError: true);
                return Results.Ok(new { value });
            }
            catch (Exception ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["script"] = [ex.Message]
                });
            }
        }).WithName("KpiValidateScript");

        // Serverseitige Bequemlichkeit. Der WebAssembly-Client wird sie NICHT
        // benutzen: Builder-Kennzahlen rechnet er selbst aus seinen Caches, ueber
        // KpiRowProvider und KpiEvaluator, die beide in Meadow.Shared liegen.
        // Ginge er hierueber, kostete jede Kennzahl einen Netzaufruf, und die
        // Eigenschaft "eine deklarative Kennzahl kostet nichts" waere weg - genau
        // die, fuer die KPIService gebaut wurde.
        api.MapGet("/kpi/dashboard", async (
            IKPIService kpis,
            ICowService cows,
            IMedicineService medicines,
            IWhereHowService whereHows,
            IClawFindingService findings,
            IUdderService udders,
            ICowTreatmentService cowTreatments,
            IClawTreatmentService clawTreatments,
            IPCowTreatmentService plannedCow,
            IPClawTreatmentService plannedClaw,
            ITreatmentReasonService reasons,
            bool? addButton) =>
        {
            // Ohne diese zehn Zeilen liefert JEDE Builder-Kennzahl eine voellig
            // plausible 0 - und zwar ohne Fehler, ohne Log, ohne Hinweis.
            //
            // KpiRowProvider projiziert aus den Caches der Dienste. Im
            // Blazor-Betrieb waermt MeadowDataLoader sie bei jedem Seitenaufbau;
            // dieser Endpunkt erreicht ihn nie, weil MeadowDataLoader Scoped an
            // den Razor-Seiten haengt. Ein kalter Cache ist eine leere Zeilenmenge,
            // und COUNT ueber nichts ist 0. Live nachgestellt: der Endpunkt gab
            // display "0" bei matchedRows 0 zurueck, waehrend die Tabelle 15 Zeilen
            // hatte.
            //
            // Reihenfolge wie in MeadowDataLoader.EnsureDashboardAsync.
            await Task.WhenAll(
                cows.GetAllDataAsync(),
                medicines.GetAllDataAsync(),
                whereHows.GetAllDataAsync(),
                findings.GetAllDataAsync(),
                udders.GetAllDataAsync(),
                cowTreatments.GetAllDataAsync(),
                clawTreatments.GetAllDataAsync(),
                plannedCow.GetAllDataAsync(),
                plannedClaw.GetAllDataAsync(),
                // Zehnter Dienst, seit der Behandlungsgrund ein KPI-Filter ist. Ohne ihn traegt
                // jede Kuh-Behandlung hier den Sammelwert "Ohne Grund", und eine Kachel, die auf
                // "Mastitis" filtert, zeigte still 0.
                reasons.GetAllDataAsync());

            return Results.Ok(await kpis.GetDashboardAsync(addButton ?? true));
        }).WithName("KpiDashboard");

        return api;
    }

    public sealed record ScriptValidationRequest(string? Script);
}
