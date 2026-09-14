using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Die reinen Nachschlageregeln ueber den Medikamenten-Cache. Rumpf hier,
/// delegierende Zeile in MedicineService.
/// </summary>
public static class MedicineLookups
{
    public static Medicine? GetById(IReadOnlyDictionary<int, Medicine> medicines, int medicineId)
    {
        return medicines.GetValueOrDefault(medicineId);
    }

    /// <summary>
    /// Anzeigename, bei Fehltreffer der Gedankenstrich "--". NICHT leer und
    /// nicht der Halbgeviertstrich der Behandlungsgruende: die Tabellen zeigen
    /// diese zwei Zeichen seit jeher fuer "Medikament nicht mehr da".
    /// </summary>
    public static string GetMedicineNameById(IReadOnlyDictionary<int, Medicine> medicines, int medicineId)
    {
        return medicines.ContainsKey(medicineId) ? medicines[medicineId].MedicineName : "--";
    }

    /// <summary>
    /// Einheit des Medikaments, oder <paramref name="fallback"/>, wenn keine
    /// hinterlegt ist. Der Rueckfallwert bleibt Sache des Aufrufers: die
    /// Tabellen haben bisher fest "ml" angezeigt, und das soll fuer Zeilen ohne
    /// Einheit unveraendert so bleiben.
    /// </summary>
    public static string GetDosageUnit(IReadOnlyDictionary<int, Medicine> medicines, int medicineId, string fallback)
    {
        var unit = GetById(medicines, medicineId)?.DosageUnit;
        return string.IsNullOrWhiteSpace(unit) ? fallback : unit;
    }

    public static List<string> GetMedicineNames(IEnumerable<Medicine> medicines)
    {
        return medicines.Select(m => m.MedicineName).ToList();
    }

    public static List<string> GetMedicineNamesByIds(IReadOnlyDictionary<int, Medicine> medicines, List<int> medicineIds)
    {
        return medicines.Where(m => medicineIds.Contains(m.Key)).Select(m => m.Value.MedicineName).ToList();
    }
}
