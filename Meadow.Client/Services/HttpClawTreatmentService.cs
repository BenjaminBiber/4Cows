using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die Klauenbehandlungen ueber HTTP. Gegenstueck zum EF-Dienst
/// ClawTreatmentService, inklusive des grossen ID in
/// <see cref="GetByIDAsync"/>.
/// </summary>
public class HttpClawTreatmentService : HttpServiceBase, IClawTreatmentService
{
    private ImmutableDictionary<int, ClawTreatment> _cachedTreatments = ImmutableDictionary<int, ClawTreatment>.Empty;

    public ImmutableDictionary<int, ClawTreatment> Treatments => _cachedTreatments;

    public HttpClawTreatmentService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpClawTreatmentService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    public async Task GetAllDataAsync()
    {
        var treatments = await GetListAsync<ClawTreatment>("api/claw-treatments", "Failed to load claw treatments.");
        if (treatments is null)
        {
            return;
        }

        _cachedTreatments = treatments.ToImmutableDictionary(t => t.ClawTreatmentId);
        Logger.LogInformation("Loaded {Count} claw treatments.", _cachedTreatments.Count);
    }

    /// <summary>
    /// Legt eine Klauenbehandlung an.
    ///
    /// Das ist der Fall, an dem der Cache bisher zerbrach. Der Schluessel ist
    /// die Identity; serverseitig schreibt EF sie beim SaveChangesAsync in die
    /// uebergebene Instanz. Ohne die Zeile unten stuende hier die 0, der
    /// Aufrufer haelt seine eigene Instanz weiter, und die Tabelle zeigte EINE
    /// Zeile, wo zwei gespeichert wurden - der zweite Eintrag ersetzte im Cache
    /// still den ersten.
    /// </summary>
    public async Task<bool> InsertDataAsync(ClawTreatment clawTreatment)
    {
        var created = await CreateAsync("api/claw-treatments", clawTreatment, "Failed to insert claw treatment.");
        if (created is null)
        {
            return false;
        }

        clawTreatment.ClawTreatmentId = created.ClawTreatmentId;

        // SetItem statt Add, und kein Neuladen: gespiegelt aus der EF-Fassung.
        _cachedTreatments = _cachedTreatments.SetItem(clawTreatment.ClawTreatmentId, clawTreatment);
        Logger.LogInformation("Inserted claw treatment {Id}.", clawTreatment.ClawTreatmentId);
        return true;
    }

    /// <summary>
    /// Erst der Cache, bei Fehltreffer ueber HTTP nachgeholt - wie die
    /// EF-Fassung, die auf die Datenbank zurueckfaellt. "Gibt es nicht" meldet
    /// auch diese Fassung mit einer FRISCHEN Instanz statt mit null.
    /// </summary>
    public async Task<ClawTreatment> GetByIDAsync(int id)
    {
        if (_cachedTreatments.ContainsKey(id))
        {
            return _cachedTreatments[id];
        }

        var treatmentResult = await ReadAsync<ClawTreatment>(
            () => GetAsync($"api/claw-treatments/{id}"),
            $"Failed to load claw treatment {id}.",
            notFoundIsExpected: true);

        if (treatmentResult is not null)
        {
            // SetItem statt Add: zwei gleichzeitige Aufrufe mit derselben Id
            // kommen beide am ContainsKey oben vorbei, und der zweite Add wirft
            // dann.
            _cachedTreatments = _cachedTreatments.SetItem(id, treatmentResult);
        }

        return treatmentResult ?? new ClawTreatment();
    }

    public async Task<bool> UpdateDataAsync(ClawTreatment clawTreatment)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/claw-treatments/{clawTreatment.ClawTreatmentId}", clawTreatment),
            $"Failed to update claw treatment {clawTreatment.ClawTreatmentId}.");

        if (isSuccess)
        {
            // Neu laden statt den Cache punktuell zu setzen: das Update
            // schreibt die ganze Zeile, und im Cache liegt noch die Instanz von
            // vor der Bearbeitung. Gespiegelt aus der EF-Fassung.
            await GetAllDataAsync();
            Logger.LogInformation("Updated claw treatment {Id}.", clawTreatment.ClawTreatmentId);
        }

        return isSuccess;
    }

    /// <summary>
    /// Vermerkt den Verband als abgenommen.
    ///
    /// Veraendert die EIGENE gecachte Instanz und ersetzt sie nicht. Die Seiten
    /// halten genau diese Objekte; eine frisch deserialisierte Behandlung an
    /// ihrer Stelle liesse die Zeile stumm unveraendert. Dieselbe Falle wie in
    /// HttpCowService.UpdateCollarNumberAsync, und dieselbe Loesung wie in der
    /// EF-Fassung.
    /// </summary>
    public async Task<bool> RemoveBandageAsync(int id)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/claw-treatments/{id}/bandage-removed"),
            $"Failed to update bandage flag of claw treatment {id}.");

        if (isSuccess && _cachedTreatments.ContainsKey(id))
        {
            var updatedTreatment = _cachedTreatments[id];
            updatedTreatment.IsBandageRemoved = true;
            _cachedTreatments = _cachedTreatments.SetItem(id, updatedTreatment);
            Logger.LogInformation("Removed bandage from claw treatment {Id}.", id);
        }

        return isSuccess;
    }

    /// <summary>
    /// Legt das Kennzeichen fuer mehrere Behandlungen auf einmal um.
    ///
    /// Ein einziger Aufruf statt <see cref="RemoveBandageAsync"/> in einer
    /// Schleife: der Endpunkt schreibt EIN Update, ein Fehler in der Mitte
    /// hinterliesse sonst einen halb entfernten Stapel - und ueber HTTP waeren
    /// es ausserdem N Netzrunden.
    ///
    /// Auch hier werden die eigenen Instanzen VERAENDERT, nicht ersetzt.
    /// </summary>
    /// <returns>Zahl der betroffenen Behandlungen, 0 im Fehlerfall.</returns>
    public async Task<int> RemoveBandagesAsync(IReadOnlyCollection<int> ids)
    {
        // Gespiegelt aus der EF-Fassung.
        if (ids.Count == 0)
        {
            return 0;
        }

        var response = await ReadAsync<RemovedResponse>(
            () => PostAsync("api/claw-treatments/bandages-removed", new BandageRemovalRequest(ids)),
            $"Failed to update bandage flags of {ids.Count} claw treatments.");

        if (response is null)
        {
            return 0;
        }

        if (response.Removed > 0)
        {
            // Nur die Behandlungen nachziehen, die der Cache auch kennt - ids
            // kann IDs enthalten, die inzwischen woanders geloescht wurden, und
            // SetItems wuerde die sonst wieder anlegen.
            var updated = ids
                .Where(id => _cachedTreatments.ContainsKey(id))
                .Select(id => _cachedTreatments[id])
                .ToList();

            foreach (var treatment in updated)
            {
                treatment.IsBandageRemoved = true;
            }

            _cachedTreatments = _cachedTreatments.SetItems(
                updated.Select(t => new KeyValuePair<int, ClawTreatment>(t.ClawTreatmentId, t)));

            Logger.LogInformation("Removed bandages from {Count} claw treatments.", response.Removed);
        }

        return response.Removed;
    }

    /// <summary>
    /// Loescht eine Klauenbehandlung. Gibt wie die EF-Fassung ein blankes Task
    /// zurueck und verschluckt jeden Fehlschlag in eine Protokollzeile.
    /// </summary>
    public async Task DeleteDataAsync(int id)
    {
        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/claw-treatments/{id}"),
            $"Failed to delete claw treatment {id}.");

        if (isSuccess && _cachedTreatments.ContainsKey(id))
        {
            _cachedTreatments = _cachedTreatments.Remove(id);
            Logger.LogInformation("Deleted claw treatment with ID {Id}.", id);
        }
    }

    // Ab hier nur noch Weiterleitungen; die Rumpfe stehen in
    // ClawTreatmentLookups. DateTime.Now wird dort zum Parameter, damit die
    // Rechnung nicht an einer fremden Uhr haengt - diese Signaturen bleiben
    // unveraendert.
    public int[] GetClawTreatmentChartData(int? year = null)
        => ClawTreatmentLookups.GetClawTreatmentChartData(_cachedTreatments.Values, DateTime.Now, year);

    public List<ClawTreatment> GetClawTreatments()
        => ClawTreatmentLookups.GetClawTreatments(Treatments.Values);

    public List<ClawTreatment> GetClawTreatmentsWithBandage()
        => ClawTreatmentLookups.GetClawTreatmentsWithBandage(Treatments.Values);
}
