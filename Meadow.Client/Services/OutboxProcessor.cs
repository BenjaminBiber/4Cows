using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Meadow.Client.Services;

/// <summary>
/// Arbeitet die Outbox ab - strikt in seq-Reihenfolge und immer nur einen
/// Eintrag gleichzeitig.
///
/// Die Reihenfolge ist kein Ordnungsbeduerfnis. Wer offline drei Behandlungen
/// erfasst und danach eine davon loescht, braucht genau diese Abfolge auf dem
/// Server; parallel abgeschickt entscheidet das Netz, wer gewinnt. "Einer
/// gleichzeitig" kostet bei einer Handvoll Eintraege nichts und nimmt dem
/// Abgleich jede Nichtdeterminiertheit.
/// </summary>
public sealed class OutboxProcessor : IDisposable
{
    /// <summary>
    /// Exponentiell mit Deckel. Der Deckel ist der wichtigere Teil: ein Tablet
    /// im Funkloch soll nicht alle fuenf Sekunden bis zum Abend weiterfragen,
    /// und der letzte Wert greift, solange es dauert.
    /// </summary>
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    ];

    private readonly MeadowOutbox _outbox;
    private readonly MeadowLocalStore _store;
    private readonly MeadowSyncState _sync;
    private readonly DatabaseStatusService _databaseStatusService;
    private readonly HttpClient _http;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly Dictionary<MeadowEntityType, IMeadowSyncTarget> _targets;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private DotNetObjectReference<OutboxProcessor>? _self;
    private CancellationTokenSource? _wake;
    private DateTimeOffset? _wakeAt;

    public OutboxProcessor(
        MeadowOutbox outbox,
        MeadowLocalStore store,
        MeadowSyncState sync,
        DatabaseStatusService databaseStatusService,
        IEnumerable<IMeadowSyncTarget> targets,
        HttpClient http,
        ILogger<OutboxProcessor> logger)
    {
        _outbox = outbox;
        _store = store;
        _sync = sync;
        _databaseStatusService = databaseStatusService;
        _http = http;
        _logger = logger;
        _targets = targets.ToDictionary(t => t.EntityType);
    }

    /// <summary>
    /// Einmal beim App-Start, aus Program.cs. Nicht erwarten, was hier
    /// angestossen wird: der erste Durchlauf darf den Start nicht aufhalten,
    /// sonst haengt der Splash am Netz - und genau das soll er nie wieder.
    /// </summary>
    /// <remarks>
    /// Das DynamicDependency haelt <see cref="ConnectivitySignalAsync"/> vor
    /// dem Trimmer fest. Sie wird ausschliesslich aus JavaScript gerufen, also
    /// ueber Reflexion, und diesen Zugriff sieht der Trimmer nicht - die
    /// Anwendung wird mit PublishTrimmed veroeffentlicht.
    ///
    /// Faellt sie weg, gibt es keinen Uebersetzungsfehler und keine Ausnahme im
    /// Browser: die beiden haeufigsten Ausloeser - Netz wieder da, Tab wieder
    /// sichtbar - hoeren einfach auf zu feuern, und die Outbox laeuft nur noch
    /// beim App-Start. Genau die Art Fehler, die im ungetrimmten
    /// Entwicklungsbuild nie auftritt.
    /// </remarks>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(OutboxProcessor))]
    public async Task InitializeAsync()
    {
        if (!await _store.OpenAsync())
        {
            _logger.LogInformation("Ohne lokalen Speicher gibt es keine Outbox - der Abgleich bleibt aus.");
            return;
        }

        await RecoverInFlightAsync();
        await _outbox.RefreshCountersAsync();

        // Der Zeitpunkt der letzten Uebertragung ueberlebt das Schliessen der
        // App - sonst zeigte ein Statusband nach jedem Start "noch nie
        // abgeglichen", obwohl vor zehn Minuten alles hinausging.
        var lastSync = await _store.GetMetaAsync(MeadowStores.MetaLastSyncUtc);
        if (DateTimeOffset.TryParse(lastSync, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when))
        {
            _sync.NoteSync(when);
        }

        _self = DotNetObjectReference.Create(this);
        await _store.WatchConnectivityAsync(_self);

        _ = DrainAsync("start");
    }

    /// <summary>
    /// Kommt aus meadow-db.js: online-Ereignis oder ein wieder sichtbarer Tab.
    /// </summary>
    [JSInvokable("ConnectivitySignal")]
    public Task ConnectivitySignalAsync(string reason) => DrainAsync(reason);

    /// <summary>
    /// Der manuelle Knopf. Oeffentlich, weil ihn eine Komponente eines
    /// spaeteren Schrittes ruft - diese Phase fasst keine .razor-Datei an.
    /// </summary>
    public Task DrainNowAsync() => DrainAsync("manuell");

    /// <summary>
    /// Ein InFlight-Eintrag heisst: beim letzten Mal ging die App mitten im
    /// Senden aus. Ob der Server ihn bekommen hat, weiss hier niemand - und es
    /// muss auch niemand wissen: die vier Endpunkte sind Upserts auf ClientId,
    /// ein zweiter Versuch liefert dieselbe Zeile mit 200 statt 201.
    /// </summary>
    private async Task RecoverInFlightAsync()
    {
        var entries = await _outbox.LoadAsync();

        foreach (var entry in entries.Where(e => e.State == OutboxState.InFlight))
        {
            _logger.LogInformation(
                "Outbox {Seq} stand auf InFlight - wird wiederholt ({OperationId}).",
                entry.Seq, entry.OperationId);

            await _outbox.UpdateAsync(entry with { State = OutboxState.Pending, NextAttemptUtc = null });
        }
    }

    private async Task DrainAsync(string trigger)
    {
        // Kein Warten auf den Riegel, sondern ein Rueckzug: laeuft schon ein
        // Durchlauf, arbeitet der die ganze Schlange ohnehin bis zum Ende ab.
        // Ein zweiter Ausloeser haette nichts zu tun.
        if (!await _gate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var entries = await _outbox.LoadAsync();
            if (entries.Count == 0)
            {
                return;
            }

            _sync.SetDraining(true);
            _logger.LogInformation("Outbox: {Count} Eintraege, Ausloeser {Trigger}.", entries.Count, trigger);

            foreach (var entry in entries.OrderBy(e => e.Seq))
            {
                // Ein dauerhaft gescheiterter Eintrag BLOCKIERT NICHT.
                //
                // Er kommt allein nicht mehr weiter - eine 400 wegen eines
                // geloeschten Medikaments wird beim zwanzigsten Versuch
                // dieselbe 400. Wuerde er die Schlange anhalten, verloere der
                // Landwirt alles, was er danach erfasst hat, und zwar
                // wortlos. Er bleibt stehen, bis ein Mensch ihn ansieht;
                // FailedCount zeigt ihn an.
                if (entry.State == OutboxState.Failed)
                {
                    continue;
                }

                if (entry.NextAttemptUtc is { } next && next > DateTimeOffset.UtcNow)
                {
                    // Der Kopf der Schlange wartet noch - und weil die
                    // Reihenfolge gilt, warten alle dahinter mit.
                    ScheduleWake(next);
                    break;
                }

                if (!await TrySendAsync(entry))
                {
                    // Vorruebergehend gescheitert: das Netz ist weg, und es ist
                    // fuer jeden folgenden Eintrag genauso weg.
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            // Der Durchlauf laeuft losgeloest - beim App-Start, auf ein
            // Browserereignis, aus einem Wecker. Eine Ausnahme, die hier
            // herauskaeme, haette niemanden, der sie auffaengt: sie
            // verschwaende in einem unbeobachteten Task. Die Eintraege bleiben
            // liegen, der naechste Ausloeser versucht es erneut.
            _logger.LogError(ex, "Outbox-Durchlauf abgebrochen.");
        }
        finally
        {
            _sync.SetDraining(false);
            _gate.Release();
            await _outbox.RefreshCountersAsync();
        }
    }

    /// <summary>
    /// Ein Eintrag.
    /// </summary>
    /// <returns>
    /// <c>true</c>, wenn weitergemacht werden darf - also bei Erfolg UND bei
    /// dauerhaftem Fehlschlag. <c>false</c> nur bei einem voruebergehenden
    /// Fehler, denn der trifft den naechsten Eintrag genauso.
    /// </returns>
    private async Task<bool> TrySendAsync(OutboxEntry entry)
    {
        var attempt = entry with
        {
            State = OutboxState.InFlight,
            Attempts = entry.Attempts + 1,
            NextAttemptUtc = null
        };

        // Vor dem Senden festhalten. Nur deshalb kann RecoverInFlightAsync nach
        // einem Absturz sagen, dass dieser Eintrag unterwegs war.
        await _outbox.UpdateAsync(attempt);

        HttpResponseMessage response;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, MeadowStores.InsertRoute(entry.EntityType))
            {
                Content = new StringContent(entry.Payload, Encoding.UTF8, "application/json")
            };

            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Kein Netz oder Zeitueberschreitung. Beim Timeout ist offen, ob
            // der Server geschrieben hat - und genau diese Frage muss hier
            // niemand beantworten: der Upsert auf ClientId macht die
            // Wiederholung harmlos.
            _databaseStatusService.ReportFailure();
            await RetryLaterAsync(attempt, ex.Message);
            return false;
        }

        using (response)
        {
            var body = await ReadBodyAsync(response);

            if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
            {
                _databaseStatusService.ReportSuccess();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // 200 statt 201 ist kein Fehler, sondern eine Wiederholung:
                    // ein frueherer Versuch hat committet, nur die Antwort ging
                    // verloren. Derselbe Zustandsuebergang - aber KEIN zweiter
                    // Erfolgshinweis, sonst meldet die App eine Behandlung, die
                    // der Nutzer laengst gespeichert hat, ein zweites Mal.
                    _sync.CountReplay();
                    _logger.LogInformation(
                        "Outbox {Seq}: Wiederholung, der Server kannte den Vorgang bereits ({OperationId}).",
                        entry.Seq, entry.OperationId);
                }

                await CompleteAsync(attempt, body);
                return true;
            }

            if ((int)response.StatusCode >= 500)
            {
                // Der Dienst hinter der API ist gestolpert, nicht die Anfrage.
                // Dieselbe Lesart wie in HttpServiceBase.SendAsync.
                _databaseStatusService.ReportFailure();
                await RetryLaterAsync(attempt, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                return false;
            }

            // 4xx. Der Server hat geantwortet, die Leitung steht - die ANFRAGE
            // ist das Problem, und die aendert sich beim naechsten Versuch
            // nicht. Ein 409 gehoert ausdruecklich dazu: "teilweise bereits
            // uebertragen" kann kein Automat aufloesen.
            _databaseStatusService.ReportSuccess();
            await FailAsync(attempt, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
            return true;
        }
    }

    /// <summary>
    /// Der Erfolgsfall, und seine Reihenfolge ist tragend:
    ///
    /// 1. Server-Id in den Cache zurueckschreiben,
    /// 2. lokalen Speicher aktualisieren,
    /// 3. die Oberflaeche benachrichtigen,
    /// 4. ZULETZT den Outbox-Eintrag loeschen.
    ///
    /// Andersherum - erst loeschen, dann zurueckschreiben - ginge der Vorgang
    /// verloren, sobald einer der drei Schritte scheitert: die Zeile stuende
    /// dann mit ihrer vorlaeufigen -3 in der Tabelle und niemand wuesste mehr,
    /// dass sie laengst eine echte Id hat. So kann der Eintrag im schlimmsten
    /// Fall ein zweites Mal hinausgehen, und das ist genau der Fall, den der
    /// Upsert abfaengt.
    /// </summary>
    private async Task CompleteAsync(OutboxEntry entry, string body)
    {
        try
        {
            if (_targets.TryGetValue(entry.EntityType, out var target))
            {
                var rows = target.ApplySyncedRows(body);
                var store = MeadowStores.StoreOf(entry.EntityType);

                foreach (var row in rows)
                {
                    // Ersetzt die wartende Zeile am SELBEN Schluessel
                    // (clientId) - deshalb entsteht hier kein zweiter
                    // Datensatz, und das Wartemerkmal faellt mit weg.
                    await _store.PutRowAsync(store, row);
                }
            }
            else
            {
                _logger.LogError("Kein Ziel fuer {EntityType} - die Antwort wurde nicht eingetragen.", entry.EntityType);
            }
        }
        catch (Exception ex)
        {
            // Der Server hat die Zeile - nur ihr Rueckweg in den Cache ist
            // gescheitert. Der Eintrag wird trotzdem geloescht: ein zweites
            // Senden brachte dieselbe Antwort und denselben Fehler. Die
            // Meldung gleich darunter laesst die offene Seite neu laden, und
            // damit kommt die Zeile mit ihrer echten Id herein.
            _logger.LogError(ex, "Outbox {Seq}: Antwort konnte nicht eingetragen werden.", entry.Seq);
        }

        _sync.NotifyData(MeadowStores.KindOf(entry.EntityType));

        var now = DateTimeOffset.UtcNow;
        _sync.NoteSync(now);
        await _store.SetMetaAsync(MeadowStores.MetaLastSyncUtc, now.ToString("O"));

        await _outbox.RemoveAsync(entry.Seq);
        _logger.LogInformation("Outbox {Seq} uebertragen ({OperationId}).", entry.Seq, entry.OperationId);
    }

    private async Task RetryLaterAsync(OutboxEntry entry, string error)
    {
        var index = Math.Min(entry.Attempts - 1, Backoff.Length - 1);
        var delay = Backoff[Math.Max(index, 0)];
        var next = DateTimeOffset.UtcNow + delay;

        // Zustand bleibt Pending und wird NICHT Failed: "kein Netz" ist kein
        // Fehler des Eintrags. Er soll weiter versucht werden, nur seltener.
        await _outbox.UpdateAsync(entry with
        {
            State = OutboxState.Pending,
            LastError = error,
            NextAttemptUtc = next
        });

        _logger.LogWarning(
            "Outbox {Seq}: Versuch {Attempts} gescheitert ({Error}), naechster in {Delay}.",
            entry.Seq, entry.Attempts, error, delay);

        ScheduleWake(next);
    }

    private async Task FailAsync(OutboxEntry entry, string error)
    {
        await _outbox.UpdateAsync(entry with
        {
            State = OutboxState.Failed,
            LastError = error,
            NextAttemptUtc = null
        });

        _logger.LogError(
            "Outbox {Seq} dauerhaft gescheitert ({OperationId}): {Error}",
            entry.Seq, entry.OperationId, error);
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception)
        {
            // Der Rumpf ist hier Nebensache - der Statuscode steht ohnehin in
            // derselben Protokollzeile.
            return string.Empty;
        }
    }

    /// <summary>
    /// Weckt den naechsten Durchlauf. Ein bereits gesetzter, FRUEHERER Wecker
    /// bleibt stehen: sonst schoebe jeder neue Fehlschlag den naechsten Versuch
    /// weiter nach hinten, und die Schlange stuende bei anhaltendem Funkloch
    /// immer laenger still.
    /// </summary>
    private void ScheduleWake(DateTimeOffset when)
    {
        if (_wakeAt is { } already && already <= when)
        {
            return;
        }

        _wake?.Cancel();
        _wake?.Dispose();

        var cts = new CancellationTokenSource();
        _wake = cts;
        _wakeAt = when;

        _ = WakeAsync(when, cts.Token);
    }

    private async Task WakeAsync(DateTimeOffset when, CancellationToken token)
    {
        try
        {
            var delay = when - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, token);
            }

            _wakeAt = null;
            await DrainAsync("backoff");
        }
        catch (OperationCanceledException)
        {
            // Ein frueherer Wecker hat uebernommen.
        }
    }

    public void Dispose()
    {
        _wake?.Cancel();
        _wake?.Dispose();
        _self?.Dispose();
        _gate.Dispose();
    }
}
