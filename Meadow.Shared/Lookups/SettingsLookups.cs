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

    /// <summary>
    /// Tage ab dem Behandlungsdatum, nach denen an das Entfernen eines Verbands
    /// erinnert werden soll. Rueckfall 14, auch bei fehlendem, leerem oder
    /// unparsbarem Wert - der Dienst soll nie mit 0 oder negativ zurückkehren.
    /// </summary>
    public static int BandageRemovalReminderDays(IReadOnlyDictionary<string, string> settings)
    {
        // Get gibt "" zurueck wenn der Schluessel fehlt oder leer ist —
        // TryParse schlaegt dann fehl, und der Fallback 14 greift.
        var raw = Get(settings, AppSetting.BandageRemovalReminderDaysKey);
        return int.TryParse(raw, out var days) && days > 0 ? days : 14;
    }
}
