using System.Collections.Immutable;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die pflegbaren Standardwerte.
/// </summary>
public interface ISettingsService
{
    ImmutableDictionary<string, string> Settings { get; }

    Task GetAllDataAsync();

    string Get(string key, string fallback = "");

    /// <summary>Anzeigetext fuer Klauen ohne erfassten Befund.</summary>
    string ClawFindingFallback { get; }

    /// <summary>
    /// Einheit fuer Mengen, deren Medikament keine Dosiereinheit hinterlegt
    /// hat.
    /// </summary>
    string DefaultDosageUnit { get; }

    /// <summary>
    /// Tage ab dem Behandlungsdatum, nach denen an das Entfernen eines Verbands
    /// erinnert werden soll. Rückfallwert 14.
    /// </summary>
    int BandageRemovalReminderDays { get; }

    Task<bool> SetAsync(string key, string value);
}
