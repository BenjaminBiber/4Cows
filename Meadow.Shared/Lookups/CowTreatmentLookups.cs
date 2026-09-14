using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Die reinen Auswertungen und Vorschlagslisten ueber den
/// Kuhbehandlungs-Cache. Rumpf hier, delegierende Zeile in
/// CowTreatmentService.
/// </summary>
public static class CowTreatmentLookups
{
    /// <summary>
    /// Zwoelf Monatswerte fuer das angegebene Jahr, ohne Jahr fuer das
    /// laufende. <paramref name="now"/> ist Parameter statt DateTime.Now im
    /// Rumpf - siehe ClawTreatmentLookups.
    /// </summary>
    public static int[] GetCowTreatmentChartData(IEnumerable<CowTreatment> treatments, DateTime now, int? year = null)
    {
        var currentYear = year.HasValue ? year.Value :  now.Year;
        var months = Enumerable.Range(1, 12);

        var groupedData = treatments
            .Where(obj => obj.AdministrationDate.Year == currentYear) 
            .GroupBy(obj => obj.AdministrationDate.Month)
            .ToDictionary(g => g.Key, g => g.Count());

        return months
            .Select(month => groupedData.ContainsKey(month) ? groupedData[month] : 0)
            .ToArray();
    }

    public static int[] GetCowTreatmentMedicineChartData(IEnumerable<CowTreatment> treatments, DateTime now, int medicine, int? year = null)
    {
        var currentYear = now.Year;
        if (year.HasValue)
        {
            currentYear = year.Value;
        }
        
        var months = Enumerable.Range(1, 12);

        return months
            .Select(month => treatments
                .Count(obj => obj.AdministrationDate.Year == currentYear && 
                              obj.AdministrationDate.Month == month &&
                              obj.MedicineId == medicine))
            .ToArray();
    }

    /// <summary>
    /// Vorschlaege fuer das Medikamenten-Autocomplete.
    ///
    /// Ueber <see cref="MedicineSearch.Rank"/> statt alphabetisch ueber die
    /// ganze Liste: Treffer am Wortanfang stehen damit vor Treffern irgendwo
    /// in der Mitte. Tippt jemand "Met", steht Metacam vor einem Praeparat,
    /// das "Metamizol" nur im hinteren Teil des Namens fuehrt.
    ///
    /// Der Cache kommt als Medikamentenliste herein, nicht als
    /// IMedicineService: die Instanzmethode behaelt ihren Dienst-Parameter und
    /// holt die Liste daraus.
    /// </summary>
    public static IEnumerable<string> SearchCowTreatmentMedicaments(IEnumerable<Medicine> medicines, string value)
    {
        return MedicineSearch.Rank(medicines, value);
    }

    public static IEnumerable<string> SearchCowTreatmentWhereHow(IEnumerable<string> whereHowNames, string value)
    {
        if (string.IsNullOrEmpty(value) || !whereHowNames.Any())
        {
            return whereHowNames;
        }

        if(!string.IsNullOrEmpty(value) && !whereHowNames.Any(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)))
        {
            return new List<string>() { value.Trim() };
        }
        
        return whereHowNames.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase));
    }

    public static int GetMinYear(IEnumerable<CowTreatment> treatments, DateTime now)
    {
        // Min() auf einer leeren Sequenz wirft. Erreichbar ueber
        // ChartDateDialog, also genau im Zustand direkt nach der
        // Erstinstallation - dort riss die Jahresauswahl den Circuit ab.
        if (!treatments.Any())
        {
            return now.Year;
        }

        return treatments.Min(t => t.AdministrationDate).Year;
    }
}
