using System.Net.Http.Json;
using System.Text.Json;
using Meadow.Shared;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Was sich ueber die dreizehn HTTP-Dienste wiederholt: die Anfrage stellen,
/// den Verbindungszustand melden, jede Ausnahme abfangen und den Rumpf
/// deserialisieren.
///
/// Einmal formuliert und nicht dreizehnmal abgeschrieben. Dreizehn Kopien
/// desselben try/catch waeren dreizehn Gelegenheiten, eines davon anders zu
/// schreiben - und der Unterschied faellt erst auf, wenn die API einmal nicht
/// erreichbar ist. Genau dann soll jeder Dienst gleich reagieren.
///
/// Die Fehlwerte stehen NICHT hier, sondern an jeder Methode: sie sind je
/// Methode verschieden (false, "--", int.MinValue, ClawFinding.FailedId, null,
/// new Udder()) und aus der EF-Fassung uebernommen.
/// </summary>
public abstract class HttpServiceBase
{
    /// <summary>
    /// Der eine JSON-Vertrag, gemeinsam mit dem Server. Niemals die
    /// Voreinstellungen von ReadFromJsonAsync: MeadowJson setzt
    /// DefaultIgnoreCondition auf Never, und nur deshalb kommt eine UdderId 0
    /// als 0 an und nicht als int.MinValue aus dem parameterlosen Konstruktor.
    ///
    /// static readonly, weil JsonSerializerOptions beim ersten Gebrauch seine
    /// Metadaten zwischenspeichert - eine frische Instanz je Aufruf wirft
    /// diesen Zwischenspeicher jedes Mal weg.
    /// </summary>
    protected static readonly JsonSerializerOptions Json = MeadowJson.CreateOptions();

    private readonly DatabaseStatusService _databaseStatusService;

    protected HttpClient Http { get; }

    /// <summary>
    /// ILogger und NICHT der Serilog-Protokolldienst der Serverseite: der
    /// liegt in der EF-Schicht, schreibt in eine Datei und in MariaDB, und
    /// beides gibt es im Browser nicht. Meadow.Client hat bewusst keine
    /// Projektreferenz dorthin.
    /// </summary>
    protected ILogger Logger { get; }

    protected HttpServiceBase(HttpClient http, DatabaseStatusService databaseStatusService, ILogger logger)
    {
        Http = http;
        _databaseStatusService = databaseStatusService;
        Logger = logger;
    }

    /// <summary>
    /// Der einzige Ort, an dem eine Anfrage tatsaechlich hinausgeht, und
    /// deshalb der einzige, der den Verbindungszustand meldet.
    ///
    /// Die Aufteilung spiegelt die EF-Seite: dort meldet ReportFailure nur der
    /// catch-Zweig, also eine geworfene Ausnahme. Eine Abfrage, die sauber
    /// laeuft und 0 Zeilen trifft, meldet ReportSuccess. Uebersetzt heisst das:
    ///
    /// - 5xx = der Dienst hinter der API ist gestolpert. Das ist die Ausnahme,
    ///   die dort geworfen wurde, also ReportFailure.
    /// - alles andere, was ankommt (auch 404, 409, 400, 503) = die API hat
    ///   geantwortet. Das entspricht affectedRows == 0, also ReportSuccess.
    /// - gar nichts (Ausnahme aus SendAsync) = die Leitung ist weg. Das faengt
    ///   GuardAsync ab und meldet dort ReportFailure.
    /// </summary>
    protected async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        var response = await Http.SendAsync(request);

        // Eine Antwort aus dem lokalen Speicher traegt eine 200, damit der
        // Aufrufer sie liest - sie ist aber kein Beleg dafuer, dass die API
        // erreichbar ist. Ohne diese Zeile zeigte das Verbindungsband
        // "verbunden", waehrend die App aus der Konserve laeuft: der
        // unangenehmste aller Zustaende, weil er den Nutzer glauben laesst,
        // seine Eingabe sei angekommen.
        if (response.Headers.Contains(MeadowOfflineHandler.OfflineHeader))
        {
            _databaseStatusService.ReportFailure();
        }
        else if ((int)response.StatusCode >= 500)
        {
            _databaseStatusService.ReportFailure();
        }
        else
        {
            _databaseStatusService.ReportSuccess();
        }

        return response;
    }

    /// <summary>
    /// Fuer die eine Methode, die ihr try/catch selbst schreiben muss:
    /// HttpKPIService.GetKPIValue reicht die Ausnahme auf Wunsch weiter und
    /// kann deshalb nicht durch <see cref="GuardAsync{TResult}"/> laufen, das
    /// jede Ausnahme schluckt. Der Zustandsbericht darf dabei nicht ausfallen.
    /// </summary>
    protected void ReportFailure() => _databaseStatusService.ReportFailure();

    /// <summary>
    /// Ob der letzte Aufruf durchkam. Fuer die fuenf Upsert-Methoden, die ohne
    /// Verbindung gar nicht erst fragen duerfen: sie WUERDEN einen Eintrag
    /// anlegen, und ein Medikament, das nur dieses Telefon kennt, haengt danach
    /// an einer Behandlung, die der Server nie annehmen kann.
    ///
    /// Dieselbe Quelle, die DatabaseConnectionState liest - es gibt also keinen
    /// zweiten Verbindungsbegriff.
    /// </summary>
    protected bool IsConnected => _databaseStatusService.IsConnected;

    protected Task<HttpResponseMessage> GetAsync(string uri)
        => SendAsync(Request(HttpMethod.Get, uri, body: null));

    protected Task<HttpResponseMessage> PostAsync(string uri, object? body = null)
        => SendAsync(Request(HttpMethod.Post, uri, body));

    protected Task<HttpResponseMessage> PutAsync(string uri, object? body = null)
        => SendAsync(Request(HttpMethod.Put, uri, body));

    protected Task<HttpResponseMessage> DeleteAsync(string uri)
        => SendAsync(Request(HttpMethod.Delete, uri, body: null));

    /// <summary>
    /// Die Klammer um jeden Aufruf. Hier endet die HttpRequestException einer
    /// nicht erreichbaren API.
    ///
    /// Ohne sie riss ein Netzfehler die Seite ab, statt einen roten Toast zu
    /// zeigen: die Razor-Dateien pruefen "if (!await svc.InsertDataAsync(x))"
    /// und rechnen mit einem Rueckgabewert, nicht mit einer Ausnahme. Die
    /// EF-Fassung macht es genauso - dort faengt jede Methode ihre
    /// DbUpdateException selbst ab.
    /// </summary>
    protected async Task<TResult> GuardAsync<TResult>(
        Func<Task<TResult>> operation,
        TResult onFailure,
        string failureMessage)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            Logger.LogError(ex, "{Message}", failureMessage);
            return onFailure;
        }
    }

    /// <summary>
    /// Eine ganze Sammlung holen. <c>null</c> heisst "ging schief" - der
    /// Aufrufer laesst dann seinen Cache stehen, statt ihn zu leeren. Dieselbe
    /// Haltung wie in der EF-Fassung, deren GetAllDataAsync im Fehlerfall gar
    /// nichts zuweist.
    /// </summary>
    protected Task<List<TItem>?> GetListAsync<TItem>(string uri, string failureMessage)
        => GuardAsync<List<TItem>?>(
            async () =>
            {
                using var response = await GetAsync(uri);
                if (!response.IsSuccessStatusCode)
                {
                    await LogStatusAsync(response, failureMessage);
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<List<TItem>>(Json);
            },
            null,
            failureMessage);

    /// <summary>
    /// Ein GET, dessen Rumpf ein Objekt ist (Woerterbuecher, Zustandsberichte).
    /// <c>null</c> heisst wieder "ging schief".
    /// </summary>
    protected Task<TValue?> GetJsonAsync<TValue>(string uri, string failureMessage)
        where TValue : class
        => ReadAsync<TValue>(() => GetAsync(uri), failureMessage);

    /// <summary>
    /// Ein Schreibaufruf, dessen Antwort keinen Rumpf hat (204) oder dessen
    /// Rumpf nicht gebraucht wird. <c>true</c> nur bei 2xx.
    ///
    /// Eine 404 oder 409 ist hier KEINE Ausnahme, sondern ein false - genau
    /// wie affectedRows == 0 in der EF-Fassung. Der Grund steht in der
    /// Protokollzeile, weil ihn die Dienste auch dort nicht zurueckgeben.
    /// </summary>
    protected Task<bool> WriteAsync(Func<Task<HttpResponseMessage>> send, string failureMessage)
        => GuardAsync(
            async () =>
            {
                using var response = await send();
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                await LogStatusAsync(response, failureMessage);
                return false;
            },
            false,
            failureMessage);

    /// <summary>
    /// Ein Aufruf, dessen Antwortrumpf gelesen wird - der erzeugte Datensatz
    /// eines POST, das { id } der Upserts, das { removed } der Mengenvariante.
    /// <c>null</c> heisst "ging schief".
    /// </summary>
    /// <param name="notFoundIsExpected">
    /// Fuer die beiden GetById-Methoden, die einen Fehltreffer als regulaeres
    /// Ergebnis kennen: eine 404 ist dort keine Stoerung, sondern die Antwort
    /// "gibt es nicht", und soll das Protokoll nicht mit Fehlern fuellen. Der
    /// Rueckgabewert bleibt null, der Aufrufer macht daraus seine frische
    /// Instanz.
    /// </param>
    protected Task<TResponse?> ReadAsync<TResponse>(
        Func<Task<HttpResponseMessage>> send,
        string failureMessage,
        bool notFoundIsExpected = false)
        where TResponse : class
        => GuardAsync<TResponse?>(
            async () =>
            {
                using var response = await send();
                if (!response.IsSuccessStatusCode)
                {
                    if (notFoundIsExpected && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Logger.LogDebug("{Message} HTTP 404.", failureMessage);
                        return null;
                    }

                    await LogStatusAsync(response, failureMessage);
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<TResponse>(Json);
            },
            null,
            failureMessage);

    /// <summary>
    /// Der erzeugte Datensatz eines POST. Aus ihm liest der Aufrufer den
    /// Schluessel, den die Datenbank vergeben hat.
    /// </summary>
    protected Task<TEntity?> CreateAsync<TEntity>(string uri, TEntity body, string failureMessage)
        where TEntity : class
        => ReadAsync<TEntity>(() => PostAsync(uri, body), failureMessage);

    /// <summary>
    /// Ergebnis eines Schreibaufrufs, der zwischen zwei Fehlschlaegen
    /// unterscheiden muss, die sonst gleich aussehen.
    /// </summary>
    /// <param name="Value">Der Antwortrumpf, oder <c>null</c> bei Fehlschlag.</param>
    /// <param name="IsOffline">
    /// <c>true</c> NUR, wenn gar keine Antwort kam. Alles, was der Server
    /// beantwortet hat - auch eine 400 oder 409 -, ist <c>false</c>.
    /// </param>
    protected readonly record struct WriteAttempt<TResponse>(TResponse? Value, bool IsOffline)
        where TResponse : class;

    /// <summary>
    /// Wie <see cref="CreateAsync{TEntity}"/>, aber es sagt, WARUM es nicht
    /// geklappt hat.
    ///
    /// GuardAsync schluckt jede Ausnahme und liefert einen Fehlwert - fuer
    /// dreizehn Dienste genau richtig, fuer die vier Insert-Methoden der
    /// Behandlungen nicht mehr: dort haengt an dem Unterschied, ob die Zeile in
    /// die Outbox wandert und der Dialog sich schliesst, oder ob der Nutzer den
    /// Fehler sehen muss.
    ///
    /// Ein 4xx ist AUSDRUECKLICH kein Offline-Fall. Eine 400 wegen eines
    /// unbekannten Medikaments in die Outbox zu legen hiesse, sie alle fuenf
    /// Minuten erneut abzulehnen, waehrend der Nutzer glaubt, gespeichert zu
    /// haben.
    /// </summary>
    protected async Task<WriteAttempt<TResponse>> TryPostAsync<TResponse>(
        string uri,
        object body,
        string failureMessage)
        where TResponse : class
    {
        try
        {
            using var response = await PostAsync(uri, body);

            if (!response.IsSuccessStatusCode)
            {
                await LogStatusAsync(response, failureMessage);
                return new WriteAttempt<TResponse>(null, false);
            }

            return new WriteAttempt<TResponse>(await response.Content.ReadFromJsonAsync<TResponse>(Json), false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Die Leitung ist weg. Beim Timeout ist offen, ob der Server
            // geschrieben hat - das darf hier offen bleiben: die vier
            // Endpunkte sind Upserts auf ClientId, die Wiederholung aus der
            // Outbox liefert dieselbe Zeile mit 200 statt 201.
            ReportFailure();
            Logger.LogWarning(ex, "{Message} Kein Netz.", failureMessage);
            return new WriteAttempt<TResponse>(null, true);
        }
        catch (Exception ex)
        {
            // Alles andere ist ein echter Fehler dieses Aufrufs - etwa ein
            // Rumpf, der sich nicht lesen laesst. Nicht in die Outbox damit.
            ReportFailure();
            Logger.LogError(ex, "{Message}", failureMessage);
            return new WriteAttempt<TResponse>(null, false);
        }
    }

    /// <summary>
    /// Ein Pfadabschnitt, der aus Daten stammt. Cow_ID ist ein vom Client
    /// vergebener String und ein Einstellungsschluessel ebenso - beides kann
    /// Zeichen enthalten, die in einer URL etwas anderes bedeuten.
    /// </summary>
    protected static string Segment(string value) => Uri.EscapeDataString(value);

    /// <summary>
    /// Der Rumpf einer Fehlerantwort ist ProblemDetails und traegt den Grund,
    /// den die Dienste selbst nicht zurueckgeben. Er geht ins Protokoll und
    /// nicht nach oben: nach oben geht der dokumentierte Fehlwert, damit die
    /// Razor-Seiten ihren roten Toast zeigen wie heute.
    /// </summary>
    private async Task LogStatusAsync(HttpResponseMessage response, string what)
    {
        string detail;
        try
        {
            detail = await response.Content.ReadAsStringAsync();
        }
        catch
        {
            // Der Grund der Antwort ist hier schon Nebensache - der Statuscode
            // steht ohnehin in derselben Protokollzeile.
            detail = string.Empty;
        }

        Logger.LogError(
            "{Message} HTTP {StatusCode} {ReasonPhrase}. {Detail}",
            what,
            (int)response.StatusCode,
            response.ReasonPhrase,
            detail);
    }

    private static HttpRequestMessage Request(HttpMethod method, string uri, object? body)
    {
        var request = new HttpRequestMessage(method, uri);

        if (body is not null)
        {
            // Mit dem LAUFZEITTYP und nicht mit object: sonst serialisiert
            // System.Text.Json die Eigenschaften von object, also keine.
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        return request;
    }
}
