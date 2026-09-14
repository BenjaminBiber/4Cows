using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Die reinen Leseregeln ueber den Einstellungs-Cache. Rumpf hier,
/// delegierende Zeile beziehungsweise Eigenschaft in SettingsService.
///
/// Die beiden abgeleiteten Werte stehen mit hier, obwohl sie am Dienst
/// Eigenschaften sind: den Rueckfallwert traegt jeweils genau ein Literal, und
/// zwei Kopien von "Pflege" oder "ml" waeren die Drift, die dieses Projekt
/// verhindern soll.
/// </summary>
public static class SettingsLookups
{
    public static string Get(IReadOnlyDictionary<string, string> settings, string key, string fallback = "")
        => settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    /// <summary>Anzeigetext fuer Klauen ohne erfassten Befund.</summary>
    public static string ClawFindingFallback(IReadOnlyDictionary<string, string> settings)
        => Get(settings, AppSetting.ClawFindingFallbackKey, "Pflege");

    /// <summary>
    /// Einheit fuer Mengen, deren Medikament keine Dosiereinheit hinterlegt
    /// hat. War vorher eine Konstante in Cow_Table.
    /// </summary>
    public static string DefaultDosageUnit(IReadOnlyDictionary<string, string> settings)
        => Get(settings, AppSetting.DefaultDosageUnitKey, "ml");
}
