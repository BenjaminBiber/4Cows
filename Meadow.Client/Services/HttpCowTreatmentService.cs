using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die Kuhbehandlungen ueber HTTP. Gegenstueck zum EF-Dienst
/// CowTreatmentService.
/// </summary>
public class HttpCowTreatmentService : HttpServiceBase, ICowTreatmentService, IMeadowSyncTarget
{
    private ImmutableDictionary<int, CowTreatment> _cachedTreatments = ImmutableDictionary<int, CowTreatment>.Empty;
    private ImmutableList<int> _cachedDistinctWhereHows = ImmutableList<int>.Empty;

    // Auf die Naht verbreitert, nicht entfernt: dieser Dienst braucht die
    // Wie/Wo-Namen weiterhin, nur nicht mehr die EF-Implementierung davon.
    private readonly IWhereHowService _whereHowService;

    private readonly MeadowOutbox _outbox;

    public ImmutableDictionary<int, CowTreatment> Treatments => _cachedTreatments;

    public ImmutableList<int> DistinctWhereHows => _cachedDistinctWhereHows;

    public MeadowEntityType EntityType => MeadowEntityType.CowTreatment;

    public HttpCowTreatmentService(
        IWhereHowService whereHowService,
        MeadowOutbox outbox,
        HttpClient http,
        DatabaseStatusService databaseStatusService,
        ILogger<HttpCowTreatmentService> logger)
        : base(http, databaseStatusService, logger)
    {
        _whereHowService = whereHowService;
        _outbox = outbox;
    }

    public async Task GetAllDataAsync()
    {
        var treatments = await GetListAsync<CowTreatment>("api/cow-treatments", "Failed to load cow treatments.");
        if (treatments is null)
        {
            return;
        }

        _cachedDistinctWhereHows = treatments.Select(t => t.WhereHowId).Distinct().ToImmutableList();
        _cachedTreatments = treatments.ToImmutableDictionary(t => t.CowTreatmentId);
        Logger.LogInformation("Loaded {Count} cow treatments.", _cachedTreatments.Count);
    }

    /// <summary>
    /// Mehrere Behandlungen in EINEM Aufruf - die Naht kennt nur diesen Weg,
    /// und der Endpunkt speichert den Stapel in EINER Transaktion.
    ///
    /// Der Id-Rueckweg laeuft POSITIONSWEISE: der Endpunkt gibt die Liste in
    /// ANFRAGEREIHENFOLGE zurueck, mit den Ids, die EF in die Instanzen
    /// geschrieben hat. Stimmt die Anzahl nicht, wird gar nichts
    /// zurueckgeschrieben und der Aufruf gilt als gescheitert - eine
    /// verschobene Zuordnung waere schlimmer als gar keine: sie traegt die Id
    /// der einen Behandlung an der anderen.
    /// </summary>
    public async Task<bool> InsertRangeAsync(IReadOnlyCollection<CowTreatment> treatments)
    {
        // Gespiegelt aus der EF-Fassung: ein leerer Stapel ist kein Fehlschlag.
        if (treatments.Count == 0)
        {
            return true;
        }

        var ordered = treatments.ToList();

        var attempt = await TryPostAsync<List<CowTreatment>>(
            "api/cow-treatments/batch",
            ordered,
            $"Failed to insert {ordered.Count} cow treatments.");

        if (attempt.IsOffline)
        {
            return await QueueOfflineAsync(ordered);
        }

        var created = attempt.Value;

        if (created is null)
        {
            return false;
        }

        if (created.Count != ordered.Count)
        {
            Logger.LogError(
                "Insert of {Sent} cow treatments answered with {Received} rows - keys not written back.",
                ordered.Count,
                created.Count);
            return false;
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].CowTreatmentId = created[i].CowTreatmentId;
        }

        // Neu laden - gespiegelt aus der EF-Fassung, die nach einem
        // erfolgreichen Stapel einmal die ganze Tabelle holt. Das deckt auch
        // DistinctWhereHows mit ab.
        await GetAllDataAsync();
        Logger.LogInformation("Inserted {Count} cow treatments.", ordered.Count);
        return true;
    }

    /// <summary>
    /// Kein Netz: der Stapel geht in die Outbox und gilt als gespeichert.
    ///
    /// Aus Sicht des Dialogs ist er das auch - die Zeilen stehen im lokalen
    /// Speicher, tragen ihre ClientId und gehen hinaus, sobald wieder Netz da
    /// ist. Der Cache bekommt sie sofort, sonst blieb die Tabelle leer,
    /// waehrend der Dialog "gespeichert" meldet.
    /// </summary>
    private async Task<bool> QueueOfflineAsync(List<CowTreatment> ordered)
    {
        var queued = await _outbox.QueueInsertAsync(
            MeadowEntityType.CowTreatment,
            ordered,
            t => t.ClientId,
            (t, provisionalId) => t.CowTreatmentId = provisionalId);

        if (!queued)
        {
            return false;
        }

        _cachedTreatments = _cachedTreatments.SetItems(
            ordered.Select(t => new KeyValuePair<int, CowTreatment>(t.CowTreatmentId, t)));
        _cachedDistinctWhereHows = _cachedTreatments.Values.Select(t => t.WhereHowId).Distinct().ToImmutableList();

        Logger.LogInformation("Queued {Count} cow treatments for later transmission.", ordered.Count);
        return true;
    }

    /// <summary>
    /// Traegt die vom Server bestaetigten Zeilen ein - aufgerufen aus
    /// <see cref="OutboxProcessor"/>, bei 201 wie bei 200.
    /// </summary>
    public IReadOnlyList<object> ApplySyncedRows(string responseJson)
    {
        var rows = MeadowSyncPayload.ReadRows<CowTreatment>(responseJson, Json);

        foreach (var row in rows)
        {
            // Schritt 1 ist der entscheidende: die wartende Zeile liegt unter
            // ihrer vorlaeufigen -3 im Cache, die bestaetigte kommt unter 57
            // herein. Ohne das Entfernen stuende die Behandlung ZWEIMAL in der
            // Tabelle, und niemand koennte sagen, welche der Zeilen echt ist.
            var local = _cachedTreatments.Values.FirstOrDefault(t => t.ClientId == row.ClientId);
            if (local is not null)
            {
                _cachedTreatments = _cachedTreatments.Remove(local.CowTreatmentId);
            }

            // SetItem und NIE Add: ein parallel gelaufener Neuabruf kann die
            // echte Id laengst eingetragen haben, und Add wuerfe dann.
            _cachedTreatments = _cachedTreatments.SetItem(row.CowTreatmentId, row);
        }

        _cachedDistinctWhereHows = _cachedTreatments.Values.Select(t => t.WhereHowId).Distinct().ToImmutableList();
        return rows;
    }

    /// <summary>
    /// Erst der Cache, bei Fehltreffer ueber HTTP nachgeholt - wie die
    /// EF-Fassung, die auf die Datenbank zurueckfaellt.
    ///
    /// "Gibt es nicht" meldet auch diese Fassung mit einer FRISCHEN Instanz
    /// statt mit null: die Signatur verspricht CowTreatment, und die
    /// Aufrufstellen lesen die Felder direkt.
    /// </summary>
    public async Task<CowTreatment> GetByIdAsync(int id)
    {
        if (_cachedTreatments.ContainsKey(id))
        {
            return _cachedTreatments[id];
        }

        var treatmentResult = await ReadAsync<CowTreatment>(
            () => GetAsync($"api/cow-treatments/{id}"),
            $"Failed to fetch cow treatment {id}.",
            notFoundIsExpected: true);

        if (treatmentResult is not null)
        {
            // SetItem statt Add: zwei gleichzeitige Aufrufe mit derselben Id
            // kommen beide am ContainsKey oben vorbei, und der zweite Add wirft
            // dann.
            _cachedTreatments = _cachedTreatments.SetItem(id, treatmentResult);
        }

        return treatmentResult ?? new CowTreatment();
    }

    /// <summary>
    /// Loescht eine Behandlung.
    ///
    /// Gibt wie die EF-Fassung ein blankes Task zurueck und verschluckt jeden
    /// Fehlschlag in eine Protokollzeile - "geloescht", "gab es nie" und "ist
    /// fehlgeschlagen" kommen beim Aufrufer gleich an. Der Cache wird nur bei
    /// Erfolg angefasst.
    /// </summary>
    public async Task DeleteDataAsync(int id)
    {
        // Eine negative Id ist eine vorlaeufige - die Zeile hat den Server nie
        // gesehen. Ein DELETE darauf traefe eine Id, die es dort nicht gibt.
        // Der Endpunkt antwortet ehrlich mit 204, der Cache entfernt die Zeile,
        // und der Outbox-Eintrag bleibt liegen und legt sie beim naechsten
        // Durchlauf wieder an: geloescht, und trotzdem wieder da. Deshalb wird
        // hier der Insert zurueckgezogen statt ein Loeschbefehl gesendet.
        if (id < 0 && _cachedTreatments.TryGetValue(id, out var pending))
        {
            if (await _outbox.CancelPendingInsertAsync(pending.ClientId, MeadowStores.CowTreatment))
            {
                _cachedTreatments = _cachedTreatments.Remove(id);
                Logger.LogInformation("Wartende Zeile {Id} zurueckgezogen, nichts gesendet.", id);
                return;
            }
        }

        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/cow-treatments/{id}"),
            $"Failed to delete cow treatment {id}.");

        if (isSuccess && _cachedTreatments.ContainsKey(id))
        {
            _cachedTreatments = _cachedTreatments.Remove(id);
            Logger.LogInformation("Deleted cow treatment with ID {Id}.", id);
        }
    }

    // Ab hier nur noch Weiterleitungen; die Rumpfe stehen in
    // CowTreatmentLookups. DateTime.Now wird dort zum Parameter, damit die
    // Rechnung nicht an einer fremden Uhr haengt - diese Signaturen bleiben
    // unveraendert.
    public int[] GetCowTreatmentChartData(int? year = null)
        => CowTreatmentLookups.GetCowTreatmentChartData(_cachedTreatments.Values, DateTime.Now, year);

    public int[] GetCowTreatmentMedicineChartData(int medicine, int? year = null)
        => CowTreatmentLookups.GetCowTreatmentMedicineChartData(_cachedTreatments.Values, DateTime.Now, medicine, year);

    // Bleibt async, obwohl nichts erwartet wird: das ist der heutige Zustand
    // und keine Aufraeumarbeit dieses Schrittes - die EF-Fassung sieht genauso
    // aus. Der Dienst-Parameter ist auf die Naht verbreitert; herausgeholt wird
    // daraus nur der Cache.
    public async Task<IEnumerable<string>> SearchCowTreatmentMedicaments(string value, CancellationToken token, IMedicineService medicineService)
    {
        return CowTreatmentLookups.SearchCowTreatmentMedicaments(medicineService.Medicines.Values, value);
    }

    public async Task<IEnumerable<string>> SearchCowTreatmentWhereHow(string value, CancellationToken token)
    {
        return CowTreatmentLookups.SearchCowTreatmentWhereHow(_whereHowService.WhereHowNames, value);
    }

    public int GetMinYear()
        => CowTreatmentLookups.GetMinYear(_cachedTreatments.Values, DateTime.Now);
}
