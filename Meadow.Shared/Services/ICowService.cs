using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die Kuhstammdaten. Dahinter steckt heute der EF-Dienst aus
/// Meadow.Data, spaeter ein HTTP-Dienst im Browser.
///
/// Die Signaturen sind WOERTLICH aus der heutigen Implementierung uebernommen,
/// samt der Nullability, die dort falsch ist: <see cref="GetById"/> und
/// <see cref="GetByEarTagNumber"/> koennen <c>null</c> liefern, stehen hier
/// aber als <c>Cow</c> und nicht als <c>Cow?</c>. Ein <c>Cow?</c> zoege CS8602
/// durch die Razor-Rumpfe und braeche die Zusage, dass sich an den Komponenten
/// nur die @inject-Zeile aendert. Wer das geradezieht, zieht es an den
/// Aufrufstellen mit gerade - nicht hier allein.
/// </summary>
public interface ICowService
{
    ImmutableDictionary<string, Cow> Cows { get; }

    Task GetAllDataAsync();

    Task<bool> InsertDataAsync(Cow cow);

    Task<bool> RemoveByIdAsync(string cowId);

    Cow GetById(string cowId);

    Cow GetByEarTagNumber(string earTagNumber);

    string GetEarTagNumberByCollarNumber(int collarNumber, bool searchContainsLeavage = true);

    string GetCowIdByCollarNumber(int collarNumber, bool includeGone = false);

    int GetCollarNumberByCowId(string cowId);

    Cow GetCalfByCollarNumber(int collarNumber);

    bool IsCollarInUse(int collarNumber);

    Task<bool> UpdateCollarNumberAsync(string cowId, int newCollarNumber);

    Task<bool> UpdateIsGoneAsync(string cowId, bool isGone);

    Task<bool> PromoteCalfAsync(string cowId, string earTagNumber);

    Task<IEnumerable<string>> SearchCows(string value, CancellationToken token);

    string GetEarTagDisplay(string cowId);

    string GetDisplayLabel(string? cowId);

    bool FilterFuncCow(string cowId, string searchString);
}
