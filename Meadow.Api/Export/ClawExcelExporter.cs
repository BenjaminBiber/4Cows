using Meadow.Shared.Models;
using Meadow.Shared.Services;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Meadow.Api.Export;

/// <summary>
/// Baut die Excel-Datei der Klauenbehandlungen. Stand woertlich in
/// Claw_Table.razor; hierher verschoben, weil die Tabelle spaeter im Browser
/// laeuft und EPPlus dort nicht laeuft - weder die Bibliothek selbst noch
/// System.Drawing.Common, das sie mitbringt.
///
/// Die Zeilen kommen von aussen herein, gefiltert wie auf dem Bildschirm.
/// Den Filter hier nachzubauen waere eine zweite Wahrheit darueber, was
/// "die aktuelle Ansicht" ist - genau die Sorte Abweichung, die niemandem
/// auffaellt, bis die exportierte Datei anders aussieht als die Tabelle.
/// </summary>
public static class ClawExcelExporter
{
    public static byte[] Build(
        IReadOnlyList<ClawTreatment> rows,
        ICowService cowService,
        IClawFindingService clawFindingService)
    {
        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Klauenbehandlungen");

        // Reihenfolge korrigiert: die Kopfzeile begann mit "Halsbandnummer",
        // geschrieben wurde in Spalte 1 aber die Ohrmarke.
        string[] headers =
        {
            "Ohrmarkennummer", "Halsbandnummer", "Datum",
            "Behandlung LV", "Verband LV", "Klotz LV",
            "Behandlung RV", "Verband RV", "Klotz RV",
            "Behandlung LH", "Verband LH", "Klotz LH",
            "Behandlung RH", "Verband RH", "Klotz RH",
            "Wurde Verband entfernt?"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            // Hier stand System.Drawing.ColorTranslator.FromHtml(MudBlazor.Color.Primary.ToString()),
            // also der String "Primary" - das wirft, und zwar beim ersten
            // Kopffeld. Der Export war damit komplett unbenutzbar.
            //
            // Danach stand hier ColorTranslator.FromHtml("#2C7DA0"). Richtig
            // gedacht, aber ColorTranslator lebt in System.Drawing.Common und
            // ist seit .NET 7 nur unter Windows unterstuetzt - im Linux-Container
            // haette es wieder geworfen. Color.FromArgb kommt aus
            // System.Drawing.Primitives und laeuft ueberall. Gleiche Farbe.
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(0x2C, 0x7D, 0xA0));
            cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
            cell.Style.Font.Bold = true;
            cell.Style.Font.Size = 12;
        }

        var ordered = rows.OrderByDescending(r => r.TreatmentDate).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var t = ordered[i];
            // Datenzeilen beginnen direkt unter dem Kopf; vorher ab Zeile 3,
            // wodurch Zeile 2 immer leer blieb.
            var row = i + 2;

            sheet.Cells[row, 1].Value = cowService.GetEarTagDisplay(t.EarTagNumber);
            sheet.Cells[row, 2].Value = Collar(t, cowService);
            sheet.Cells[row, 3].Value = t.TreatmentDate.ToString("dd.MM.yyyy");

            var column = 4;
            foreach (var position in new[] { HoofPosition.LV, HoofPosition.RV, HoofPosition.LH, HoofPosition.RH })
            {
                sheet.Cells[row, column++].Value = FindingName(t, position, clawFindingService);
                sheet.Cells[row, column++].Value = t.GetBandage(position) ? "Verband" : "Kein Verband";
                sheet.Cells[row, column++].Value = t.GetBlock(position) ? "Klotz" : "Kein Klotz";
            }

            // Spalte 16 ist "Wurde Verband entfernt?" - markiert wurde vorher
            // Spalte 15 ("Klotz RH").
            var removed = sheet.Cells[row, 16];
            removed.Value = t.IsBandageRemoved ? "Ja" : "Nein";
            removed.Style.Fill.PatternType = ExcelFillStyle.Solid;
            removed.Style.Fill.BackgroundColor.SetColor(
                t.IsBandageRemoved ? System.Drawing.Color.LightGreen : System.Drawing.Color.LightPink);
        }

        for (var i = 1; i <= headers.Length; i++)
        {
            sheet.Column(i).Width = 20;
        }

        return package.GetAsByteArray();
    }

    private static string Collar(ClawTreatment r, ICowService cowService)
    {
        var collar = cowService.GetCollarNumberByCowId(r.EarTagNumber);
        return collar == int.MinValue ? "–" : collar.ToString();
    }

    private static string FindingName(ClawTreatment r, HoofPosition position, IClawFindingService clawFindingService)
        => clawFindingService.GetNameById(r.GetFindingId(position)).Trim();
}
