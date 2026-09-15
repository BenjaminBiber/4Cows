using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die Euterviertel ueber HTTP. Gegenstueck zum EF-Dienst
/// UdderService.
/// </summary>
public class HttpUdderService : HttpServiceBase, IUdderService
{
    private ImmutableDictionary<int, Udder> _cachedUdder = ImmutableDictionary<int, Udder>.Empty;

    public ImmutableDictionary<int, Udder> Udder => _cachedUdder;

    public HttpUdderService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpUdderService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    public async Task GetAllDataAsync()
    {
        var udders = await GetListAsync<Udder>("api/udders", "Failed to load udder entries.");
        if (udders is null)
        {
            return;
        }

        _cachedUdder = udders.ToImmutableDictionary(u => u.UdderId);
        Logger.LogInformation("Loaded {Count} Udder.", _cachedUdder.Count);
    }

    /// <summary>
    /// Legt eine Viertelkombination an.
    ///
    /// Die Sentinel-Normalisierung steht hier NICHT noch einmal: der Dienst
    /// hinter /api/udders setzt eine UdderId von int.MinValue auf 0, damit die
    /// Datenbank die Identity vergibt, und eine zweite Stelle mit derselben
    /// Regel waere die naechste, die jemand vergisst. Was eine Zeile mit
    /// UDDER_ID = -2147483648 anrichtet, steht ausfuehrlich im Kommentar in
    /// UdderService.InsertDataAsync.
    ///
    /// MeadowJson setzt DefaultIgnoreCondition auf Never, der Sentinel steht
    /// also wirklich auf der Leitung und kommt dort an, wo er abgefangen wird.
    /// Die erzeugte Id kommt mit der Antwort zurueck und wird in die
    /// UEBERGEBENE Instanz geschrieben - serverseitig tut EF genau das.
    /// </summary>
    public async Task<bool> InsertDataAsync(Udder udder)
    {
        var created = await CreateAsync("api/udders", udder, "Failed to insert udder.");
        if (created is null)
        {
            return false;
        }

        udder.UdderId = created.UdderId;

        // Neu laden - gespiegelt aus der EF-Fassung.
        await GetAllDataAsync();
        Logger.LogInformation("Inserted Udder {UdderId}.", udder.UdderId);
        return true;
    }

    /// <summary>
    /// Sucht die Zeile zu den vier Viertelflaggen und legt sie an, wenn es
    /// keine gibt.
    ///
    /// Der Vergleich passiert im Dienst hinter dem Endpunkt und nicht hier.
    /// Ein zweiter Anlagepfad im Browser waere genau das, wogegen der Semaphor
    /// in UdderService steht - und dessen Reichweite endet ohnehin am
    /// Serverprozess, nicht an diesem Browser.
    ///
    /// int.MinValue ist der Fehlwert, den auch die EF-Fassung liefert: sie
    /// faellt dort auf "(... ?? new Udder()).UdderId" durch, und der
    /// parameterlose Konstruktor setzt genau diesen Sentinel.
    /// </summary>
    public async Task<int> GetIDByBools(Udder emptyUdder)
    {
        // Erst im eigenen Cache nachsehen, genau mit dem Vergleich aus der
        // EF-Fassung. Zwei Gruende, beide gemessen:
        //
        // - Ohne Netz kaeme der Endpunkt nie zu Wort, GetIDByBools gaebe
        //   int.MinValue zurueck, und die Behandlung landete mit dem Sentinel
        //   in COW_QUARTER_ID - ein Wert, den keine der 121 bestehenden
        //   Zeilen hat. Die Viertelanzeige liest ihn dann als "keine
        //   Viertel", obwohl die Zeile fuer "keine Viertel" (Id 1) existiert.
        // - Online spart es den Aufruf fuer die fuenfzehn Kombinationen, die
        //   der Cache ohnehin schon kennt.
        //
        // Ein Treffer hier ist keine zweite Wahrheit: der Endpunkt vergleicht
        // dieselben vier Flaggen. Angelegt wird weiterhin nur dort.
        var known = _cachedUdder.Values.FirstOrDefault(x =>
            x.QuarterLV == emptyUdder.QuarterLV && x.QuarterRV == emptyUdder.QuarterRV &&
            x.QuarterLH == emptyUdder.QuarterLH && x.QuarterRH == emptyUdder.QuarterRH);
        if (known is not null)
        {
            return known.UdderId;
        }

        // Eine Kombination, die der Cache nicht kennt - ab hier wuerde sie
        // angelegt. Ohne Verbindung nicht; siehe
        // HttpMedicineService.GetMedicineIdByName, es ist dieselbe Regel.
        //
        // Die haeufigen Faelle kosten das nichts: "keine Viertel" und die
        // bereits benutzten Kombinationen stehen oben im Cache. Getroffen wird
        // nur, wer ohne Netz eine bisher nie verwendete Viertel-Kombination
        // waehlt - und der bekommt die Ablehnung aus ResolveAsync.
        if (!IsConnected)
        {
            Logger.LogInformation("Unbekannte Viertel-Kombination, ohne Verbindung nicht anzulegen.");
            return int.MinValue;
        }

        var response = await ReadAsync<IdResponse>(
            () => PostAsync(
                "api/udders/by-quarters",
                new UdderByQuartersRequest(
                    emptyUdder.QuarterLV,
                    emptyUdder.QuarterLH,
                    emptyUdder.QuarterRV,
                    emptyUdder.QuarterRH)),
            "Failed to resolve udder quarters.");

        if (response?.Id is not int id)
        {
            return int.MinValue;
        }

        // Den eigenen Cache nachziehen: der Endpunkt kann die Kombination
        // gerade erst angelegt haben, und ohne dieses Nachladen lieferte
        // GetById fuer die soeben erzeugte Id ein leeres Udder - also "keine
        // Viertel" fuer eine Behandlung, in der welche gewaehlt wurden.
        await GetAllDataAsync();
        return id;
    }

    /// <summary>
    /// Die Id der Kombination ohne Viertel, angelegt falls noetig.
    ///
    /// Geht ueber DENSELBEN Endpunkt wie <see cref="GetIDByBools"/>, mit vier
    /// false. "Alle vier false" sieht nach einem Sonderfall aus, ist aber
    /// keiner: es ist eine der sechzehn Kombinationen wie jede andere. Ein
    /// eigener Weg dafuer waere ein ZWEITER Anlagepfad fuer dieselbe Zeile.
    /// Die Methode bleibt auf der Naht, weil die Oberflaeche an ihren
    /// Aufrufstellen kein Udder-Objekt in der Hand hat.
    /// </summary>
    public Task<int> GetIdForNoQuarters()
        => GetIDByBools(new Udder(0, false, false, false, false));

    // Weiterleitungen; die Rumpfe stehen in UdderLookups, damit es die Regel
    // "unbekannte ID heisst keine Viertel" genau einmal gibt.
    public bool HasAnyQuarter(int id) => UdderLookups.HasAnyQuarter(_cachedUdder, id);

    public Udder GetById(int id) => UdderLookups.GetById(_cachedUdder, id);
}
