using System.ComponentModel;
using Meadow.Api.Infrastructure;
using Meadow.Shared.Mcp;
using Meadow.Shared.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Meadow.Api.Mcp;

/// <summary>
/// Die schreibenden MCP-Werkzeuge: anlegen und aendern, NICHT loeschen.
/// Loeschen bleibt bewusst der Oberflaeche vorbehalten, wo davor ein
/// Bestaetigungsdialog steht.
///
/// Nach jedem erfolgreichen Schreiben steigt der Versionszaehler des
/// betroffenen Bereichs - dasselbe, was BumpsOnWrite an den REST-Endpunkten
/// tut. Ohne das saehe ein offenes Tablet die von Claude angelegte
/// Behandlung erst nach einem Neuladen.
/// </summary>
[McpServerToolType]
public sealed class MeadowWriteTools(McpData data, IDataVersion version)
{
    private const int MaxSeriesDays = 60;
    private const string ClearValue = "-";

    // ---- Kuhbehandlungen ------------------------------------------------

    [McpServerTool(Name = "add_cow_treatment", Title = "Kuhbehandlung erfassen", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Erfasst eine Medikamentengabe an einer Kuh. Mit repeatDays > 1 wird eine Serie an aufeinanderfolgenden Tagen angelegt (z. B. 3 Tage Antibiotikum). Gibt die angelegten Behandlungen mit Id zurück.")]
    public async Task<string> AddCowTreatment(
        [Description("Cow-ID, Ohrmarke oder Halsbandnummer.")] string cow,
        [Description("Datum der (ersten) Gabe, JJJJ-MM-TT.")] string date,
        [Description("Medikament (Name oder Id), muss in den Basisdaten existieren.")] string medicine,
        [Description("Dosis in der Dosiereinheit des Medikaments.")] float dosage,
        [Description("Wie/Wo (Name oder Id). Leer = Standard-Wie/Wo des Medikaments.")] string? whereHow = null,
        [Description("Behandelte Euterviertel, z. B. \"LV,RH\". Leer = keine Viertelangabe.")] string? quarters = null,
        [Description("Optional: Behandlungsgrund (Name oder Id).")] string? reason = null,
        [Description("Anzahl aufeinanderfolgender Tage (Standard 1, höchstens 60).")] int repeatDays = 1)
    {
        await data.LoadAllAsync();
        var c = data.RequireCow(cow);
        var start = McpData.RequireDate(date, "date");
        var medicineId = data.RequireMedicine(medicine);
        var whereHowId = ResolveWhereHowFor(medicineId, whereHow);
        var reasonId = data.OptionalReason(reason);
        var quarterSet = McpData.RequirePositions(quarters, "quarters");
        RequirePositive(dosage, "dosage");

        if (repeatDays is < 1 or > MaxSeriesDays)
        {
            throw new McpException($"repeatDays muss zwischen 1 und {MaxSeriesDays} liegen.");
        }

        var udderId = await data.UdderIdAsync(quarterSet);
        version.Bump(DataScope.Udders);

        var rows = Enumerable.Range(0, repeatDays)
            .Select(i => new CowTreatment(0, c.CowId, medicineId, start.AddDays(i), dosage, whereHowId, udderId, reasonId))
            .ToList();

        if (!await data.CowTreatments.InsertRangeAsync(rows))
        {
            throw new McpException("Die Kuhbehandlung konnte nicht gespeichert werden (Grund im Serverprotokoll).");
        }

        version.Bump(DataScope.CowTreatments);
        return McpData.ToJson(new { created = rows.Select(data.CowTreatmentDto).ToList() });
    }

    [McpServerTool(Name = "update_cow_treatment", Title = "Kuhbehandlung ändern", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Ändert eine bestehende Kuhbehandlung. Nur angegebene Felder werden geändert. Für den Behandlungsgrund entfernt \"-\" den Wert.")]
    public async Task<string> UpdateCowTreatment(
        [Description("Id der Kuhbehandlung (aus list_cow_treatments oder get_cow).")] int id,
        [Description("Optional: neue Kuh (Cow-ID, Ohrmarke oder Halsbandnummer).")] string? cow = null,
        [Description("Optional: neues Datum, JJJJ-MM-TT.")] string? date = null,
        [Description("Optional: neues Medikament (Name oder Id).")] string? medicine = null,
        [Description("Optional: neue Dosis.")] float? dosage = null,
        [Description("Optional: neues Wie/Wo (Name oder Id).")] string? whereHow = null,
        [Description("Optional: neue Euterviertel, z. B. \"LV,RH\"; \"-\" = keine.")] string? quarters = null,
        [Description("Optional: neuer Behandlungsgrund; \"-\" entfernt ihn.")] string? reason = null)
    {
        await data.LoadAllAsync();
        if (!data.CowTreatments.Treatments.TryGetValue(id, out var existing))
        {
            throw new McpException($"Kuhbehandlung {id} gibt es nicht.");
        }

        var t = existing.Copy();
        if (cow is not null) t.EarTagNumber = data.RequireCow(cow).CowId;
        if (date is not null) t.AdministrationDate = McpData.RequireDate(date, "date");
        if (medicine is not null) t.MedicineId = data.RequireMedicine(medicine);
        if (dosage is { } d) { RequirePositive(d, "dosage"); t.MedicineDosage = d; }
        if (whereHow is not null) t.WhereHowId = data.RequireWhereHow(whereHow);
        if (quarters is not null)
        {
            t.UdderId = await data.UdderIdAsync(quarters.Trim() == ClearValue
                ? new HashSet<HoofPosition>()
                : McpData.RequirePositions(quarters, "quarters"));
            version.Bump(DataScope.Udders);
        }
        if (reason is not null) t.TreatmentReasonId = reason.Trim() == ClearValue ? null : data.OptionalReason(reason);

        if (!await data.CowTreatments.UpdateDataAsync(t))
        {
            throw new McpException($"Kuhbehandlung {id} konnte nicht geändert werden (Grund im Serverprotokoll).");
        }

        version.Bump(DataScope.CowTreatments);
        return McpData.ToJson(new { updated = data.CowTreatmentDto(t) });
    }

    // ---- Klauenbehandlungen ---------------------------------------------

    [McpServerTool(Name = "add_claw_treatment", Title = "Klauenbehandlung erfassen", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Erfasst eine Klauenbehandlung mit Befund je Klaue (LV, RV, LH, RH) sowie Verband und Klotz. Klauen ohne Angabe bleiben leer.")]
    public async Task<string> AddClawTreatment(
        [Description("Cow-ID, Ohrmarke oder Halsbandnummer.")] string cow,
        [Description("Behandlungsdatum, JJJJ-MM-TT.")] string date,
        [Description("Optional: Befund links vorne (Name oder Id des Klauenbefunds).")] string? findingLV = null,
        [Description("Optional: Befund rechts vorne.")] string? findingRV = null,
        [Description("Optional: Befund links hinten.")] string? findingLH = null,
        [Description("Optional: Befund rechts hinten.")] string? findingRH = null,
        [Description("Klauen mit Verband, z. B. \"LH,RH\".")] string? bandages = null,
        [Description("Klauen mit Klotz, z. B. \"LH\".")] string? blocks = null)
    {
        await data.LoadAllAsync();
        var c = data.RequireCow(cow);
        var bandageSet = McpData.RequirePositions(bandages, "bandages");
        var blockSet = McpData.RequirePositions(blocks, "blocks");

        var t = new ClawTreatment(0, c.CowId, McpData.RequireDate(date, "date"),
            data.OptionalFinding(findingLV), bandageSet.Contains(HoofPosition.LV), blockSet.Contains(HoofPosition.LV),
            data.OptionalFinding(findingLH), bandageSet.Contains(HoofPosition.LH), blockSet.Contains(HoofPosition.LH),
            data.OptionalFinding(findingRV), bandageSet.Contains(HoofPosition.RV), blockSet.Contains(HoofPosition.RV),
            data.OptionalFinding(findingRH), bandageSet.Contains(HoofPosition.RH), blockSet.Contains(HoofPosition.RH),
            isBandageRemoved: false);

        if (!await data.ClawTreatments.InsertDataAsync(t))
        {
            throw new McpException("Die Klauenbehandlung konnte nicht gespeichert werden (Grund im Serverprotokoll).");
        }

        version.Bump(DataScope.ClawTreatments);
        return McpData.ToJson(new { created = data.ClawTreatmentDto(t) });
    }

    [McpServerTool(Name = "update_claw_treatment", Title = "Klauenbehandlung ändern", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Ändert eine bestehende Klauenbehandlung. Nur angegebene Felder werden geändert; \"-\" entfernt einen Befund. bandages/blocks ersetzen die bisherige Auswahl vollständig.")]
    public async Task<string> UpdateClawTreatment(
        [Description("Id der Klauenbehandlung.")] int id,
        [Description("Optional: neues Datum, JJJJ-MM-TT.")] string? date = null,
        [Description("Optional: Befund links vorne; \"-\" = keiner.")] string? findingLV = null,
        [Description("Optional: Befund rechts vorne; \"-\" = keiner.")] string? findingRV = null,
        [Description("Optional: Befund links hinten; \"-\" = keiner.")] string? findingLH = null,
        [Description("Optional: Befund rechts hinten; \"-\" = keiner.")] string? findingRH = null,
        [Description("Optional: Klauen mit Verband, z. B. \"LH,RH\"; \"-\" = keine.")] string? bandages = null,
        [Description("Optional: Klauen mit Klotz; \"-\" = keine.")] string? blocks = null)
    {
        await data.LoadAllAsync();
        if (!data.ClawTreatments.Treatments.TryGetValue(id, out var existing))
        {
            throw new McpException($"Klauenbehandlung {id} gibt es nicht.");
        }

        var t = existing.Copy();
        if (date is not null) t.TreatmentDate = McpData.RequireDate(date, "date");
        if (findingLV is not null) t.ClawFindingLVId = Finding(findingLV);
        if (findingRV is not null) t.ClawFindingRVId = Finding(findingRV);
        if (findingLH is not null) t.ClawFindingLHId = Finding(findingLH);
        if (findingRH is not null) t.ClawFindingRHId = Finding(findingRH);

        if (bandages is not null)
        {
            var set = Positions(bandages, "bandages");
            foreach (var p in HoofPositions.All)
            {
                t.SetBandage(p, set.Contains(p));
            }
        }

        if (blocks is not null)
        {
            var set = Positions(blocks, "blocks");
            t.BlockLV = set.Contains(HoofPosition.LV);
            t.BlockRV = set.Contains(HoofPosition.RV);
            t.BlockLH = set.Contains(HoofPosition.LH);
            t.BlockRH = set.Contains(HoofPosition.RH);
        }

        if (!await data.ClawTreatments.UpdateDataAsync(t))
        {
            throw new McpException($"Klauenbehandlung {id} konnte nicht geändert werden (Grund im Serverprotokoll).");
        }

        version.Bump(DataScope.ClawTreatments);
        return McpData.ToJson(new { updated = data.ClawTreatmentDto(t) });

        int? Finding(string value) => value.Trim() == ClearValue ? null : data.OptionalFinding(value);
    }

    [McpServerTool(Name = "remove_bandages", Title = "Verbände abnehmen", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Vermerkt bei den angegebenen Klauenbehandlungen, dass der Verband abgenommen wurde (gilt für alle Klauen der Behandlung).")]
    public async Task<string> RemoveBandages(
        [Description("Ids der Klauenbehandlungen (aus list_open_bandages).")] int[] ids)
    {
        if (ids.Length == 0)
        {
            throw new McpException("Ohne Ids gibt es nichts abzunehmen.");
        }

        await data.LoadAllAsync();
        var unknown = ids.Where(i => !data.ClawTreatments.Treatments.ContainsKey(i)).ToList();
        if (unknown.Count > 0)
        {
            throw new McpException($"Diese Klauenbehandlungen gibt es nicht: {string.Join(", ", unknown)}.");
        }

        var count = await data.ClawTreatments.RemoveBandagesAsync(ids.Distinct().ToList());
        if (count > 0)
        {
            version.Bump(DataScope.ClawTreatments);
        }

        return McpData.ToJson(new { removed = count, requested = ids.Distinct().Count() });
    }

    // ---- Geplante Behandlungen ------------------------------------------

    [McpServerTool(Name = "add_planned_cow_treatment", Title = "Kuhbehandlung planen", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Plant eine Medikamentengabe für ein Datum (z. B. Trockenstellen, Nachbehandlung).")]
    public async Task<string> AddPlannedCowTreatment(
        [Description("Cow-ID, Ohrmarke oder Halsbandnummer.")] string cow,
        [Description("Geplantes Datum, JJJJ-MM-TT.")] string date,
        [Description("Medikament (Name oder Id).")] string medicine,
        [Description("Dosis.")] float dosage,
        [Description("Wie/Wo (Name oder Id). Leer = Standard des Medikaments.")] string? whereHow = null,
        [Description("Euterviertel, z. B. \"LV,RH\".")] string? quarters = null,
        [Description("Optional: Behandlungsgrund.")] string? reason = null)
    {
        await data.LoadAllAsync();
        var c = data.RequireCow(cow);
        var when = McpData.RequireDate(date, "date");
        var medicineId = data.RequireMedicine(medicine);
        var whereHowId = ResolveWhereHowFor(medicineId, whereHow);
        var reasonId = data.OptionalReason(reason);
        var quarterSet = McpData.RequirePositions(quarters, "quarters");
        RequirePositive(dosage, "dosage");

        var udderId = await data.UdderIdAsync(quarterSet);
        version.Bump(DataScope.Udders);

        var t = new PlannedCowTreatment(0, c.CowId, medicineId, when, dosage, whereHowId,
            isFound: false, isTreatet: false, udderId, reasonId);

        if (!await data.PlannedCow.InsertRangeAsync(new[] { t }))
        {
            throw new McpException("Die geplante Kuhbehandlung konnte nicht gespeichert werden (Grund im Serverprotokoll).");
        }

        version.Bump(DataScope.PlannedCowTreatments);
        return McpData.ToJson(new { created = data.PlannedCowDto(t) });
    }

    [McpServerTool(Name = "add_planned_claw_treatment", Title = "Klauenbehandlung planen", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Plant eine Klauenbehandlung (z. B. Kontrolle) für bestimmte Klauen und ein Datum.")]
    public async Task<string> AddPlannedClawTreatment(
        [Description("Cow-ID, Ohrmarke oder Halsbandnummer.")] string cow,
        [Description("Geplantes Datum, JJJJ-MM-TT.")] string date,
        [Description("Betroffene Klauen, z. B. \"LH,RH\".")] string? claws = null,
        [Description("Optional: Beschreibung/Notiz.")] string? description = null)
    {
        await data.LoadAllAsync();
        var c = data.RequireCow(cow);
        var set = McpData.RequirePositions(claws, "claws");

        var t = new PlannedClawTreatment(0, c.CowId, McpData.RequireDate(date, "date"),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            set.Contains(HoofPosition.LV), set.Contains(HoofPosition.LH),
            set.Contains(HoofPosition.RV), set.Contains(HoofPosition.RH));

        if (!await data.PlannedClaw.InsertDataAsync(t))
        {
            throw new McpException("Die geplante Klauenbehandlung konnte nicht gespeichert werden (Grund im Serverprotokoll).");
        }

        version.Bump(DataScope.PlannedClawTreatments);
        return McpData.ToJson(new { created = data.PlannedClawDto(t) });
    }

    // ---- Kuehe ----------------------------------------------------------

    [McpServerTool(Name = "update_cow", Title = "Kuh ändern", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Ändert Stammdaten einer Kuh: Halsbandnummer, Abgang (isGone) oder – bei einem Kalb – die Ohrmarke zuweisen. Hinweis: der XLink-Abgleich kann Halsband und Abgang später überschreiben.")]
    public async Task<string> UpdateCow(
        [Description("Cow-ID, Ohrmarke oder Halsbandnummer.")] string cow,
        [Description("Optional: neue Halsbandnummer.")] int? collarNumber = null,
        [Description("Optional: true = abgegangen, false = wieder im Bestand.")] bool? isGone = null,
        [Description("Optional: Ohrmarke für ein Kalb (macht aus dem Kalb eine Kuh).")] string? earTag = null)
    {
        await data.LoadAllAsync();
        var c = data.RequireCow(cow);

        if (collarNumber is { } collar && collar != c.CollarNumber)
        {
            if (collar <= 0)
            {
                throw new McpException("Die Halsbandnummer muss positiv sein.");
            }

            if (data.Cows.IsCollarInUse(collar))
            {
                throw new McpException($"Halsbandnummer {collar} ist bereits vergeben.");
            }

            if (!await data.Cows.UpdateCollarNumberAsync(c.CowId, collar))
            {
                throw new McpException("Die Halsbandnummer konnte nicht geändert werden (Grund im Serverprotokoll).");
            }
        }

        if (isGone is { } gone && gone != c.IsGone
            && !await data.Cows.UpdateIsGoneAsync(c.CowId, gone))
        {
            throw new McpException("Der Abgangsvermerk konnte nicht geändert werden (Grund im Serverprotokoll).");
        }

        if (!string.IsNullOrWhiteSpace(earTag))
        {
            var tag = earTag.Trim();
            if (!c.IsCalv && !string.IsNullOrWhiteSpace(c.EarTagNumber))
            {
                throw new McpException($"Kuh {c.EarTagNumber} hat bereits eine Ohrmarke; nur Kälbern kann eine zugewiesen werden.");
            }

            if (data.Cows.Cows.Values.Any(x => string.Equals(x.EarTagNumber, tag, StringComparison.OrdinalIgnoreCase)))
            {
                throw new McpException($"Ohrmarke {tag} ist bereits vergeben.");
            }

            if (!await data.Cows.PromoteCalfAsync(c.CowId, tag))
            {
                throw new McpException("Die Ohrmarke konnte nicht zugewiesen werden (Grund im Serverprotokoll).");
            }
        }

        version.Bump(DataScope.Cows);
        await data.Cows.GetAllDataAsync();
        return McpData.ToJson(new { updated = data.CowDto(data.Cows.Cows[c.CowId]) });
    }

    // ---- Basisdaten -----------------------------------------------------

    [McpServerTool(Name = "add_base_data", Title = "Basisdaten anlegen", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Legt einen Stammdatensatz an: medicine, whereHow, reason oder clawFinding. Bei medicine optional Dosiereinheit und Standard-Wie/Wo.")]
    public async Task<string> AddBaseData(
        [Description("medicine, whereHow, reason oder clawFinding.")] string kind,
        [Description("Name des neuen Eintrags.")] string name,
        [Description("Nur medicine: Dosiereinheit, z. B. \"ml\" oder \"Tube\".")] string? dosageUnit = null,
        [Description("Nur medicine: Standard-Wie/Wo (Name oder Id).")] string? defaultWhereHow = null,
        [Description("Nur whereHow: fragt der Dialog nach Euterviertel? (Standard true)")] bool asksForQuarters = true)
    {
        await data.LoadAllAsync();
        var n = name?.Trim();
        if (string.IsNullOrEmpty(n))
        {
            throw new McpException("Ein Eintrag ohne Namen ist keiner.");
        }

        switch (NormalizeKind(kind))
        {
            case "medicine":
            {
                RequireNew(McpLookup.ResolveId(data.Medicines.Medicines, n, m => m.MedicineName), "Medikament", n);
                RequireLength(n, Medicine.NameMaxLength);
                var m = new Medicine(0, n)
                {
                    DosageUnit = Unit(dosageUnit),
                    DefaultWhereHowId = string.IsNullOrWhiteSpace(defaultWhereHow) ? null : data.RequireWhereHow(defaultWhereHow)
                };
                Require(await data.Medicines.InsertDataAsync(m), "Das Medikament");
                version.Bump(DataScope.Medicines);
                return McpData.ToJson(new { created = new { kind = "medicine", id = m.MedicineId, name = m.MedicineName } });
            }
            case "wherehow":
            {
                RequireNew(McpLookup.ResolveId(data.WhereHows.WhereHows, n, w => w.WhereHowName), "Wie/Wo", n);
                var w = new WhereHow(0, n, asksForQuarters);
                Require(await data.WhereHows.InsertDataAsync(w), "Das Wie/Wo");
                version.Bump(DataScope.WhereHows);
                return McpData.ToJson(new { created = new { kind = "whereHow", id = w.WhereHowId, name = w.WhereHowName } });
            }
            case "reason":
            {
                RequireNew(McpLookup.ResolveId(data.Reasons.Reasons, n, r => r.TreatmentReasonName), "Behandlungsgrund", n);
                RequireLength(n, 64);
                var r = new TreatmentReason(0, n);
                Require(await data.Reasons.InsertDataAsync(r), "Der Behandlungsgrund");
                version.Bump(DataScope.TreatmentReasons);
                return McpData.ToJson(new { created = new { kind = "reason", id = r.TreatmentReasonId, name = r.TreatmentReasonName } });
            }
            default:
            {
                RequireNew(McpLookup.ResolveId(data.Findings.Findings, n, f => f.ClawFindingName), "Klauenbefund", n);
                RequireLength(n, ClawFinding.NameMaxLength);
                var f = new ClawFinding(0, n);
                Require(await data.Findings.InsertDataAsync(f), "Der Klauenbefund");
                version.Bump(DataScope.ClawFindings);
                return McpData.ToJson(new { created = new { kind = "clawFinding", id = f.ClawFindingId, name = f.ClawFindingName } });
            }
        }
    }

    [McpServerTool(Name = "update_base_data", Title = "Basisdaten ändern", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Ändert einen Stammdatensatz (umbenennen; bei medicine auch Dosiereinheit und Standard-Wie/Wo). Die Änderung wirkt auf alle Behandlungen, die den Eintrag verwenden.")]
    public async Task<string> UpdateBaseData(
        [Description("medicine, whereHow, reason oder clawFinding.")] string kind,
        [Description("Bisheriger Name oder Id.")] string item,
        [Description("Optional: neuer Name.")] string? newName = null,
        [Description("Nur medicine: neue Dosiereinheit; \"-\" entfernt sie.")] string? dosageUnit = null,
        [Description("Nur medicine: neues Standard-Wie/Wo; \"-\" entfernt es.")] string? defaultWhereHow = null,
        [Description("Nur whereHow: fragt der Dialog nach Euterviertel?")] bool? asksForQuarters = null)
    {
        await data.LoadAllAsync();
        var rename = string.IsNullOrWhiteSpace(newName) ? null : newName.Trim();

        switch (NormalizeKind(kind))
        {
            case "medicine":
            {
                var id = data.RequireMedicine(item);
                var src = data.Medicines.Medicines[id];
                var m = new Medicine(id, rename ?? src.MedicineName)
                {
                    DosageUnit = dosageUnit is null ? src.DosageUnit : dosageUnit.Trim() == ClearValue ? null : Unit(dosageUnit),
                    DefaultWhereHowId = defaultWhereHow is null ? src.DefaultWhereHowId
                        : defaultWhereHow.Trim() == ClearValue ? null : data.RequireWhereHow(defaultWhereHow)
                };
                if (rename is not null) { RequireRename(McpLookup.ResolveId(data.Medicines.Medicines, rename, x => x.MedicineName), id, "Medikament", rename); RequireLength(rename, Medicine.NameMaxLength); }
                Require(await data.Medicines.UpdateDataAsync(m), "Das Medikament");
                version.Bump(DataScope.Medicines);
                return McpData.ToJson(new { updated = new { kind = "medicine", id, name = m.MedicineName, m.DosageUnit } });
            }
            case "wherehow":
            {
                var id = data.RequireWhereHow(item);
                var src = data.WhereHows.WhereHows[id];
                if (rename is not null) RequireRename(McpLookup.ResolveId(data.WhereHows.WhereHows, rename, x => x.WhereHowName), id, "Wie/Wo", rename);
                var w = new WhereHow(id, rename ?? src.WhereHowName, asksForQuarters ?? src.ShowDialog);
                Require(await data.WhereHows.UpdateDataAsync(w), "Das Wie/Wo");
                version.Bump(DataScope.WhereHows);
                return McpData.ToJson(new { updated = new { kind = "whereHow", id, name = w.WhereHowName } });
            }
            case "reason":
            {
                var id = data.OptionalReason(item) ?? throw new McpException("item fehlt.");
                if (rename is null) throw new McpException("Für einen Behandlungsgrund lässt sich nur der Name ändern – newName fehlt.");
                RequireRename(McpLookup.ResolveId(data.Reasons.Reasons, rename, x => x.TreatmentReasonName), id, "Behandlungsgrund", rename);
                RequireLength(rename, 64);
                Require(await data.Reasons.UpdateDataAsync(new TreatmentReason(id, rename)), "Der Behandlungsgrund");
                version.Bump(DataScope.TreatmentReasons);
                return McpData.ToJson(new { updated = new { kind = "reason", id, name = rename } });
            }
            default:
            {
                var id = data.OptionalFinding(item) ?? throw new McpException("item fehlt.");
                if (rename is null) throw new McpException("Für einen Klauenbefund lässt sich nur der Name ändern – newName fehlt.");
                RequireRename(McpLookup.ResolveId(data.Findings.Findings, rename, x => x.ClawFindingName), id, "Klauenbefund", rename);
                RequireLength(rename, ClawFinding.NameMaxLength);
                Require(await data.Findings.UpdateDataAsync(new ClawFinding(id, rename)), "Der Klauenbefund");
                version.Bump(DataScope.ClawFindings);
                return McpData.ToJson(new { updated = new { kind = "clawFinding", id, name = rename } });
            }
        }
    }

    // ---- Standardwerte --------------------------------------------------

    [McpServerTool(Name = "set_setting", Title = "Standardwert setzen", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Setzt einen Standardwert: ClawFindingFallback (Anzeigetext für Klauen ohne Befund), DefaultDosageUnit (Dosiereinheit, wenn das Medikament keine hat) oder BandageRemovalReminderDays (Tage bis zur Verband-Erinnerung, positive ganze Zahl).")]
    public async Task<string> SetSetting(
        [Description("ClawFindingFallback, DefaultDosageUnit oder BandageRemovalReminderDays.")] string key,
        [Description("Neuer Wert.")] string value)
    {
        var k = key?.Trim() ?? string.Empty;
        var v = value?.Trim() ?? string.Empty;

        string[] known = { AppSetting.ClawFindingFallbackKey, AppSetting.DefaultDosageUnitKey, AppSetting.BandageRemovalReminderDaysKey };
        var match = known.FirstOrDefault(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase))
                    ?? throw new McpException($"Unbekannter Schlüssel \"{key}\". Erlaubt: {string.Join(", ", known)}.");

        if (v.Length == 0)
        {
            throw new McpException("Der Wert darf nicht leer sein.");
        }

        // Dieselbe Regel wie Settings.razor.SaveBandageReminderDays.
        if (match == AppSetting.BandageRemovalReminderDaysKey)
        {
            if (!int.TryParse(v, out var days) || days < 1)
            {
                throw new McpException("BandageRemovalReminderDays muss eine positive ganze Zahl sein.");
            }
            v = days.ToString();
        }

        if (!await data.Settings.SetAsync(match, v))
        {
            throw new McpException($"Der Standardwert {match} konnte nicht gespeichert werden (Grund im Serverprotokoll).");
        }

        version.Bump(DataScope.Settings);
        return McpData.ToJson(new { key = match, value = v });
    }

    // ---- Hilfen ---------------------------------------------------------

    private int ResolveWhereHowFor(int medicineId, string? whereHow)
    {
        if (!string.IsNullOrWhiteSpace(whereHow))
        {
            return data.RequireWhereHow(whereHow);
        }

        return data.Medicines.Medicines.TryGetValue(medicineId, out var m) && m.DefaultWhereHowId is { } id
                                                                          && data.WhereHows.WhereHows.ContainsKey(id)
            ? id
            : throw new McpException("whereHow fehlt, und das Medikament hat kein Standard-Wie/Wo.");
    }

    private static IReadOnlySet<HoofPosition> Positions(string value, string what)
        => value.Trim() == ClearValue ? new HashSet<HoofPosition>() : McpData.RequirePositions(value, what);

    private static void RequirePositive(float value, string field)
    {
        if (!(value > 0) || float.IsInfinity(value))
        {
            throw new McpException($"{field} muss eine positive Zahl sein.");
        }
    }

    private static string NormalizeKind(string? kind)
        => kind?.Trim().ToLowerInvariant() switch
        {
            "medicine" or "medicines" => "medicine",
            "wherehow" or "wherehows" => "wherehow",
            "reason" or "reasons" => "reason",
            "clawfinding" or "clawfindings" => "clawfinding",
            _ => throw new McpException("kind muss medicine, whereHow, reason oder clawFinding sein.")
        };

    private static string? Unit(string? unit)
    {
        var u = unit?.Trim();
        if (string.IsNullOrEmpty(u))
        {
            return null;
        }

        RequireLength(u, 16);
        return u;
    }

    private static void RequireLength(string value, int max)
    {
        if (value.Length > max)
        {
            throw new McpException($"\"{value}\" ist zu lang (höchstens {max} Zeichen).");
        }
    }

    private static void RequireNew(int? existingId, string what, string name)
    {
        if (existingId is not null)
        {
            throw new McpException($"{what} \"{name}\" gibt es bereits (Id {existingId}).");
        }
    }

    private static void RequireRename(int? existingId, int ownId, string what, string name)
    {
        if (existingId is { } other && other != ownId)
        {
            throw new McpException($"{what} \"{name}\" gibt es bereits (Id {other}). Zum Zusammenführen bitte die Oberfläche verwenden.");
        }
    }

    private static void Require(bool ok, string what)
    {
        if (!ok)
        {
            throw new McpException($"{what} konnte nicht gespeichert werden (Grund im Serverprotokoll).");
        }
    }
}
