using System.ComponentModel;
using Meadow.Shared.Models;
using Meadow.Shared.Profile;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Meadow.Api.Mcp;

/// <summary>
/// Die lesenden MCP-Werkzeuge. Jedes laedt die Caches neu, bevor es
/// antwortet - siehe <see cref="McpData.LoadAllAsync"/>.
///
/// Ausgegeben wird JSON mit aufgeloesten Namen (Medikament, Wie/Wo, Befund)
/// statt nackter Ids: Claude soll eine Behandlungsliste lesen koennen, ohne
/// fuer jede Zeile die Stammdaten nachzuschlagen.
/// </summary>
[McpServerToolType]
public sealed class MeadowReadTools(McpData data)
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 1000;

    [McpServerTool(Name = "get_overview", Title = "Überblick", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Überblick über den Hof: Anzahl Kühe und Behandlungen, offene und überfällige Verbände, anstehende geplante Behandlungen und die Standardwerte. Guter Einstieg.")]
    public async Task<string> GetOverview()
    {
        await data.LoadAllAsync();
        var today = DateTime.Today;
        var reminderDays = data.Settings.BandageRemovalReminderDays;
        var claw = data.ClawTreatments.Treatments.Values.ToList();

        return McpData.ToJson(new
        {
            today = McpData.Day(today),
            cows = new
            {
                active = data.Cows.Cows.Values.Count(c => !c.IsGone),
                calves = data.Cows.Cows.Values.Count(c => !c.IsGone && c.IsCalv),
                gone = data.Cows.Cows.Values.Count(c => c.IsGone)
            },
            cowTreatments = data.CowTreatments.Treatments.Count,
            clawTreatments = claw.Count,
            openBandages = claw.Count(t => t.OpenBandages().Any()),
            overdueBandages = ClawTreatmentExtensions.OverdueBandages(claw, reminderDays, today)
                .Select(data.ClawTreatmentDto).ToList(),
            upcomingPlannedCowTreatments = data.PlannedCow.Treatments.Values
                .Where(t => !t.IsTreatet && t.AdministrationDate.Date >= today.AddDays(-7))
                .OrderBy(t => t.AdministrationDate).Take(20)
                .Select(data.PlannedCowDto).ToList(),
            upcomingPlannedClawTreatments = data.PlannedClaw.Treatments.Values
                .Where(t => t.TreatmentDate.Date >= today.AddDays(-7))
                .OrderBy(t => t.TreatmentDate).Take(20)
                .Select(data.PlannedClawDto).ToList(),
            settings = data.Settings.Settings
        });
    }

    [McpServerTool(Name = "list_cows", Title = "Kühe auflisten", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Listet Kühe mit Cow-ID, Ohrmarke, Halsbandnummer und Status (Kalb, abgegangen).")]
    public async Task<string> ListCows(
        [Description("Optionaler Suchtext: Teil der Ohrmarke, Cow-ID oder genaue Halsbandnummer.")] string? search = null,
        [Description("Auch abgegangene Tiere anzeigen.")] bool includeGone = false,
        [Description("Maximale Anzahl Zeilen (Standard 100, höchstens 1000).")] int limit = DefaultLimit)
    {
        await data.LoadAllAsync();
        var q = search?.Trim();

        var cows = data.Cows.Cows.Values
            .Where(c => includeGone || !c.IsGone)
            .Where(c => string.IsNullOrEmpty(q)
                        || c.CowId.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || (c.EarTagNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                        || c.CollarNumber.ToString() == q)
            .OrderBy(c => c.CollarNumber)
            .ToList();

        return McpData.ToJson(new
        {
            total = cows.Count,
            cows = cows.Take(Clamp(limit)).Select(data.CowDto).ToList()
        });
    }

    [McpServerTool(Name = "get_cow", Title = "Kuh im Detail", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Alles zu einer Kuh: Stammdaten, Kennzahlen der letzten 12 Monate, alle Kuh- und Klauenbehandlungen, offene Verbände und geplante Behandlungen.")]
    public async Task<string> GetCow(
        [Description("Cow-ID, Ohrmarke oder Halsbandnummer.")] string cow)
    {
        await data.LoadAllAsync();
        var c = data.RequireCow(cow);

        var profile = CowProfileBuilder.Build(
            c.CowId,
            data.CowTreatments.Treatments.Values,
            data.ClawTreatments.Treatments.Values,
            data.PlannedCow.Treatments.Values,
            data.PlannedClaw.Treatments.Values,
            data.Udders.Udder,
            DateTime.Today);

        return McpData.ToJson(new
        {
            cow = data.CowDto(c),
            summary = new
            {
                profile.TotalCowTreatments,
                profile.TotalClawTreatments,
                lastTreatment = profile.LastTreatment is { } last ? McpData.Day(last) : null,
                profile.Last12Months,
                profile.Previous12Months,
                profile.OpenBandages,
                profile.OpenPlanned,
                topMedicines = profile.Medicines.Take(5)
                    .Select(m => new { medicine = data.MedicineName(m.Id), m.Count }).ToList(),
                topFindings = profile.Findings.Take(5)
                    .Select(f => new { finding = data.FindingName(f.Id), f.Count }).ToList()
            },
            cowTreatments = profile.CowTreatments.Select(data.CowTreatmentDto).ToList(),
            clawTreatments = profile.ClawTreatments.Select(data.ClawTreatmentDto).ToList(),
            plannedCowTreatments = profile.PlannedCowTreatments.Select(data.PlannedCowDto).ToList(),
            plannedClawTreatments = profile.PlannedClawTreatments.Select(data.PlannedClawDto).ToList()
        });
    }

    [McpServerTool(Name = "list_cow_treatments", Title = "Kuhbehandlungen auflisten", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Kuhbehandlungen (Medikamentengaben), neueste zuerst. Filterbar nach Kuh, Zeitraum und Medikament.")]
    public async Task<string> ListCowTreatments(
        [Description("Optional: Cow-ID, Ohrmarke oder Halsbandnummer.")] string? cow = null,
        [Description("Optional: ab Datum (JJJJ-MM-TT), inklusive.")] string? from = null,
        [Description("Optional: bis Datum (JJJJ-MM-TT), inklusive.")] string? to = null,
        [Description("Optional: Medikament (Name oder Id).")] string? medicine = null,
        [Description("Maximale Anzahl Zeilen (Standard 100, höchstens 1000).")] int limit = DefaultLimit)
    {
        await data.LoadAllAsync();
        var cowId = string.IsNullOrWhiteSpace(cow) ? null : data.RequireCow(cow).CowId;
        var fromDate = McpData.OptionalDate(from, "from");
        var toDate = McpData.OptionalDate(to, "to");
        var medicineId = string.IsNullOrWhiteSpace(medicine) ? (int?)null : data.RequireMedicine(medicine);

        var rows = data.CowTreatments.Treatments.Values
            .Where(t => cowId is null || t.EarTagNumber == cowId)
            .Where(t => fromDate is null || t.AdministrationDate.Date >= fromDate.Value.Date)
            .Where(t => toDate is null || t.AdministrationDate.Date <= toDate.Value.Date)
            .Where(t => medicineId is null || t.MedicineId == medicineId)
            .OrderByDescending(t => t.AdministrationDate)
            .ToList();

        return McpData.ToJson(new
        {
            total = rows.Count,
            treatments = rows.Take(Clamp(limit)).Select(data.CowTreatmentDto).ToList()
        });
    }

    [McpServerTool(Name = "list_claw_treatments", Title = "Klauenbehandlungen auflisten", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Klauenbehandlungen mit Befund, Verband und Klotz je Klaue (LV, RV, LH, RH), neueste zuerst. Filterbar nach Kuh, Zeitraum und Befund.")]
    public async Task<string> ListClawTreatments(
        [Description("Optional: Cow-ID, Ohrmarke oder Halsbandnummer.")] string? cow = null,
        [Description("Optional: ab Datum (JJJJ-MM-TT), inklusive.")] string? from = null,
        [Description("Optional: bis Datum (JJJJ-MM-TT), inklusive.")] string? to = null,
        [Description("Optional: Klauenbefund (Name oder Id), an irgendeiner Klaue.")] string? finding = null,
        [Description("Maximale Anzahl Zeilen (Standard 100, höchstens 1000).")] int limit = DefaultLimit)
    {
        await data.LoadAllAsync();
        var cowId = string.IsNullOrWhiteSpace(cow) ? null : data.RequireCow(cow).CowId;
        var fromDate = McpData.OptionalDate(from, "from");
        var toDate = McpData.OptionalDate(to, "to");
        var findingId = data.OptionalFinding(finding);

        var rows = data.ClawTreatments.Treatments.Values
            .Where(t => cowId is null || t.EarTagNumber == cowId)
            .Where(t => fromDate is null || t.TreatmentDate.Date >= fromDate.Value.Date)
            .Where(t => toDate is null || t.TreatmentDate.Date <= toDate.Value.Date)
            .Where(t => findingId is null
                        || t.ClawFindingLVId == findingId || t.ClawFindingRVId == findingId
                        || t.ClawFindingLHId == findingId || t.ClawFindingRHId == findingId)
            .OrderByDescending(t => t.TreatmentDate)
            .ToList();

        return McpData.ToJson(new
        {
            total = rows.Count,
            treatments = rows.Take(Clamp(limit)).Select(data.ClawTreatmentDto).ToList()
        });
    }

    [McpServerTool(Name = "list_open_bandages", Title = "Offene Verbände", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Alle Klauenbehandlungen, bei denen noch ein Verband liegt, ältester zuerst, mit Kennzeichen 'overdue' nach der eingestellten Erinnerungsfrist.")]
    public async Task<string> ListOpenBandages()
    {
        await data.LoadAllAsync();
        var today = DateTime.Today;
        var reminderDays = data.Settings.BandageRemovalReminderDays;

        var rows = data.ClawTreatments.Treatments.Values
            .Where(t => t.OpenBandages().Any())
            .OrderBy(t => t.TreatmentDate)
            .Select(t => new
            {
                overdue = t.IsBandageOverdue(reminderDays, today),
                daysOn = (today.Date - t.TreatmentDate.Date).Days,
                treatment = data.ClawTreatmentDto(t)
            })
            .ToList();

        return McpData.ToJson(new { reminderDays, total = rows.Count, bandages = rows });
    }

    [McpServerTool(Name = "list_planned_treatments", Title = "Geplante Behandlungen", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Geplante Kuh- und Klauenbehandlungen, nach Datum sortiert.")]
    public async Task<string> ListPlannedTreatments(
        [Description("\"cow\", \"claw\" oder leer für beide.")] string? kind = null,
        [Description("Optional: Cow-ID, Ohrmarke oder Halsbandnummer.")] string? cow = null)
    {
        await data.LoadAllAsync();
        var cowId = string.IsNullOrWhiteSpace(cow) ? null : data.RequireCow(cow).CowId;
        var k = kind?.Trim().ToLowerInvariant();
        if (k is not (null or "" or "cow" or "claw"))
        {
            throw new McpException("kind muss \"cow\", \"claw\" oder leer sein.");
        }

        return McpData.ToJson(new
        {
            plannedCowTreatments = k == "claw"
                ? null
                : data.PlannedCow.Treatments.Values
                    .Where(t => cowId is null || t.EarTagNumber == cowId)
                    .OrderBy(t => t.AdministrationDate)
                    .Select(data.PlannedCowDto).ToList(),
            plannedClawTreatments = k == "cow"
                ? null
                : data.PlannedClaw.Treatments.Values
                    .Where(t => cowId is null || t.EarTagNumber == cowId)
                    .OrderBy(t => t.TreatmentDate)
                    .Select(data.PlannedClawDto).ToList()
        });
    }

    [McpServerTool(Name = "list_base_data", Title = "Basisdaten", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Stammdaten: medicines (Medikamente mit Dosiereinheit und Standard-Wie/Wo), whereHows (Verabreichungsarten), reasons (Behandlungsgründe), clawFindings (Klauenbefunde). Ohne kind alle vier.")]
    public async Task<string> ListBaseData(
        [Description("medicines, whereHows, reasons, clawFindings oder leer für alle.")] string? kind = null)
    {
        await data.LoadAllAsync();
        var k = kind?.Trim().ToLowerInvariant();
        bool Want(string name) => string.IsNullOrEmpty(k) || k == name.ToLowerInvariant();

        if (!string.IsNullOrEmpty(k) && k is not ("medicines" or "wherehows" or "reasons" or "clawfindings"))
        {
            throw new McpException("kind muss medicines, whereHows, reasons oder clawFindings sein.");
        }

        return McpData.ToJson(new
        {
            medicines = Want("medicines")
                ? data.Medicines.Medicines.Values.OrderBy(m => m.MedicineName).Select(m => new
                {
                    id = m.MedicineId,
                    name = m.MedicineName,
                    dosageUnit = m.DosageUnit,
                    defaultWhereHow = m.DefaultWhereHowId is { } w ? data.WhereHowName(w) : null
                }).ToList<object>()
                : null,
            whereHows = Want("whereHows")
                ? data.WhereHows.WhereHows.Values.OrderBy(w => w.WhereHowName)
                    .Select(w => new { id = w.WhereHowId, name = w.WhereHowName, asksForQuarters = w.ShowDialog })
                    .ToList<object>()
                : null,
            reasons = Want("reasons")
                ? data.Reasons.Reasons.Values.OrderBy(r => r.TreatmentReasonName)
                    .Select(r => new { id = r.TreatmentReasonId, name = r.TreatmentReasonName })
                    .ToList<object>()
                : null,
            clawFindings = Want("clawFindings")
                ? data.Findings.Findings.Values.OrderBy(f => f.ClawFindingName)
                    .Select(f => new { id = f.ClawFindingId, name = f.ClawFindingName })
                    .ToList<object>()
                : null
        });
    }

    [McpServerTool(Name = "get_kpis", Title = "Kennzahlen", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Die Kennzahlen (KPIs) des Dashboards mit aktuellem Wert, Vorperiode und Veränderung.")]
    public async Task<string> GetKpis()
    {
        await data.LoadAllAsync();
        await data.Kpis.GetAllDataAsync();
        var tiles = await data.Kpis.GetDashboardAsync(false);

        return McpData.ToJson(tiles.Where(t => !t.IsAddTile).Select(t => new
        {
            id = t.Kpi.KPIId,
            title = t.Kpi.Title,
            state = t.Result.State.ToString(),
            value = t.Result.Display,
            number = t.Result.Number,
            previous = t.Result.Previous,
            deltaPercent = t.Result.DeltaPercent
        }).ToList());
    }

    private static int Clamp(int limit) => Math.Clamp(limit, 1, MaxLimit);
}
