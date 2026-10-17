using Meadow.Shared.Models;
using Meadow.Shared.Services;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Meadow.Api.Export;

/// <summary>
/// Baut die Excel-Datei der Kuhbehandlungen. Gleicher Aufbau wie
/// ClawExcelExporter: die Zeilen kommen gefiltert wie auf dem Bildschirm
/// herein, die Spalten folgen der Tabelle in Cow_Table.razor.
/// </summary>
public static class CowExcelExporter
{
    public static byte[] Build(
        IReadOnlyList<CowTreatment> rows,
        ICowService cowService,
        IMedicineService medicineService,
        IWhereHowService whereHowService,
        IUdderService udderService,
        ITreatmentReasonService treatmentReasonService,
        ISettingsService settingsService)
    {
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Kuhbehandlungen");

        string[] headers =
        {
            "Halsbandnummer", "Ohrmarkennummer", "Datum", "Medikament",
            "Wie / Wo", "Menge", "Einheit", "Grund"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            // Color.FromArgb statt ColorTranslator - siehe ClawExcelExporter.
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0x2C, 0x7D, 0xA0));
            cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
            cell.Style.Font.Bold = true;
            cell.Style.Font.Size = 12;
        }

        var ordered = rows.OrderByDescending(r => r.AdministrationDate).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var t = ordered[i];
            var row = i + 2;

            sheet.Cells[row, 1].Value = Collar(t, cowService);
            sheet.Cells[row, 2].Value = cowService.GetEarTagDisplay(t.EarTagNumber);
            sheet.Cells[row, 3].Value = t.AdministrationDate.ToString("dd.MM.yyyy");
            sheet.Cells[row, 4].Value = medicineService.GetMedicineNameById(t.MedicineId);
            sheet.Cells[row, 5].Value = whereHowService.GetFullWhereHowName(t.WhereHowId, udderService, t.UdderId);

            // Menge als Zahl, damit Excel damit rechnen kann; die Einheit
            // steht deshalb in einer eigenen Spalte.
            var dosage = sheet.Cells[row, 6];
            dosage.Value = Math.Round((double)t.MedicineDosage, 2);
            dosage.Style.Numberformat.Format = "0.##";

            sheet.Cells[row, 7].Value = medicineService.GetDosageUnit(t.MedicineId, settingsService.DefaultDosageUnit);
            sheet.Cells[row, 8].Value = treatmentReasonService.GetNameById(t.TreatmentReasonId);
        }

        for (var i = 1; i <= headers.Length; i++)
        {
            sheet.Column(i).Width = 20;
        }
        // "Wie / Wo" enthaelt die Euterviertel und wird schnell laenger.
        sheet.Column(5).Width = 30;

        return package.GetAsByteArray();
    }

    private static string Collar(CowTreatment r, ICowService cowService)
    {
        var collar = cowService.GetCollarNumberByCowId(r.EarTagNumber);
        return collar == int.MinValue ? "–" : collar.ToString();
    }
}
