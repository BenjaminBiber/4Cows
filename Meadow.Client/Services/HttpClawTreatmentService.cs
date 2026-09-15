using System.Collections.Immutable;
using System.Text.Json;
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
public class HttpClawTreatmentService : HttpServiceBase, IClawTreatmentService, IMeadowSyncTarget
{
    private ImmutableDictionary<int, ClawTreatment> _cachedTreatments = ImmutableDictionary<int, ClawTreatment>.Empty;

    private readonly MeadowOutbox _outbox;

    public ImmutableDictionary<int, ClawTreatment> Treatments => _cachedTreatments;

    public MeadowEntityType EntityType => MeadowEntityType.ClawTreatment;

    public HttpClawTreatmentService(
        MeadowOutbox outbox,
        HttpClient http,
        DatabaseStatusService databaseStatusService,
        ILogger<HttpClawTreatmentService> logger)
        : base(http, databaseStatusService, logger)
    {
        _outbox = outbox;
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
        var attempt = await TryPostAsync<ClawTreatment>(
            "api/claw-treatments",
            clawTreatment,
            "Failed to insert claw treatment.");

        if (attempt.IsOffline)
        {
            return await QueueOfflineAsync(clawTreatment);
        }

        var created = attempt.Value;

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
    /// Kein Netz: die Behandlung geht in die Outbox und gilt als gespeichert.
    /// Der Cache bekommt sie sofort unter ihrer vorlaeufigen, negativen Id.
    /// </summary>
    private async Task<bool> QueueOfflineAsync(ClawTreatment clawTreatment)
    {
        var queued = await _outbox.QueueInsertAsync(
            MeadowEntityType.ClawTreatment,
            [clawTreatment],
            t => t.ClientId,
            (t, provisionalId) => t.ClawTreatmentId = provisionalId);

        if (!queued)
        {
            return false;
        }

        _cachedTreatments = _cachedTreatments.SetItem(clawTreatment.ClawTreatmentId, clawTreatment);
        Logger.LogInformation("Queued claw treatment for later transmission.");
        return true;
    }

    /// <summary>
    /// Traegt die vom Server bestaetigten Zeilen ein - aufgerufen aus
    /// <see cref="OutboxProcessor"/>, bei 201 wie bei 200.
    /// </summary>
    public IReadOnlyList<object> ApplySyncedRows(string responseJson)
    {
        var rows = MeadowSyncPayload.ReadRows<ClawTreatment>(responseJson, Json);

        foreach (var row in rows)
        {
            // Erst den vorlaeufigen Schluessel weg, dann den echten setzen -
            // sonst stuende die Behandlung zweimal in der Tabelle, einmal unter
            // -3 und einmal unter 57.
            var local = _cachedTreatments.Values.FirstOrDefault(t => t.ClientId == row.ClientId);
            if (local is not null)
            {
                _cachedTreatments = _cachedTreatments.Remove(local.ClawTreatmentId);
            }

            // SetItem und NIE Add: ein parallel gelaufener Neuabruf kann die
            // echte Id laengst eingetragen haben.
            _cachedTreatments = _cachedTreatments.SetItem(row.ClawTreatmentId, row);
        }

        return rows;
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
        // Eine negative Id ist eine vorlaeufige: die Zeile wartet noch als
        // Insert und hat den Server nie gesehen. Dann wird nicht geaendert,
        // sondern der wartende Insert nachgebessert - sonst ginge ein PUT auf
        // api/claw-treatments/-3 hinaus, das nach dem Insert in eine 404 laeuft
        // und als dauerhaft gescheitert gilt. Uebertragen waere der Stand VOR
        // der Bearbeitung.
        if (clawTreatment.ClawTreatmentId < 0
            && await _outbox.UpdatePendingInsertAsync(
                clawTreatment.ClientId, MeadowStores.ClawTreatment, clawTreatment))
        {
            _cachedTreatments = _cachedTreatments.SetItem(clawTreatment.ClawTreatmentId, clawTreatment);
            Logger.LogInformation(
                "Wartende Klauenbehandlung {Id} geaendert - es geht nur EIN Anlegen hinaus.",
                clawTreatment.ClawTreatmentId);
            return true;
        }

        var route = $"api/claw-treatments/{clawTreatment.ClawTreatmentId}";

        var outcome = await TryWriteAsync(
            () => PutAsync(route, clawTreatment),
            $"Failed to update claw treatment {clawTreatment.ClawTreatmentId}.");

        if (outcome.IsOffline)
        {
            // Die Nutzlast wird JETZT serialisiert, mit dem Stand, den der
            // Nutzer gerade gespeichert hat. Sie spaeter aus dem Cache zu
            // holen hiesse, eine Aenderung von 17:00 mit dem Stand von 17:20
            // hinauszuschicken.
            var queued = await _outbox.QueueWriteAsync(
                MeadowEntityType.ClawTreatment,
                MeadowOperation.Update,
                "PUT",
                route,
                clawTreatment.ClawTreatmentId,
                [clawTreatment.ClientId],
                JsonSerializer.Serialize(clawTreatment, Json),
                rowToKeep: clawTreatment);

            if (queued)
            {
                _cachedTreatments = _cachedTreatments.SetItem(clawTreatment.ClawTreatmentId, clawTreatment);
                Logger.LogInformation(
                    "Klauenbehandlung {Id} geaendert, wartet auf die Uebertragung.", clawTreatment.ClawTreatmentId);
            }

            return queued;
        }

        var isSuccess = outcome.Ok;

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
        // Wie in UpdateDataAsync: eine wartende Zeile wird nachgebessert, nicht
        // geaendert. Der Verband gehoert zu einer Behandlung, die es
        // serverseitig noch gar nicht gibt.
        if (id < 0 && _cachedTreatments.TryGetValue(id, out var wartend))
        {
            wartend.IsBandageRemoved = true;

            if (await _outbox.UpdatePendingInsertAsync(
                    wartend.ClientId, MeadowStores.ClawTreatment, wartend))
            {
                _cachedTreatments = _cachedTreatments.SetItem(id, wartend);
                Logger.LogInformation("Verband an wartender Klauenbehandlung {Id} vermerkt.", id);
                return true;
            }

            wartend.IsBandageRemoved = false;
        }

        var route = $"api/claw-treatments/{id}/bandage-removed";

        var outcome = await TryWriteAsync(
            () => PutAsync(route),
            $"Failed to update bandage flag of claw treatment {id}.");

        // Ohne Leitung wandert der Vorgang in die Outbox, statt als
        // Fehlschlag zu enden. Der Verband ist im Stall abgenommen, ob die App
        // gerade Netz hat oder nicht - und beim naechsten Mal wuerde der
        // Landwirt ihn sonst ein zweites Mal suchen.
        if (outcome.IsOffline && _cachedTreatments.TryGetValue(id, out var offline))
        {
            offline.IsBandageRemoved = true;

            var queued = await _outbox.QueueWriteAsync(
                MeadowEntityType.ClawTreatment,
                MeadowOperation.Update,
                "PUT",
                route,
                id,
                [offline.ClientId],
                rowToKeep: offline);

            if (queued)
            {
                _cachedTreatments = _cachedTreatments.SetItem(id, offline);
                Logger.LogInformation("Verband von Klauenbehandlung {Id} wartet auf die Uebertragung.", id);
                return true;
            }

            // Nicht zwischengelagert - dann darf die Zeile auch nicht so
            // aussehen, als sei sie erledigt.
            offline.IsBandageRemoved = false;
            return false;
        }

        var isSuccess = outcome.Ok;

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

        var attempt = await TryPostAsync<RemovedResponse>(
            "api/claw-treatments/bandages-removed",
            new BandageRemovalRequest(ids),
            $"Failed to update bandage flags of {ids.Count} claw treatments.");

        // Ohne Leitung: derselbe EINE Aufruf wandert in die Outbox, mit
        // derselben Id-Liste. Ihn in N Eintraege zu zerlegen braeche die Zusage
        // des Endpunkts - er schreibt ein Update, und ein halb entfernter
        // Stapel ist genau das, wogegen die Mengenvariante gebaut wurde.
        if (attempt.IsOffline)
        {
            return await QueueBandageRemovalAsync(ids);
        }

        var response = attempt.Value;

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
    /// Die Mengenvariante ohne Leitung: EIN Outbox-Eintrag fuer den ganzen
    /// Stapel, mit der Nutzlast, die auch hinausgegangen waere.
    ///
    /// Die ServerId des Eintrags ist die erste Id des Stapels. Sie steht dort
    /// nur zur Anzeige - die Adresse traegt keine Id, die Ids stehen im Rumpf.
    ///
    /// Beruecksichtigt werden nur Zeilen, die der Cache kennt: fuer eine
    /// unbekannte Id gaebe es keine ClientId, und ohne die weiss der
    /// Zeilenpunkt nicht, welche Zeile er meint.
    /// </summary>
    private async Task<int> QueueBandageRemovalAsync(IReadOnlyCollection<int> ids)
    {
        var alle = ids
            .Where(id => _cachedTreatments.ContainsKey(id))
            .Select(id => _cachedTreatments[id])
            .ToList();

        // Wartende Zeilen gehoeren NICHT in den Stapel: ihre Ids sind
        // vorlaeufig und negativ, und der Endpunkt faende sie nie. Sie werden
        // einzeln in ihrem eigenen wartenden Insert nachgebessert.
        var wartende = alle.Where(t => t.ClawTreatmentId < 0).ToList();
        var known = alle.Where(t => t.ClawTreatmentId > 0).ToList();

        var nachgebessert = 0;
        foreach (var treatment in wartende)
        {
            treatment.IsBandageRemoved = true;

            if (await _outbox.UpdatePendingInsertAsync(
                    treatment.ClientId, MeadowStores.ClawTreatment, treatment))
            {
                _cachedTreatments = _cachedTreatments.SetItem(treatment.ClawTreatmentId, treatment);
                nachgebessert++;
            }
            else
            {
                treatment.IsBandageRemoved = false;
            }
        }

        if (known.Count == 0)
        {
            return nachgebessert;
        }

        foreach (var treatment in known)
        {
            treatment.IsBandageRemoved = true;
        }

        var queued = await _outbox.QueueWriteAsync(
            MeadowEntityType.ClawTreatment,
            MeadowOperation.Update,
            "POST",
            "api/claw-treatments/bandages-removed",
            known[0].ClawTreatmentId,
            known.Select(t => t.ClientId).ToList(),
            JsonSerializer.Serialize(new BandageRemovalRequest(known.Select(t => t.ClawTreatmentId).ToList()), Json));

        if (!queued)
        {
            foreach (var treatment in known)
            {
                treatment.IsBandageRemoved = false;
            }

            return nachgebessert;
        }

        // Die wartenden Zeilen einzeln ablegen - QueueWriteAsync nimmt nur EINE
        // Zeile entgegen, und hier sind es mehrere.
        foreach (var treatment in known)
        {
            await _outbox.MarkRowPendingAsync(MeadowStores.ClawTreatment, treatment);
        }

        _cachedTreatments = _cachedTreatments.SetItems(
            known.Select(t => new KeyValuePair<int, ClawTreatment>(t.ClawTreatmentId, t)));

        Logger.LogInformation("{Count} Verbaende warten auf die Uebertragung.", known.Count);
        return known.Count + nachgebessert;
    }

    /// <summary>
    /// Loescht eine Klauenbehandlung. Gibt wie die EF-Fassung ein blankes Task
    /// zurueck und verschluckt jeden Fehlschlag in eine Protokollzeile.
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
            if (await _outbox.CancelPendingInsertAsync(pending.ClientId, MeadowStores.ClawTreatment))
            {
                _cachedTreatments = _cachedTreatments.Remove(id);
                Logger.LogInformation("Wartende Zeile {Id} zurueckgezogen, nichts gesendet.", id);
                return;
            }
        }

        var route = $"api/claw-treatments/{id}";

        var outcome = await TryWriteAsync(
            () => DeleteAsync(route),
            $"Failed to delete claw treatment {id}.");

        // Ohne Leitung wird die Loeschung vorgemerkt, und die Zeile
        // verschwindet SOFORT - online tut sie das auch. Eine Zeile, die man
        // geloescht hat und die trotzdem stehen bleibt, wuerde ein zweites Mal
        // geloescht.
        if (outcome.IsOffline && _cachedTreatments.TryGetValue(id, out var gone))
        {
            if (await _outbox.QueueWriteAsync(
                    MeadowEntityType.ClawTreatment, MeadowOperation.Delete, "DELETE",
                    route, id, [gone.ClientId]))
            {
                _cachedTreatments = _cachedTreatments.Remove(id);
                Logger.LogInformation("Loeschung von Klauenbehandlung {Id} wartet auf die Uebertragung.", id);
            }

            return;
        }

        var isSuccess = outcome.Ok;

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
