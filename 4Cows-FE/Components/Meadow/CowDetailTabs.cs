namespace _4Cows_FE.Components.Meadow;

/// <summary>Die fuenf Laschen der Kuh-Seite, in Anzeigereihenfolge.</summary>
public enum CowDetailTab
{
    CowTreatments,
    ClawTreatments,
    PlannedCow,
    PlannedClaw,
    Bandages
}

/// <summary>
/// Beschriftung und Adress-Kuerzel der Laschen.
///
/// Die Auswahl lebt im Query-String (?tab=), nicht in einem Feld: nur so
/// koennen die Kacheln "offene Verbaende" und "offene geplante Termine"
/// ECHTE Links sein - MeadowKpiTile rendert ein &lt;a href&gt;, und ein
/// href auf ein Fragment, das nichts anspringt, waere eine Luege. Nebenbei
/// funktionieren damit Zurueck-Knopf und geteilte Adressen.
/// </summary>
public static class CowDetailTabs
{
    public static readonly CowDetailTab[] All =
    {
        CowDetailTab.CowTreatments,
        CowDetailTab.ClawTreatments,
        CowDetailTab.PlannedCow,
        CowDetailTab.PlannedClaw,
        CowDetailTab.Bandages
    };

    public static string Label(CowDetailTab tab) => tab switch
    {
        CowDetailTab.CowTreatments => "Kuh",
        CowDetailTab.ClawTreatments => "Klauen",
        CowDetailTab.PlannedCow => "Gepl. Kuh",
        CowDetailTab.PlannedClaw => "Gepl. Klauen",
        _ => "Verbände"
    };

    /// <summary>Ohne Umlaute und Grossbuchstaben - das steht in der Adresszeile.</summary>
    public static string ToSlug(CowDetailTab tab) => tab switch
    {
        CowDetailTab.ClawTreatments => "klauen",
        CowDetailTab.PlannedCow => "geplant-kuh",
        CowDetailTab.PlannedClaw => "geplant-klauen",
        CowDetailTab.Bandages => "verbaende",
        _ => "kuh"
    };

    /// <summary>Unbekanntes oder fehlendes Kuerzel faellt auf die erste Lasche zurueck.</summary>
    public static CowDetailTab Parse(string? slug) => slug switch
    {
        "klauen" => CowDetailTab.ClawTreatments,
        "geplant-kuh" => CowDetailTab.PlannedCow,
        "geplant-klauen" => CowDetailTab.PlannedClaw,
        "verbaende" => CowDetailTab.Bandages,
        _ => CowDetailTab.CowTreatments
    };
}
