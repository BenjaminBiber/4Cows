namespace Meadow.Shared.Models;

/// <summary>
/// Art eines Behandlungsplans am Medikament. Als int gespeichert (Spalte
/// Plan_Mode) - die Werte sind deshalb Vertrag und duerfen nicht umnummeriert
/// werden.
/// </summary>
public enum TreatmentPlanMode
{
    /// <summary>"N Gaben innerhalb von D Tagen" - Starttag zaehlt als Tag 1.</summary>
    Window = 0,

    /// <summary>"N Gaben alle D Tage" - naechste Gabe = letzte tatsaechliche Gabe + D.</summary>
    Interval = 1
}
