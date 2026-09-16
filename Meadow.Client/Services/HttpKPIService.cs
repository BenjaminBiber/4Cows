using System.Collections.Immutable;
using System.Net.Http.Json;
using Meadow.Shared.Kpi;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die Kennzahlen ueber HTTP. Gegenstueck zum EF-Dienst KPIService.
///
/// GetKPIValueAsync(DatabaseContext, KPI, bool) hat hier kein Gegenstueck und
/// steht auch nicht auf der Naht: sie nimmt einen DatabaseContext, weil sie
/// sich eine Verbindung ueber alle SQL-Kennzahlen eines Dashboards teilt. Die
/// Ersparnis, um die es dabei ging, loest <see cref="GetDashboardAsync"/> hier
/// von innen - siehe dort.
/// </summary>
public class HttpKPIService : HttpServiceBase, IKPIService
{
    private ImmutableDictionary<int, KPI> _cachedKPIs = ImmutableDictionary<int, KPI>.Empty;

    /// <summary>
    /// Baut die Zeilen einer Datenquelle aus den Caches der neun anderen
    /// Dienste. Kommt ueber den Konstruktor herein, genau wie in der
    /// EF-Fassung - der Typ liegt in Meadow.Shared und haengt an keiner
    /// Datenbank.
    /// </summary>
    private readonly KpiRowProvider _rowProvider;

    public ImmutableDictionary<int, KPI> KPIs => _cachedKPIs;

    public HttpKPIService(
        KpiRowProvider rowProvider,
        HttpClient http,
        DatabaseStatusService databaseStatusService,
        ILogger<HttpKPIService> logger)
        : base(http, databaseStatusService, logger)
    {
        _rowProvider = rowProvider;
    }

    public async Task GetAllDataAsync()
    {
        var kpis = await GetListAsync<KPI>("api/kpis", "Failed to load KPIs.");
        if (kpis is null)
        {
            return;
        }

        _cachedKPIs = kpis.ToImmutableDictionary(k => k.KPIId);
        Logger.LogInformation("Loaded {Count} KPIs.", _cachedKPIs.Count);
    }

    /// <summary>
    /// Legt eine Kennzahl an und traegt die erzeugte Id in die UEBERGEBENE
    /// Instanz nach - serverseitig tut EF genau das.
    /// </summary>
    public async Task<bool> InsertDataAsync(KPI KPI)
    {
        var created = await CreateAsync("api/kpis", KPI, $"Failed to insert KPI {KPI.Title}.");
        if (created is null)
        {
            return false;
        }

        KPI.KPIId = created.KPIId;

        // SetItem statt Add, und kein Neuladen: gespiegelt aus der EF-Fassung.
        _cachedKPIs = _cachedKPIs.SetItem(KPI.KPIId, KPI);
        Logger.LogInformation("Inserted KPI {Title}.", KPI.Title);
        return true;
    }

    /// <summary>
    /// Der Wert EINER gespeicherten Kennzahl, gerechnet vom Server.
    ///
    /// Der Anfragerumpf ist leer: das Skript kommt aus der Datenbankzeile, nie
    /// aus der Anfrage. Der Skript-Guard (KpiScriptGuard) laeuft deshalb dort
    /// und wird hier NICHT noch einmal nachgebaut - eine zweite Fassung
    /// derselben Regel waere die naechste, die auseinanderlaeuft.
    ///
    /// "--" ist der dokumentierte Fehlwert und heisst "leeres Ergebnis ODER
    /// Fehler"; die beiden lassen sich auf diesem Weg nicht unterscheiden.
    /// Derselbe Wert wie in der EF-Fassung.
    ///
    /// <paramref name="throwError"/> deckt hier NUR den Aufruf ab: einen
    /// Netzfehler, eine unbekannte Kennzahl, eine Fehlerantwort. Ein
    /// gescheitertes SKRIPT kommt weiterhin als "--" an, weil der Endpunkt
    /// throwError nicht weiterreicht - er ruft GetKPIValue ohne das Kennzeichen
    /// auf und reicht das Ergebnis unveraendert durch.
    /// </summary>
    public async Task<string> GetKPIValue(KPI kpi, bool throwError = false)
    {
        // Bewusst aufgeteilt, wie in der EF-Fassung: das erste try deckt nur
        // ab, dass die Anfrage ueberhaupt hinausgeht. Eine ANTWORT mit
        // Fehlercode ist etwas anderes als eine tote Leitung - SendAsync hat
        // den Zustand dafuer schon richtig gemeldet, und ein pauschales
        // ReportFailure im catch machte aus einer 404 faelschlich einen
        // Verbindungsabbruch.
        HttpResponseMessage response;
        try
        {
            response = await PostAsync($"api/kpi/{kpi.KPIId}/value");
        }
        catch (Exception ex)
        {
            // Von Hand gemeldet, weil diese Methode als einzige nicht durch
            // GuardAsync laufen kann: sie muss die Ausnahme auf Wunsch
            // weiterreichen, und GuardAsync schluckt jede.
            ReportFailure();

            if (throwError)
            {
                throw;
            }

            Logger.LogError(ex, "Failed to reach the KPI endpoint for {KPIId}.", kpi.KPIId);
            return "--";
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                var message =
                    $"Die Kennzahl {kpi.KPIId} konnte nicht berechnet werden (HTTP {(int)response.StatusCode}). {detail}";

                Logger.LogError("{Message}", message);

                if (throwError)
                {
                    throw new InvalidOperationException(message);
                }

                return "--";
            }

            try
            {
                var value = await response.Content.ReadFromJsonAsync<KpiValueResponse>(Json);
                return value?.Value ?? "--";
            }
            catch (Exception ex)
            {
                ReportFailure();

                if (throwError)
                {
                    throw;
                }

                Logger.LogError(ex, "Error while getting KPI-Value for {KPIId}.", kpi.KPIId);
                return "--";
            }
        }
    }

    /// <summary>
    /// Prueft ein Skript, ohne es zu speichern - in zwei Stufen.
    ///
    /// Stufe eins laeuft IMMER und ohne Netz: KpiScriptGuard ist reine Logik aus Meadow.Shared und
    /// faengt genau die haeufigen Faelle ab (kein SELECT, mehr als eine Anweisung, INTO OUTFILE).
    /// Das ist die Pruefung, die der Dialog bisher erst beim SPEICHERN machte.
    ///
    /// Stufe zwei fuehrt es tatsaechlich aus - aber nur, wenn der Server das erlaubt. Ist es
    /// abgeschaltet (die Vorgabe, und zwar zu Recht: die Anwendung hat keine Authentifizierung und
    /// verbindet als root), kommt 403 zurueck und daraus wird ein HINWEIS, kein Fehler. Kaputt ist
    /// nichts - es wurde nur nicht gelaufen.
    ///
    /// Und es prueft das Skript AUS DEM EDITOR. Der alte Knopf rief den Wert-Endpunkt fuer die
    /// gespeicherte Zeile ab; bei einer neuen Kennzahl gab es die noch gar nicht.
    /// </summary>
    public async Task<KpiScriptCheck> CheckScriptAsync(string? script)
    {
        var rejection = KpiScriptGuard.Reject(script);
        if (rejection is not null)
        {
            return KpiScriptCheck.Rejected(rejection);
        }

        HttpResponseMessage response;
        try
        {
            response = await PostAsync("api/kpi/validate", new { Script = script });
        }
        catch (Exception ex)
        {
            ReportFailure();
            Logger.LogError(ex, "Failed to reach the KPI validation endpoint.");
            // Offline ist das Skript nicht pruefbar, aber auch nicht widerlegt - dasselbe
            // "es wurde nicht gelaufen" wie bei abgeschalteter Pruefung.
            return KpiScriptCheck.Skipped();
        }

        using (response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return KpiScriptCheck.Skipped();
            }

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return KpiScriptCheck.Rejected(body);
            }

            // { "value": "..." } - nur das eine Feld, deshalb kein eigener Antworttyp.
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(body);
                return KpiScriptCheck.Ran(
                    json.RootElement.TryGetProperty("value", out var value)
                        ? value.GetString() ?? ""
                        : "");
            }
            catch (System.Text.Json.JsonException)
            {
                return KpiScriptCheck.Ran("");
            }
        }
    }

    /// <summary>
    /// Alles, was das Dashboard braucht, in EINEM Durchlauf.
    ///
    /// Die Schleife selbst liegt in KpiDashboard und wird mit KPIService
    /// geteilt. Hier bleibt genau das eine, was sich zwischen Server und
    /// Browser wirklich unterscheidet: woher der Wert eines handgeschriebenen
    /// Skripts kommt - dort ein SqlQueryRaw auf einem geteilten Context, hier
    /// ein HTTP-Aufruf.
    ///
    /// /api/kpi/dashboard wird bewusst NICHT benutzt. Es gaebe dieselben
    /// Kacheln zurueck, kostete aber pro Builder-Kennzahl einen Netzaufruf -
    /// und wirfe damit genau die Eigenschaft weg, fuer die diese Klasse gebaut
    /// wurde: eine deklarative Kennzahl kostet nichts, weil sie aus Caches
    /// gerechnet wird, die ohnehin im Speicher liegen. Nur handgeschriebenes
    /// SQL muss ueber die Leitung.
    /// </summary>
    public async Task<IReadOnlyList<KpiTileModel>> GetDashboardAsync(bool addButtonKPI = true)
    {
        await GetAllDataAsync();

        return await KpiDashboard.BuildAsync(
            KPIs.Values,
            _rowProvider.Rows,
            // Als Lambda und nicht als Methodengruppe: GetKPIValue hat einen
            // optionalen zweiten Parameter, und den fuellt eine
            // Methodengruppen-Konvertierung nicht auf.
            kpi => GetKPIValue(kpi),
            DateTime.Now,
            (kpi, message) => Logger.LogError(
                "KPI '{Title}' could not be evaluated: {Message}", kpi.Title, message),
            addButtonKPI);
    }

    public async Task<bool> UpdateDataAsync(KPI KPI)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/kpis/{KPI.KPIId}", KPI),
            $"Failed to update KPI {KPI.KPIId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Updated KPI {Title}.", KPI.Title);
        }

        return isSuccess;
    }

    public async Task<bool> DeleteDataAsync(int kpiId)
    {
        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/kpis/{kpiId}"),
            $"Failed to delete KPI {kpiId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Deleted KPI with ID {KPIId}.", kpiId);
        }

        return isSuccess;
    }
}
