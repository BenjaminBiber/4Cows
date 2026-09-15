using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die Medikamente ueber HTTP. Gegenstueck zum EF-Dienst
/// MedicineService.
///
/// Der Semaphor der EF-Fassung hat hier KEIN Gegenstueck. Er klammerte dort
/// ein Suchen-sonst-Anlegen, das im Prozess des Servers lief; ueber HTTP
/// entscheidet der Server, und sein Semaphor steht weiter an derselben Stelle.
/// Ein zweiter im Browser schuetzte nur diesen einen Browser vor sich selbst -
/// und liesse glauben, das Rennen sei geschlossen.
/// </summary>
public class HttpMedicineService : HttpServiceBase, IMedicineService
{
    private ImmutableDictionary<int, Medicine> _cachedMedicines = ImmutableDictionary<int, Medicine>.Empty;

    public ImmutableDictionary<int, Medicine> Medicines => _cachedMedicines;

    public HttpMedicineService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpMedicineService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    public async Task GetAllDataAsync()
    {
        var medicines = await GetListAsync<Medicine>("api/medicines", "Failed to load medicines.");
        if (medicines is null)
        {
            return;
        }

        _cachedMedicines = medicines.ToImmutableDictionary(m => m.MedicineId);
        Logger.LogInformation("Loaded {Count} medicines.", _cachedMedicines.Count);
    }

    /// <summary>
    /// Wie oft jedes Medikament benutzt wird.
    ///
    /// <c>null</c> heisst "konnte nicht gezaehlt werden" und NICHT "nirgends
    /// benutzt" - ein leeres Woerterbuch gaebe den Papierkorb fuer jede Zeile
    /// frei. Der Endpunkt antwortet in dem Fall mit 503, und die faellt hier
    /// zusammen mit jedem anderen Fehlschlag auf dasselbe null. Gespiegelt aus
    /// MedicineService.GetUsageCountsAsync.
    /// </summary>
    public async Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync()
    {
        var counts = await GetJsonAsync<Dictionary<int, int>>(
            "api/medicines/usage-counts",
            "Failed to count medicine usages.");

        return counts?.ToImmutableDictionary();
    }

    /// <summary>
    /// Legt ein Medikament an und traegt die erzeugte Id in die UEBERGEBENE
    /// Instanz nach.
    ///
    /// Serverseitig macht EF genau das: die erzeugte Identity landet in der
    /// Instanz, die der Aufrufer weiterhaelt. Ohne diese Zeile stuende dort die
    /// 0, und der Dialog spraeche danach ueber ein Medikament mit der Id 0.
    /// </summary>
    public async Task<bool> InsertDataAsync(Medicine medicine)
    {
        var created = await CreateAsync(
            "api/medicines",
            medicine,
            $"Failed to insert medicine {medicine.MedicineName}.");

        if (created is null)
        {
            return false;
        }

        medicine.MedicineId = created.MedicineId;

        // Neu laden statt SetItem - gespiegelt aus der EF-Fassung, die nach
        // jedem Insert die ganze Tabelle holt.
        await GetAllDataAsync();
        Logger.LogInformation("Inserted medicine {MedicineName}.", medicine.MedicineName);
        return true;
    }

    public async Task<bool> RemoveByIdAsync(int medicineId)
    {
        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/medicines/{medicineId}"),
            $"Failed to delete medicine {medicineId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Deleted medicine {MedicineId}.", medicineId);
        }

        return isSuccess;
    }

    public async Task<bool> UpdateDataAsync(Medicine medicine)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/medicines/{medicine.MedicineId}", medicine),
            $"Failed to update medicine {medicine.MedicineId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Updated medicine {MedicineName}.", medicine.MedicineName);
        }

        return isSuccess;
    }

    public async Task<bool> MergeAsync(int sourceId, int targetId, string? survivingName = null)
    {
        // Derselbe Guard wie in der EF-Fassung, und aus demselben Grund: ein
        // Merge auf sich selbst wuerde erst umhaengen und dann genau das Ziel
        // loeschen. Erreichbar ueber eine reine Gross-/Kleinschreibungsaenderung,
        // weil der Namensvergleich case-insensitiv ist. Hier ausserdem eine
        // Anfrage weniger.
        if (sourceId == targetId)
        {
            return false;
        }

        var isSuccess = await WriteAsync(
            () => PostAsync($"api/medicines/{sourceId}/merge", new NamedMergeRequest(targetId, survivingName)),
            $"Failed to merge medicine {sourceId} into {targetId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Merged medicine {Source} into {Target}.", sourceId, targetId);
        }

        return isSuccess;
    }

    // Ab hier nur noch Weiterleitungen; die Rumpfe stehen in MedicineLookups,
    // damit es das "--" bei Fehltreffer genau einmal gibt. Zeichen fuer Zeichen
    // wie im EF-Dienst MedicineService.
    public Medicine? GetById(int medicineId) => MedicineLookups.GetById(_cachedMedicines, medicineId);

    public string GetMedicineNameById(int medicineId) => MedicineLookups.GetMedicineNameById(_cachedMedicines, medicineId);

    public string GetDosageUnit(int medicineId, string fallback) => MedicineLookups.GetDosageUnit(_cachedMedicines, medicineId, fallback);

    public List<string> GetMedicineNames() => MedicineLookups.GetMedicineNames(_cachedMedicines.Values);

    public List<string> GetMedicineNamesByIds(List<int> medicineIds) => MedicineLookups.GetMedicineNamesByIds(_cachedMedicines, medicineIds);

    /// <summary>
    /// Sucht das Praeparat zum Namen und legt es an, wenn es keines gibt.
    ///
    /// Der Vergleich passiert NICHT hier, sondern im Dienst hinter
    /// /medicines/by-name - sonst muesste dieser Client mit genau derselben
    /// Trim- und Kleinschreibung vergleichen wie der Server, und die erste
    /// Abweichung waere eine zweite Zeile fuer dasselbe Praeparat.
    ///
    /// int.MinValue bleibt der Fehlwert, wie in der EF-Fassung: die Dialoge
    /// pruefen darauf und brechen ab, BEVOR sie eine Behandlung schreiben. Eine
    /// 0 liessen sie durch.
    /// </summary>
    public async Task<int> GetMedicineIdByName(string medicineName)
    {
        // Gespiegelt aus der EF-Fassung, die als Erstes auf null prueft.
        if (medicineName == null)
        {
            return int.MinValue;
        }

        var response = await ReadAsync<IdResponse>(
            () => PostAsync("api/medicines/by-name", new NameRequest(medicineName.Trim())),
            $"Failed to resolve medicine name {medicineName}.");

        if (response?.Id is not int id)
        {
            return int.MinValue;
        }

        // Den eigenen Cache nachziehen. Der Endpunkt kann das Praeparat GERADE
        // ERST angelegt haben; ohne dieses Nachladen kennt der Cache die Id
        // nicht, und GetMedicineNameById zeigte in der Spalte daneben "--" fuer
        // ein Medikament, das dieser Aufruf soeben erzeugt hat. Die EF-Fassung
        // braucht die Zeile nicht, weil ihr InsertDataAsync den Cache schon
        // gefuellt hat - hier lief das Insert auf dem Server.
        await GetAllDataAsync();
        return id;
    }
}
