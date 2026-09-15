using System.Net;
using System.Text;
using Meadow.Client.Components.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Der Draht zwischen HTTP und lokalem Speicher. Er tut zweierlei:
///
/// - Jede erfolgreiche Tabellenantwort wird als Schnappschuss abgelegt.
/// - Faellt eine Tabellenanfrage aus, weil kein Netz da ist, antwortet der
///   Schnappschuss an ihrer Stelle.
///
/// Warum hier und nicht in den zwoelf Diensten: dort waeren es zwoelf Kopien
/// derselben vier Zeilen, und die dreizehn GetAllDataAsync sind der Ort, an dem
/// heute schon steht, was eine Tabelle ist. Ein DelegatingHandler sieht
/// dieselben Anfragen, kennt aber nur EINE Regel - und der Rumpf geht
/// unveraendert durch denselben Deserialisierer wie sonst. Die Dienste merken
/// vom lokalen Speicher nichts; ihr Cache fuellt sich wie immer, nur eben aus
/// IndexedDB.
///
/// Damit ist Punkt 1 des Starts erledigt, ohne dass irgendjemand eine
/// Ladereihenfolge von Hand schreiben muss: die erste Seite ruft ihre
/// Ensure-Methode, und was ankommt, kommt aus dem Speicher.
/// </summary>
public sealed class MeadowOfflineHandler : DelegatingHandler
{
    /// <summary>
    /// Merkt eine Antwort, die aus dem lokalen Speicher stammt.
    ///
    /// Sie traegt eine 200, weil der Aufrufer sie lesen soll - aber sie ist
    /// kein Beleg dafuer, dass die API erreichbar ist. HttpServiceBase.SendAsync
    /// liest genau diesen Kopf, damit das Verbindungsband nicht "verbunden"
    /// zeigt, waehrend die App aus der Konserve laeuft.
    /// </summary>
    public const string OfflineHeader = "X-Meadow-Offline";

    private readonly MeadowLocalStore _store;
    private readonly MeadowSyncState _sync;
    private readonly ILogger<MeadowOfflineHandler> _logger;

    /// <summary>
    /// Der zuletzt in DIESER Sitzung gesehene X-Data-Version-Wert.
    /// <c>null</c> heisst "noch keine Antwort gesehen".
    /// </summary>
    private string? _sessionVersion;

    public MeadowOfflineHandler(
        MeadowLocalStore store,
        MeadowSyncState sync,
        ILogger<MeadowOfflineHandler> logger)
    {
        _store = store;
        _sync = sync;
        _logger = logger;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var store = request.Method == HttpMethod.Get
            ? MeadowStores.ForPath(request.RequestUri?.AbsolutePath)
            : null;

        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            await NoteDataVersionAsync(response);

            if (store is not null && response.IsSuccessStatusCode)
            {
                await MirrorAsync(store, response, cancellationToken);
            }

            return response;
        }
        // Nicht bei einem ABGEBROCHENEN Aufruf: dort hat jemand die Anfrage
        // zurueckgezogen, und eine Antwort aus der Konserve waere eine Antwort
        // auf eine Frage, die niemand mehr stellt. Nur der Zeitueberschreitung
        // und dem Verbindungsfehler wird geholfen.
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && store is not null
                                   && !cancellationToken.IsCancellationRequested)
        {
            var json = await _store.ReadTableJsonAsync(store);
            if (json is null)
            {
                // Nichts gespeichert - dann ist der Fehlschlag der ehrliche
                // Ausgang, und die Dienste lassen ihren Cache stehen.
                throw;
            }

            _logger.LogInformation("Tabelle {Store} aus dem lokalen Speicher beantwortet.", store);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                Headers = { { OfflineHeader, "1" } }
            };
        }
    }

    /// <summary>
    /// Legt den Antwortrumpf als Schnappschuss ab.
    ///
    /// Der Rumpf wird dabei EINMAL gelesen und durch denselben Inhalt ersetzt -
    /// ein HttpContent laesst sich nicht zweimal streamen, und der Aufrufer
    /// hinter diesem Handler will ihn noch haben.
    /// </summary>
    private async Task MirrorAsync(string store, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string json;

        try
        {
            json = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Antwort fuer {Store} konnte nicht gelesen werden.", store);
            return;
        }

        var replacement = new StringContent(json, Encoding.UTF8, "application/json");

        // Die Kopfzeilen des Rumpfes mitnehmen, ohne den frisch gesetzten
        // Content-Type zu verlieren: eine doppelte Kopfzeile waere ein
        // Formatfehler, kein Zusatz.
        foreach (var header in response.Content.Headers)
        {
            if (!string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        response.Content.Dispose();
        response.Content = replacement;

        // keepPending nur fuer die vier Behandlungstabellen: die Server-Kopie
        // ueberschreibt ihren eigenen Datensatz (gleiche clientId), die noch
        // wartende Zeile bleibt unberuehrt stehen. Genau dafuer ist der keyPath
        // clientId da.
        await _store.WriteTableJsonAsync(store, json, MeadowStores.HasPendingRows(store));
    }

    /// <summary>
    /// X-Data-Version steht auf JEDER Antwort - auch auf GETs und Fehlern.
    ///
    /// Weicht der Wert ab, hat jemand anders geschrieben (oder der Server ist
    /// neu gestartet; die Boot-Id im Token faengt das mit ab). Dann sind die
    /// Caches dieses Tabs veraltet, und zwar lautlos: eine Zeile mit einer Id,
    /// die es nicht mehr gibt, sieht aus wie jede andere.
    ///
    /// Beim ERSTEN Wert einer Sitzung wird nur invalidiert, nicht gemeldet:
    /// zu diesem Zeitpunkt ist noch nichts geladen, und eine Meldung an alle
    /// vier Datenarten liesse die gerade ladende Seite ein zweites Mal laden.
    /// </summary>
    private async Task NoteDataVersionAsync(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-Data-Version", out var values))
        {
            return;
        }

        var token = values.FirstOrDefault();
        if (string.IsNullOrEmpty(token) || token == _sessionVersion)
        {
            return;
        }

        var isFirst = _sessionVersion is null;
        _sessionVersion = token;

        var stored = await _store.GetMetaAsync(MeadowStores.MetaDataVersion);
        await _store.SetMetaAsync(MeadowStores.MetaDataVersion, token);

        if (isFirst && stored == token)
        {
            // Derselbe Stand wie beim letzten Mal - die Schnappschuesse sind
            // aktuell, es gibt nichts zu verwerfen.
            return;
        }

        _logger.LogInformation("Fremder Datenstand ({Token}) - Caches werden verworfen.", token);
        _sync.NotifyDataVersionChanged();

        if (isFirst)
        {
            return;
        }

        // Ein vollstaendiger Neuabruf der offenen Seite. Die Seiten haengen an
        // MeadowDataChanges und rufen dort ihr GetAllDataAsync - dasselbe, was
        // nach einer eigenen Aenderung passiert.
        foreach (var kind in Enum.GetValues<MeadowDataKind>())
        {
            _sync.NotifyData(kind);
        }
    }
}
