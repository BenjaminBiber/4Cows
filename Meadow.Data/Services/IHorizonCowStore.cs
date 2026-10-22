using Meadow.Shared.Models;

namespace Meadow.Data.Services;

/// <summary>
/// Die schmale Schreibnaht fuer den Horizon-Abgleich. Absichtlich NICHT auf ICowService:
/// die Naht teilt sich der Browser (HttpCowService), und dort gibt es keinen Abgleich.
/// </summary>
public interface IHorizonCowStore
{
    IReadOnlyDictionary<string, Cow> Cows { get; }
    Task GetAllDataAsync();
    Task<bool> InsertDataAsync(Cow cow);
    Task<bool> PromoteCalfAsync(string cowId, string earTagNumber);

    /// <summary>
    /// Uebernimmt die Horizon-Werte fuer eine Kuh in EINEM Update: Lely-ID, Halsband,
    /// IsGone, Ort, Gruppe, Beschreibung, und die Ohrmarke nur, wenn earTag nicht leer ist
    /// (eine Ohrmarke wird nie geloescht).
    /// </summary>
    Task<bool> ApplyHorizonAsync(string cowId, string lelyAnimalId, int collarNumber, string? earTag,
        bool isGone, string? location, string? group, string? description);
}
