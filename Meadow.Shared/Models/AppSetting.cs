using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class;

/// <summary>
/// Pflegbare Standardwerte, bearbeitbar unter Einstellungen -> Standardwerte.
///
/// Erster Eintrag ist der Anzeige-Fallback fuer Klauen ohne Befund: bisher
/// schrieb der Hinzufuegen-Dialog still "Pflege" in JEDE unberuehrte Klaue,
/// wodurch jede Behandlung wie eine Rundum-Klauenpflege aussah. Jetzt bleibt
/// das Feld leer und dieser Text wird nur angezeigt.
/// </summary>
[Table("AppSetting")]
public class AppSetting
{
    public const string ClawFindingFallbackKey = "ClawFindingFallback";

    /// <summary>
    /// Anzeigeeinheit fuer Mengen ohne hinterlegte Dosiereinheit am Medikament.
    /// Ersetzt die fest verdrahtete Konstante in Cow_Table.
    /// </summary>
    public const string DefaultDosageUnitKey = "DefaultDosageUnit";

    [Key]
    [Required]
    [StringLength(64)]
    [Column("SettingKey")]
    public string SettingKey { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [Column("SettingValue")]
    public string SettingValue { get; set; } = string.Empty;

    public AppSetting()
    {
    }

    public AppSetting(string key, string value)
    {
        SettingKey = key;
        SettingValue = value;
    }
}
