using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die Medikamente.
///
/// GetUsageCountsAsync liefert null, wenn nicht gezaehlt werden konnte - ein
/// leeres Dictionary saehe aus wie "nirgends benutzt" und gaebe den Papierkorb
/// fuer jede Zeile frei. Das gilt fuer jede Implementierung hinter der Naht.
/// </summary>
public interface IMedicineService
{
    ImmutableDictionary<int, Medicine> Medicines { get; }

    Task GetAllDataAsync();

    Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync();

    Task<bool> InsertDataAsync(Medicine medicine);

    Task<bool> RemoveByIdAsync(int medicineId);

    Task<bool> UpdateDataAsync(Medicine medicine);

    Task<bool> MergeAsync(int sourceId, int targetId, string? survivingName = null);

    Medicine? GetById(int medicineId);

    string GetMedicineNameById(int medicineId);

    string GetDosageUnit(int medicineId, string fallback);

    List<string> GetMedicineNames();

    List<string> GetMedicineNamesByIds(List<int> medicineIds);

    Task<int> GetMedicineIdByName(string medicineName);
}
