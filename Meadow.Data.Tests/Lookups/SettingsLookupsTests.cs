using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Die Leseregeln der pflegbaren Standardwerte.
///
/// Die beiden Rueckfalltexte "Pflege" und "ml" stehen im Projekt jeweils genau
/// einmal, naemlich hier. Sie sind das, was im Stall zu lesen ist, solange
/// niemand etwas anderes eingetragen hat - eine zweite Kopie an einer
/// Tabellenzelle wuerde still von dieser hier abweichen.
/// </summary>
public class SettingsLookupsTests
{
    private static Dictionary<string, string> Settings(params (string Key, string Value)[] entries)
        => entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);

    [Fact]
    public void A_key_that_was_never_stored_falls_back()
    {
        // Der Zustand einer frischen Installation: AppSetting startet leer.
        Assert.Equal("ersatz", SettingsLookups.Get(Settings(), "beliebig", "ersatz"));
    }

    [Fact]
    public void A_setting_left_blank_falls_back_instead_of_blanking_the_display()
    {
        // Ein leergeraeumtes Feld auf der Einstellungsseite darf die Anzeige
        // nicht leer machen.
        Assert.Equal("ersatz", SettingsLookups.Get(Settings(("beliebig", "   ")), "beliebig", "ersatz"));
    }

    [Fact]
    public void A_stored_value_wins()
    {
        Assert.Equal("eigen", SettingsLookups.Get(Settings(("beliebig", "eigen")), "beliebig", "ersatz"));
    }

    [Fact]
    public void Without_a_fallback_a_missing_setting_reads_empty()
    {
        Assert.Equal(string.Empty, SettingsLookups.Get(Settings(), "beliebig"));
    }

    [Fact]
    public void Claws_without_a_finding_read_Pflege_until_someone_changes_it()
    {
        // Vorher schrieb der Dialog dieses Wort still in JEDE unberuehrte
        // Klaue, wodurch jede Behandlung wie eine Rundum-Pflege aussah. Jetzt
        // bleibt das Feld leer und der Text wird nur angezeigt.
        Assert.Equal("Pflege", SettingsLookups.ClawFindingFallback(Settings()));
    }

    [Fact]
    public void The_stored_claw_finding_fallback_wins()
    {
        var settings = Settings((AppSetting.ClawFindingFallbackKey, "Kontrolle"));

        Assert.Equal("Kontrolle", SettingsLookups.ClawFindingFallback(settings));
    }

    [Fact]
    public void Quantities_without_a_unit_read_ml_until_someone_changes_it()
    {
        // War vorher eine fest verdrahtete Konstante in der Kuhtabelle.
        Assert.Equal("ml", SettingsLookups.DefaultDosageUnit(Settings()));
    }

    [Fact]
    public void The_stored_dosage_unit_wins()
    {
        var settings = Settings((AppSetting.DefaultDosageUnitKey, "Stueck"));

        Assert.Equal("Stueck", SettingsLookups.DefaultDosageUnit(settings));
    }

    [Fact]
    public void Bandage_reminder_days_fall_back_to_14_when_key_is_missing()
    {
        // Frische Installation: noch kein Wert hinterlegt.
        Assert.Equal(14, SettingsLookups.BandageRemovalReminderDays(Settings()));
    }

    [Fact]
    public void Bandage_reminder_days_fall_back_to_14_when_value_is_empty()
    {
        // Leer gespeicherter Wert darf die Erinnerung nicht abschalten.
        Assert.Equal(14, SettingsLookups.BandageRemovalReminderDays(
            Settings((AppSetting.BandageRemovalReminderDaysKey, "   "))));
    }

    [Fact]
    public void Bandage_reminder_days_fall_back_to_14_when_value_is_not_a_number()
    {
        // Beschaedigter oder von Hand gesetzter Wert - kein Crash, sicherer Fallback.
        Assert.Equal(14, SettingsLookups.BandageRemovalReminderDays(
            Settings((AppSetting.BandageRemovalReminderDaysKey, "abc"))));
    }

    [Fact]
    public void Bandage_reminder_days_fall_back_to_14_when_value_is_zero_or_negative()
    {
        // 0 oder negativ waere unsinnig - sicherer Fallback.
        Assert.Equal(14, SettingsLookups.BandageRemovalReminderDays(
            Settings((AppSetting.BandageRemovalReminderDaysKey, "0"))));
        Assert.Equal(14, SettingsLookups.BandageRemovalReminderDays(
            Settings((AppSetting.BandageRemovalReminderDaysKey, "-5"))));
    }

    [Fact]
    public void The_stored_bandage_reminder_days_win()
    {
        var settings = Settings((AppSetting.BandageRemovalReminderDaysKey, "21"));

        Assert.Equal(21, SettingsLookups.BandageRemovalReminderDays(settings));
    }
}
