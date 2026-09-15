using System.Collections.Immutable;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die geplanten Klauenbehandlungen ueber HTTP. Gegenstueck zum EF-Dienst
/// PClawTreatmentService.
/// </summary>
public class HttpPClawTreatmentService : HttpServiceBase, IPClawTreatmentService, IMeadowSyncTarget
{
    private ImmutableDictionary<int, PlannedClawTreatment> _cachedTreatments = ImmutableDictionary<int, PlannedClawTreatment>.Empty;

    private readonly MeadowOutbox _outbox;

    public ImmutableDictionary<int, PlannedClawTreatment> Treatments => _cachedTreatments;

    public MeadowEntityType EntityType => MeadowEntityType.PlannedClawTreatment;

    public HttpPClawTreatmentService(
        MeadowOutbox outbox,
        HttpClient http,
        DatabaseStatusService databaseStatusService,
        ILogger<HttpPClawTreatmentService> logger)
        : base(http, databaseStatusService, logger)
    {
        _outbox = outbox;
    }

    public async Task GetAllDataAsync()
    {
        var treatments = await GetListAsync<PlannedClawTreatment>(
            "api/planned-claw-treatments",
            "Failed to load planned claw treatments.");

        if (treatments is null)
        {
            return;
        }

        _cachedTreatments = treatments.ToImmutableDictionary(t => t.PlannedClawTreatmentId);
        Logger.LogInformation("Loaded {Count} planned claw treatments.", _cachedTreatments.Count);
    }

    /// <summary>
    /// Legt eine geplante Klauenbehandlung an und traegt die erzeugte Id in die
    /// UEBERGEBENE Instanz nach.
    ///
    /// Derselbe Fall wie in HttpClawTreatmentService: der Schluessel ist die
    /// Identity, und ohne diesen Rueckweg stuende bei zwei Inserts hintereinander
    /// zweimal die 0 im Cache - der zweite Eintrag ersetzte still den ersten.
    /// </summary>
    public async Task<bool> InsertDataAsync(PlannedClawTreatment clawTreatment)
    {
        var attempt = await TryPostAsync<PlannedClawTreatment>(
            "api/planned-claw-treatments",
            clawTreatment,
            "Failed to insert planned claw treatment.");

        if (attempt.IsOffline)
        {
            return await QueueOfflineAsync(clawTreatment);
        }

        var created = attempt.Value;

        if (created is null)
        {
            return false;
        }

        clawTreatment.PlannedClawTreatmentId = created.PlannedClawTreatmentId;

        // SetItem statt Add, und kein Neuladen: gespiegelt aus der EF-Fassung.
        _cachedTreatments = _cachedTreatments.SetItem(clawTreatment.PlannedClawTreatmentId, clawTreatment);
        Logger.LogInformation("Inserted planned claw treatment {Id}.", clawTreatment.PlannedClawTreatmentId);
        return true;
    }

    /// <summary>
    /// Kein Netz: die Planung geht in die Outbox und gilt als gespeichert. Der
    /// Cache bekommt sie sofort unter ihrer vorlaeufigen, negativen Id.
    /// </summary>
    private async Task<bool> QueueOfflineAsync(PlannedClawTreatment clawTreatment)
    {
        var queued = await _outbox.QueueInsertAsync(
            MeadowEntityType.PlannedClawTreatment,
            [clawTreatment],
            t => t.ClientId,
            (t, provisionalId) => t.PlannedClawTreatmentId = provisionalId);

        if (!queued)
        {
            return false;
        }

        _cachedTreatments = _cachedTreatments.SetItem(clawTreatment.PlannedClawTreatmentId, clawTreatment);
        Logger.LogInformation("Queued planned claw treatment for later transmission.");
        return true;
    }

    /// <summary>
    /// Traegt die vom Server bestaetigten Zeilen ein - aufgerufen aus
    /// <see cref="OutboxProcessor"/>, bei 201 wie bei 200.
    /// </summary>
    public IReadOnlyList<object> ApplySyncedRows(string responseJson)
    {
        var rows = MeadowSyncPayload.ReadRows<PlannedClawTreatment>(responseJson, Json);

        foreach (var row in rows)
        {
            // Erst den vorlaeufigen Schluessel weg, dann den echten setzen -
            // sonst stuende die Planung zweimal in der Tabelle, einmal unter
            // -3 und einmal unter 57.
            var local = _cachedTreatments.Values.FirstOrDefault(t => t.ClientId == row.ClientId);
            if (local is not null)
            {
                _cachedTreatments = _cachedTreatments.Remove(local.PlannedClawTreatmentId);
            }

            // SetItem und NIE Add: ein parallel gelaufener Neuabruf kann die
            // echte Id laengst eingetragen haben.
            _cachedTreatments = _cachedTreatments.SetItem(row.PlannedClawTreatmentId, row);
        }

        return rows;
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
            if (await _outbox.CancelPendingInsertAsync(pending.ClientId, MeadowStores.PlannedClawTreatment))
            {
                _cachedTreatments = _cachedTreatments.Remove(id);
                Logger.LogInformation("Wartende Zeile {Id} zurueckgezogen, nichts gesendet.", id);
                return true;
            }
        }

        var route = $"api/planned-claw-treatments/{id}";

        var outcome = await TryWriteAsync(
            () => DeleteAsync(route),
            $"Failed to delete planned claw treatment {id}.");

        // Ohne Leitung wird die Loeschung vorgemerkt, und die Zeile
        // verschwindet SOFORT - online tut sie das auch.
        if (outcome.IsOffline && _cachedTreatments.TryGetValue(id, out var gone))
        {
            if (!await _outbox.QueueWriteAsync(
                    MeadowEntityType.PlannedClawTreatment, MeadowOperation.Delete, "DELETE",
                    route, id, [gone.ClientId]))
            {
                return false;
            }

            _cachedTreatments = _cachedTreatments.Remove(id);
            Logger.LogInformation("Loeschung von geplanter Klauenbehandlung {Id} wartet auf die Uebertragung.", id);
            return true;
        }

        var isSuccess = outcome.Ok;

        if (isSuccess && _cachedTreatments.ContainsKey(id))
        {
            _cachedTreatments = _cachedTreatments.Remove(id);
            Logger.LogInformation("Removed planned claw treatment {Id}.", id);
        }

        return isSuccess;
    }

    /// <summary>
    /// Liefert <c>null</c> bei unbekannter Id und steht trotzdem als
    /// PlannedClawTreatment - die Nullability ist woertlich aus der EF-Fassung
    /// uebernommen.
    /// </summary>
    public PlannedClawTreatment GetById(int id)
    {
        return _cachedTreatments.ContainsKey(id) ? _cachedTreatments[id] : null!;
    }
}
