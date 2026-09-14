using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Die reinen Auswertungen ueber den Klauenbehandlungs-Cache. Rumpf hier,
/// delegierende Zeile in ClawTreatmentService - eine Kopie je Regel, damit
/// EF-Dienst und HTTP-Dienst dieselbe Zahl liefern.
/// </summary>
public static class ClawTreatmentLookups
{
    /// <summary>
    /// Zwoelf Monatswerte fuer das angegebene Jahr, ohne Jahr fuer das
    /// laufende.
    ///
    /// <paramref name="now"/> ist Parameter statt DateTime.Now im Rumpf: hier
    /// darf nichts an der Uhr des Servers haengen, sonst ist die Rechnung
    /// weder pruefbar noch auf einem Client mit anderer Zeitzone dieselbe.
    /// Die Instanzmethode reicht DateTime.Now durch und behaelt ihre Signatur.
    /// </summary>
    public static int[] GetClawTreatmentChartData(IEnumerable<ClawTreatment> treatments, DateTime now, int? year = null)
    {
        var currentYear = year.HasValue ? year.Value : now.Year;
        var months = Enumerable.Range(1, 12);

        var groupedData = treatments
            .Where(obj => obj.TreatmentDate.Year == currentYear)
            .GroupBy(obj => obj.TreatmentDate.Month)
            .ToDictionary(g => g.Key, g => g.Count());

        return months
            .Select(month => groupedData.ContainsKey(month) ? groupedData[month] : 0)
            .ToArray();
    }

    public static List<ClawTreatment> GetClawTreatments(IEnumerable<ClawTreatment> treatments)
    {
        return treatments.ToList();
    }

    public static List<ClawTreatment> GetClawTreatmentsWithBandage(IEnumerable<ClawTreatment> treatments)
    {
        return treatments.Where(x => (x.BandageLH || x.BandageLV || x.BandageRV || x.BandageRH) && !x.IsBandageRemoved).OrderBy(x => x.TreatmentDate).ToList();
    }
}
