using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die pflegbaren Standardwerte ueber HTTP. Gegenstueck zum EF-Dienst
/// SettingsService.
/// </summary>
public class HttpSettingsService : HttpServiceBase, ISettingsService
{
    private ImmutableDictionary<string, string> _cached = ImmutableDictionary<string, string>.Empty;

    public ImmutableDictionary<string, string> Settings => _cached;

    public HttpSettingsService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpSettingsService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    /// <summary>
    /// Holt alle Standardwerte.
    ///
    /// Der Endpunkt liefert ein OBJEKT und keine Liste - der Cache IST ein
    /// Woerterbuch, und ein Client, der einen Standardwert nachschlaegt, soll
    /// dafuer keinen Sucher ueber ein Array schreiben muessen. Deshalb steht
    /// hier GetJsonAsync und nicht GetListAsync.
    /// </summary>
    public async Task GetAllDataAsync()
    {
        var settings = await GetJsonAsync<Dictionary<string, string>>("api/settings", "Failed to load settings.");
        if (settings is null)
        {
            return;
        }

        _cached = settings.ToImmutableDictionary();
        Logger.LogInformation("Loaded {Count} settings.", _cached.Count);
    }

    // Weiterleitungen; die Rumpfe stehen in SettingsLookups. Auch die beiden
    // abgeleiteten Werte: ihr Rueckfallwert ist je ein Literal, und zwei Kopien
    // von "Pflege" oder "ml" waeren genau die Drift, die die Naht verhindern
    // soll.
    public string Get(string key, string fallback = "")
        => SettingsLookups.Get(_cached, key, fallback);

    /// <summary>Anzeigetext fuer Klauen ohne erfassten Befund.</summary>
    public string ClawFindingFallback
        => SettingsLookups.ClawFindingFallback(_cached);

    /// <summary>
    /// Einheit fuer Mengen, deren Medikament keine Dosiereinheit hinterlegt
    /// hat.
    /// </summary>
    public string DefaultDosageUnit
        => SettingsLookups.DefaultDosageUnit(_cached);

    /// <summary>
    /// Tage ab dem Behandlungsdatum, nach denen an das Entfernen eines Verbands
    /// erinnert werden soll.
    /// </summary>
    public int BandageRemovalReminderDays
        => SettingsLookups.BandageRemovalReminderDays(_cached);

    /// <summary>
    /// Setzt einen Standardwert. Ein unbekannter Schluessel wird angelegt, denn
    /// die Tabelle hat keine feste Schluesselliste - deshalb gibt es nur diesen
    /// einen Weg und kein getrenntes Anlegen.
    /// </summary>
    public async Task<bool> SetAsync(string key, string value)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/settings/{Segment(key)}", new SettingValueUpdate(value)),
            $"Failed to update setting {key}.");

        if (isSuccess)
        {
            // SetItem statt Neuladen - gespiegelt aus der EF-Fassung: der
            // geschriebene Wert ist bekannt, ein zweiter Volldurchlauf brachte
            // nichts dazu.
            _cached = _cached.SetItem(key, value);
            Logger.LogInformation("Updated setting {Key}.", key);
        }

        return isSuccess;
    }
}
