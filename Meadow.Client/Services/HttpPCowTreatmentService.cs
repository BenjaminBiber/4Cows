using System.Collections.Immutable;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die geplanten Kuhbehandlungen ueber HTTP. Gegenstueck zum EF-Dienst
/// PCowTreatmentService.
/// </summary>
public class HttpPCowTreatmentService : HttpServiceBase, IPCowTreatmentService, IMeadowSyncTarget
{
    private ImmutableDictionary<int, PlannedCowTreatment> _cachedTreatments = ImmutableDictionary<int, PlannedCowTreatment>.Empty;
    private ImmutableList<string> _cachedMedicineList = ImmutableList<string>.Empty;
    private ImmutableList<int> _cachedWhereHowList = ImmutableList<int>.Empty;

    private readonly MeadowOutbox _outbox;

    public ImmutableDictionary<int, PlannedCowTreatment> Treatments => _cachedTreatments;

    public ImmutableList<string> CowMedicineTreatmentList => _cachedMedicineList;

    public ImmutableList<int> CowWhereHowList => _cachedWhereHowList;

    public MeadowEntityType EntityType => MeadowEntityType.PlannedCowTreatment;

    public HttpPCowTreatmentService(
        MeadowOutbox outbox,
        HttpClient http,
        DatabaseStatusService databaseStatusService,
        ILogger<HttpPCowTreatmentService> logger)
        : base(http, databaseStatusService, logger)
    {
        _outbox = outbox;
    }

    public async Task GetAllDataAsync()
    {
        var treatments = await GetListAsync<PlannedCowTreatment>(
            "api/planned-cow-treatments",
            "Failed to load planned cow treatments.");

        if (treatments is null)
        {
            return;
        }

        _cachedTreatments = treatments.ToImmutableDictionary(t => t.PlannedCowTreatmentId);
        _cachedMedicineList = treatments.Select(t => t.MedicineId.ToString()).Distinct().ToImmutableList();
        _cachedWhereHowList = treatments.Select(t => t.WhereHowId).Distinct().ToImmutableList();
        Logger.LogInformation("Loaded {Count} planned cow treatments.", _cachedTreatments.Count);
    }

    /// <summary>
    /// Mehrere Planungen in EINEM Aufruf. Der Id-Rueckweg laeuft
    /// POSITIONSWEISE: der Endpunkt gibt die Liste in ANFRAGEREIHENFOLGE
    /// zurueck, mit den Ids, die EF in die Instanzen geschrieben hat. Stimmt
    /// die Anzahl nicht, wird gar nichts zurueckgeschrieben und der Aufruf gilt
    /// als gescheitert - eine verschobene Zuordnung traegt die Id der einen
    /// Planung an der anderen und waere schlimmer als gar keine.
    /// </summary>
    public async Task<bool> InsertRangeAsync(IReadOnlyCollection<PlannedCowTreatment> treatments)
    {
        // Gespiegelt aus der EF-Fassung: ein leerer Stapel ist kein Fehlschlag.
        if (treatments.Count == 0)
        {
            return true;
        }

        var ordered = treatments.ToList();

        var attempt = await TryPostAsync<List<PlannedCowTreatment>>(
            "api/planned-cow-treatments/batch",
            ordered,
            $"Failed to insert {ordered.Count} planned cow treatments.");

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
                "Insert of {Sent} planned cow treatments answered with {Received} rows - keys not written back.",
                ordered.Count,
                created.Count);
            return false;
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].PlannedCowTreatmentId = created[i].PlannedCowTreatmentId;
        }

        // Ein Neuladen deckt beide abgeleiteten Listen mit ab - gespiegelt aus
        // der EF-Fassung.
        await GetAllDataAsync();
        Logger.LogInformation("Inserted {Count} planned cow treatments.", ordered.Count);
        return true;
    }

    /// <summary>
    /// Kein Netz: der Stapel geht in die Outbox und gilt als gespeichert. Der
    /// Cache bekommt die Zeilen sofort, sonst blieb die Tabelle leer, waehrend
    /// der Dialog "gespeichert" meldet.
    /// </summary>
    private async Task<bool> QueueOfflineAsync(List<PlannedCowTreatment> ordered)
    {
        var queued = await _outbox.QueueInsertAsync(
            MeadowEntityType.PlannedCowTreatment,
            ordered,
            t => t.ClientId,
            (t, provisionalId) => t.PlannedCowTreatmentId = provisionalId);

        if (!queued)
        {
            return false;
        }

        _cachedTreatments = _cachedTreatments.SetItems(
            ordered.Select(t => new KeyValuePair<int, PlannedCowTreatment>(t.PlannedCowTreatmentId, t)));
        RebuildDerivedLists();

        Logger.LogInformation("Queued {Count} planned cow treatments for later transmission.", ordered.Count);
        return true;
    }

    /// <summary>
    /// Traegt die vom Server bestaetigten Zeilen ein - aufgerufen aus
    /// <see cref="OutboxProcessor"/>, bei 201 wie bei 200.
    /// </summary>
    public IReadOnlyList<object> ApplySyncedRows(string responseJson)
    {
        var rows = MeadowSyncPayload.ReadRows<PlannedCowTreatment>(responseJson, Json);

        foreach (var row in rows)
        {
            // Erst den vorlaeufigen Schluessel weg, dann den echten setzen -
            // sonst stuende die Planung zweimal in der Tabelle, einmal unter
            // -3 und einmal unter 57.
            var local = _cachedTreatments.Values.FirstOrDefault(t => t.ClientId == row.ClientId);
            if (local is not null)
            {
                _cachedTreatments = _cachedTreatments.Remove(local.PlannedCowTreatmentId);
            }

            // SetItem und NIE Add: ein parallel gelaufener Neuabruf kann die
            // echte Id laengst eingetragen haben.
            _cachedTreatments = _cachedTreatments.SetItem(row.PlannedCowTreatmentId, row);
        }

        RebuildDerivedLists();
        return rows;
    }

    /// <summary>
    /// Die beiden abgeleiteten Listen. Sie haengen am Cache und nicht an der
    /// Antwort - deshalb einmal hier, statt an drei Stellen abgeschrieben.
    /// </summary>
    private void RebuildDerivedLists()
    {
        _cachedMedicineList = _cachedTreatments.Values.Select(t => t.MedicineId.ToString()).Distinct().ToImmutableList();
        _cachedWhereHowList = _cachedTreatments.Values.Select(t => t.WhereHowId).Distinct().ToImmutableList();
    }

    public async Task<bool> RemoveByIDAsync(int id)
    {
        // Eine negative Id ist eine vorlaeufige - die Zeile hat den Server nie
        // gesehen. Ein DELETE darauf traefe eine Id, die es dort nicht gibt.
        // Der Endpunkt antwortet ehrlich mit 204, der Cache entfernt die Zeile,
        // und der Outbox-Eintrag bleibt liegen und legt sie beim naechsten
        // Durchlauf wieder an: geloescht, und trotzdem wieder da. Deshalb wird
        // hier der Insert zurueckgezogen statt ein Loeschbefehl gesendet.
        if (id < 0 && _cachedTreatments.TryGetValue(id, out var pending))
        {
            if (await _outbox.CancelPendingInsertAsync(pending.ClientId, MeadowStores.PlannedCowTreatment))
            {
                _cachedTreatments = _cachedTreatments.Remove(id);
                Logger.LogInformation("Wartende Zeile {Id} zurueckgezogen, nichts gesendet.", id);
                return true;
            }
        }

        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/planned-cow-treatments/{id}"),
            $"Failed to delete planned cow treatment {id}.");

        if (isSuccess && _cachedTreatments.ContainsKey(id))
        {
            _cachedTreatments = _cachedTreatments.Remove(id);
            _cachedMedicineList = _cachedTreatments.Values.Select(t => t.MedicineId.ToString()).Distinct().ToImmutableList();
            _cachedWhereHowList = _cachedTreatments.Values.Select(t => t.WhereHowId).Distinct().ToImmutableList();
            Logger.LogInformation("Removed planned cow treatment {Id}.", id);
        }

        return isSuccess;
    }

    /// <summary>
    /// Liefert <c>null</c> bei unbekannter Id und steht trotzdem als
    /// PlannedCowTreatment - die Nullability ist woertlich aus der EF-Fassung
    /// uebernommen. Ein PlannedCowTreatment? zoege CS8602 durch die
    /// Razor-Rumpfe und braeche die Zusage, dass sich an den Komponenten nur
    /// die Registrierung aendert.
    /// </summary>
    public PlannedCowTreatment GetById(int id)
    {
        return _cachedTreatments.ContainsKey(id) ? _cachedTreatments[id] : null!;
    }
}
