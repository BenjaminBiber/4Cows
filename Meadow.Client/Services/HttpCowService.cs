using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die Kuhstammdaten ueber HTTP. Gegenstueck zum EF-Dienst
/// CowService, Methode fuer Methode.
/// </summary>
public class HttpCowService : HttpServiceBase, ICowService
{
    // Wie in der EF-Fassung nach der stabilen Cow_ID und nie nach der
    // Ohrmarke: ein Kalb hat keine Ohrmarke, aber immer eine Cow_ID.
    private ImmutableDictionary<string, Cow> _cachedCows = ImmutableDictionary<string, Cow>.Empty;

    public ImmutableDictionary<string, Cow> Cows => _cachedCows;

    public HttpCowService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpCowService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    public async Task GetAllDataAsync()
    {
        var cows = await GetListAsync<Cow>("api/cows", "Failed to load cows.");
        if (cows is null)
        {
            return;
        }

        _cachedCows = cows.ToImmutableDictionary(c => c.CowId);
        Logger.LogInformation("Loaded {Count} cows.", _cachedCows.Count);
    }

    /// <summary>
    /// Legt eine Kuh an.
    ///
    /// Als einziger Insert dieser dreizehn Dienste OHNE Id-Rueckweg: Cow_ID ist
    /// der einzige vom Client vergebene Schluessel im Datenmodell - Cow.CreateCalf
    /// wuerfelt eine GUID -, die Datenbank vergibt hier also nichts, was
    /// zurueckkommen muesste. Die erzeugte Entitaet wird trotzdem gelesen, weil
    /// sie beweist, dass der Server die Zeile wirklich angelegt hat.
    /// </summary>
    public async Task<bool> InsertDataAsync(Cow cow)
    {
        var created = await CreateAsync("api/cows", cow, $"Failed to insert cow {cow.CowId}.");
        if (created is null)
        {
            return false;
        }

        // SetItem statt Add - gespiegelt aus der EF-Fassung, die es aus
        // Einheitlichkeit mit ihren Geschwistern so haelt.
        _cachedCows = _cachedCows.SetItem(cow.CowId, cow);
        Logger.LogInformation("Inserted cow {CowId}.", cow.CowId);
        return true;
    }

    public async Task<bool> RemoveByIdAsync(string cowId)
    {
        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/cows/{Segment(cowId)}"),
            $"Failed to remove cow {cowId}.");

        if (isSuccess && _cachedCows.ContainsKey(cowId))
        {
            _cachedCows = _cachedCows.Remove(cowId);
        }

        return isSuccess;
    }

    // Ab hier die reinen Nachschlagemethoden: die Rumpfe stehen in CowLookups,
    // damit es jede dieser Regeln genau einmal gibt. Ohne sie einigten sich
    // EF-Dienst und HTTP-Dienst irgendwann unabhaengig darauf, was "Kalb"
    // heisst. Die Zeilen sind Zeichen fuer Zeichen dieselben wie in
    // CowService der EF-Schicht.
    public Cow GetById(string cowId) => CowLookups.GetById(_cachedCows, cowId);

    public Cow GetByEarTagNumber(string earTagNumber) => CowLookups.GetByEarTagNumber(_cachedCows, earTagNumber);

    public string GetEarTagNumberByCollarNumber(int collarNumber, bool searchContainsLeavage = true)
        => CowLookups.GetEarTagNumberByCollarNumber(_cachedCows, collarNumber, searchContainsLeavage);

    public string GetCowIdByCollarNumber(int collarNumber, bool includeGone = false)
        => CowLookups.GetCowIdByCollarNumber(_cachedCows, collarNumber, includeGone);

    public int GetCollarNumberByCowId(string cowId) => CowLookups.GetCollarNumberByCowId(_cachedCows, cowId);

    public Cow GetCalfByCollarNumber(int collarNumber) => CowLookups.GetCalfByCollarNumber(_cachedCows, collarNumber);

    public bool IsCollarInUse(int collarNumber) => CowLookups.IsCollarInUse(_cachedCows, collarNumber);

    /// <summary>
    /// Aendert die Halsbandnummer.
    ///
    /// Die gecachte Instanz wird VERAENDERT und nicht ersetzt - das ist die
    /// unauffaelligste Falle des ganzen Umbaus und der Grund, warum diese
    /// Methode keine frisch deserialisierte Kuh in den Cache legt.
    /// BaseDataCow.razor haelt mit "_all = CowService.Cows.Values.ToList()"
    /// GENAU DIESE Objekte; ein Austausch liesse die Seite auf der alten
    /// Instanz sitzen und die Zeile aenderte sich stumm nicht. Das saehe wie
    /// ein Renderfehler aus und waere keiner.
    ///
    /// Gespiegelt aus CowService.UpdateCollarNumberAsync, bis hin zu den
    /// gesetzten Feldern.
    /// </summary>
    public async Task<bool> UpdateCollarNumberAsync(string cowId, int newCollarNumber)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/cows/{Segment(cowId)}/collar-number", new CollarNumberUpdate(newCollarNumber)),
            $"Failed to update collar number of cow {cowId}.");

        if (isSuccess && _cachedCows.ContainsKey(cowId))
        {
            var updatedCow = _cachedCows[cowId];
            updatedCow.CollarNumber = newCollarNumber;
            _cachedCows = _cachedCows.SetItem(cowId, updatedCow);
            Logger.LogInformation("Updated collar number for cow {CowId} to {CollarNumber}.", cowId, newCollarNumber);
        }

        return isSuccess;
    }

    /// <summary>
    /// Setzt oder loescht den Abgangsvermerk. Veraendert die eigene Instanz -
    /// die Begruendung steht an <see cref="UpdateCollarNumberAsync"/>.
    /// </summary>
    public async Task<bool> UpdateIsGoneAsync(string cowId, bool isGone)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/cows/{Segment(cowId)}/is-gone", new IsGoneUpdate(isGone)),
            $"Failed to update is gone flag of cow {cowId}.");

        if (isSuccess && _cachedCows.ContainsKey(cowId))
        {
            var updatedCow = _cachedCows[cowId];
            updatedCow.IsGone = isGone;
            _cachedCows = _cachedCows.SetItem(cowId, updatedCow);
            Logger.LogInformation("Updated is gone for cow {CowId} to {IsGone}.", cowId, isGone);
        }

        return isSuccess;
    }

    /// <summary>
    /// Weist einem Kalb eine Ohrmarke zu und loescht das Kalb-Kennzeichen, OHNE
    /// die Cow_ID anzufassen - an ihr haengt die ganze Behandlungshistorie.
    ///
    /// Zwei Felder, weil die EF-Fassung dieselben zwei setzt: EarTagNumber und
    /// IsCalv. Veraendert die eigene Instanz - siehe
    /// <see cref="UpdateCollarNumberAsync"/>.
    /// </summary>
    public async Task<bool> PromoteCalfAsync(string cowId, string earTagNumber)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/cows/{Segment(cowId)}/ear-tag", new EarTagAssignment(earTagNumber)),
            $"Failed to promote calf {cowId}.");

        if (isSuccess && _cachedCows.ContainsKey(cowId))
        {
            var updatedCow = _cachedCows[cowId];
            updatedCow.EarTagNumber = earTagNumber;
            updatedCow.IsCalv = false;
            _cachedCows = _cachedCows.SetItem(cowId, updatedCow);
            Logger.LogInformation("Promoted calf {CowId} with ear tag number {EarTagNumber}.", cowId, earTagNumber);
        }

        return isSuccess;
    }

    // Task.FromResult bleibt hier und wandert nicht in CowLookups: die Rechnung
    // ist synchron, aber die Signatur dieser Methode darf sich nicht aendern.
    // Wie in der EF-Fassung.
    public Task<IEnumerable<string>> SearchCows(string value, CancellationToken token)
        => Task.FromResult(CowLookups.SearchCows(_cachedCows, value));

    public string GetEarTagDisplay(string cowId) => CowLookups.GetEarTagDisplay(_cachedCows, cowId);

    public string GetDisplayLabel(string? cowId) => CowLookups.GetDisplayLabel(_cachedCows, cowId);

    public bool FilterFuncCow(string cowId, string searchString) => CowLookups.FilterFuncCow(_cachedCows, cowId, searchString);
}
