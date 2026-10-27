using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Meadow.Shared;
using Meadow.Shared.Lookups;
using Meadow.Shared.Mcp;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using ModelContextProtocol;

namespace Meadow.Api.Mcp;

/// <summary>
/// Was Lese- und Schreibwerkzeuge gemeinsam brauchen: die Dienste, das
/// Vorwaermen der Caches, Namen statt Ids und die Fehlermeldungen.
///
/// Die Werkzeuge gehen ueber DIESELBEN Singleton-Dienste wie die
/// REST-Endpunkte. Damit sehen Oberflaeche und Claude dieselben Caches, und
/// ein Schreiben ueber MCP aktualisiert sie genauso wie ein POST.
/// </summary>
public sealed class McpData(
    ISettingsService settings,
    ICowService cows,
    IMedicineService medicines,
    IWhereHowService whereHows,
    ITreatmentReasonService reasons,
    IClawFindingService findings,
    IUdderService udders,
    ICowTreatmentService cowTreatments,
    IClawTreatmentService clawTreatments,
    IPCowTreatmentService plannedCow,
    IPClawTreatmentService plannedClaw,
    IKPIService kpis)
{
    public ISettingsService Settings => settings;
    public ICowService Cows => cows;
    public IMedicineService Medicines => medicines;
    public IWhereHowService WhereHows => whereHows;
    public ITreatmentReasonService Reasons => reasons;
    public IClawFindingService Findings => findings;
    public IUdderService Udders => udders;
    public ICowTreatmentService CowTreatments => cowTreatments;
    public IClawTreatmentService ClawTreatments => clawTreatments;
    public IPCowTreatmentService PlannedCow => plannedCow;
    public IPClawTreatmentService PlannedClaw => plannedClaw;
    public IKPIService Kpis => kpis;

    private static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var options = MeadowJson.CreateOptions();
        options.WriteIndented = false;
        // Umlaute als Zeichen statt \u00FC: die Ausgabe landet nicht in HTML,
        // sondern bei Claude, und jede Escape-Folge kostet dort Tokens.
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        return options;
    }

    public static string ToJson(object value) => JsonSerializer.Serialize(value, Json);

    /// <summary>
    /// Laedt alle Caches neu. Im API-Betrieb waermt sie niemand vor (siehe
    /// EndpointCommon.FindAsync), und Claude soll den Stand der Datenbank
    /// sehen, nicht den vom Start. Nacheinander und nicht parallel: die Dienste
    /// teilen sich eine DbContextFactory - Reihenfolge wie in /api/bootstrap.
    /// </summary>
    public async Task LoadAllAsync()
    {
        await settings.GetAllDataAsync();
        await cows.GetAllDataAsync();
        await medicines.GetAllDataAsync();
        await whereHows.GetAllDataAsync();
        await reasons.GetAllDataAsync();
        await findings.GetAllDataAsync();
        await udders.GetAllDataAsync();
        await cowTreatments.GetAllDataAsync();
        await clawTreatments.GetAllDataAsync();
        await plannedCow.GetAllDataAsync();
        await plannedClaw.GetAllDataAsync();
    }

    // ---- Aufloesen ------------------------------------------------------

    public Cow RequireCow(string? query)
        => McpLookup.ResolveCow(cows.Cows, query)
           ?? throw new McpException($"Keine Kuh mit Cow-ID, Ohrmarke oder Halsbandnummer \"{query}\" gefunden. Tipp: list_cows durchsuchen.");

    public int RequireMedicine(string? query)
        => McpLookup.ResolveId(medicines.Medicines, query, m => m.MedicineName)
           ?? throw new McpException($"Medikament \"{query}\" ist unbekannt. Vorhandene Namen liefert list_base_data(kind: \"medicines\"); neue Medikamente legt add_base_data an.");

    public int RequireWhereHow(string? query)
        => McpLookup.ResolveId(whereHows.WhereHows, query, w => w.WhereHowName)
           ?? throw new McpException($"Wie/Wo \"{query}\" ist unbekannt. Vorhandene Werte liefert list_base_data(kind: \"whereHows\").");

    public int? OptionalReason(string? query)
        => string.IsNullOrWhiteSpace(query)
            ? null
            : McpLookup.ResolveId(reasons.Reasons, query, r => r.TreatmentReasonName)
              ?? throw new McpException($"Behandlungsgrund \"{query}\" ist unbekannt. Vorhandene Gruende liefert list_base_data(kind: \"reasons\").");

    public int? OptionalFinding(string? query)
        => string.IsNullOrWhiteSpace(query)
            ? null
            : McpLookup.ResolveId(findings.Findings, query, f => f.ClawFindingName)
              ?? throw new McpException($"Klauenbefund \"{query}\" ist unbekannt. Vorhandene Befunde liefert list_base_data(kind: \"clawFindings\").");

    public static IReadOnlySet<HoofPosition> RequirePositions(string? positions, string what)
        => McpLookup.ParsePositions(positions)
           ?? throw new McpException($"{what}: \"{positions}\" ist keine gueltige Angabe. Erlaubt sind LV, RV, LH, RH (kommagetrennt).");

    public static DateTime RequireDate(string? value, string field)
    {
        var raw = value?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            throw new McpException($"{field} fehlt (Format JJJJ-MM-TT).");
        }

        string[] formats = { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "dd.MM.yyyy", "d.M.yyyy" };
        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date;
        }

        throw new McpException($"{field} \"{value}\" ist kein Datum (Format JJJJ-MM-TT).");
    }

    public static DateTime? OptionalDate(string? value, string field)
        => string.IsNullOrWhiteSpace(value) ? null : RequireDate(value, field);

    /// <summary>
    /// Die Euter-Zeile fuer eine Viertelkombination. GetIDByBools legt eine
    /// fehlende Kombination an - genau wie der Behandlungs-Dialog.
    /// </summary>
    public async Task<int> UdderIdAsync(IReadOnlySet<HoofPosition> quarters)
    {
        var id = quarters.Count == 0
            ? await udders.GetIdForNoQuarters()
            : await udders.GetIDByBools(new Udder(0,
                quarters.Contains(HoofPosition.LV), quarters.Contains(HoofPosition.LH),
                quarters.Contains(HoofPosition.RV), quarters.Contains(HoofPosition.RH)));

        if (id == int.MinValue)
        {
            throw new McpException("Die Euterviertel-Angabe konnte nicht gespeichert werden (siehe Serverprotokoll).");
        }

        return id;
    }

    // ---- Anzeigen -------------------------------------------------------

    public string CowLabel(string? cowId) => CowLookups.GetDisplayLabel(cows.Cows, cowId);

    public string MedicineName(int id) => medicines.GetMedicineNameById(id);

    public string WhereHowName(int id) => whereHows.GetWhereHowNameById(id);

    public string? ReasonName(int? id) => id is null ? null : reasons.GetNameById(id);

    public string? FindingName(int? id) => id is null ? null : findings.GetNameById(id);

    public string Quarters(int udderId)
    {
        if (!udders.Udder.TryGetValue(udderId, out var u))
        {
            return string.Empty;
        }

        var list = new List<string>();
        if (u.QuarterLV) list.Add("LV");
        if (u.QuarterRV) list.Add("RV");
        if (u.QuarterLH) list.Add("LH");
        if (u.QuarterRH) list.Add("RH");
        return string.Join(",", list);
    }

    public static string Day(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public object CowDto(Cow c) => new
    {
        cowId = c.CowId,
        earTag = c.EarTagNumber,
        collarNumber = c.CollarNumber,
        isCalf = c.IsCalv,
        isGone = c.IsGone,
        label = CowLabel(c.CowId)
    };

    public object CowTreatmentDto(CowTreatment t) => new
    {
        id = t.CowTreatmentId,
        cowId = t.EarTagNumber,
        cow = CowLabel(t.EarTagNumber),
        date = Day(t.AdministrationDate),
        medicine = MedicineName(t.MedicineId),
        medicineId = t.MedicineId,
        dosage = t.MedicineDosage,
        dosageUnit = medicines.GetDosageUnit(t.MedicineId, settings.DefaultDosageUnit),
        whereHow = WhereHowName(t.WhereHowId),
        quarters = Quarters(t.UdderId),
        reason = ReasonName(t.TreatmentReasonId)
    };

    public object ClawTreatmentDto(ClawTreatment t) => new
    {
        id = t.ClawTreatmentId,
        cowId = t.EarTagNumber,
        cow = CowLabel(t.EarTagNumber),
        date = Day(t.TreatmentDate),
        claws = HoofPositions.All.Select(p => new
        {
            position = p.ToString(),
            finding = FindingName(p switch
            {
                HoofPosition.LV => t.ClawFindingLVId,
                HoofPosition.RV => t.ClawFindingRVId,
                HoofPosition.LH => t.ClawFindingLHId,
                _ => t.ClawFindingRHId
            }),
            bandage = t.GetBandage(p),
            block = p switch
            {
                HoofPosition.LV => t.BlockLV,
                HoofPosition.RV => t.BlockRV,
                HoofPosition.LH => t.BlockLH,
                _ => t.BlockRH
            }
        }).Where(c => c.finding is not null || c.bandage || c.block).ToList(),
        openBandages = t.OpenBandages().Select(p => p.ToString()).ToList(),
        isBandageRemoved = t.IsBandageRemoved
    };

    public object PlannedCowDto(PlannedCowTreatment t) => new
    {
        id = t.PlannedCowTreatmentId,
        cowId = t.EarTagNumber,
        cow = CowLabel(t.EarTagNumber),
        date = Day(t.AdministrationDate),
        medicine = MedicineName(t.MedicineId),
        dosage = t.MedicineDosage,
        whereHow = WhereHowName(t.WhereHowId),
        quarters = Quarters(t.UdderId),
        reason = ReasonName(t.TreatmentReasonId),
        isFound = t.IsFound,
        isTreated = t.IsTreatet
    };

    public object PlannedClawDto(PlannedClawTreatment t) => new
    {
        id = t.PlannedClawTreatmentId,
        cowId = t.EarTagNumber,
        cow = CowLabel(t.EarTagNumber),
        date = Day(t.TreatmentDate),
        description = t.Desciption,
        claws = HoofPositions.All.Where(t.GetFlag).Select(p => p.ToString()).ToList()
    };
}
