using BB_Cow.Class;

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

/// <summary>
/// Der zweite Teil des Query-Vokabulars der Kuh-Seite: ?klaue= filtert die
/// Lasche "Klauen" auf eine Position.
///
/// Steht neben CowDetailTabs aus demselben Grund, aus dem die Lasche dort
/// steht - der Zustand gehoert in die Adresse. Nur so kann das 2x2 der
/// Klauen aus ECHTEN Links bestehen: Zurueck-Knopf, Mittelklick und ein
/// geteilter Link funktionieren damit von selbst.
///
/// Das Kuerzel ist der Enum-Name in Kleinbuchstaben ("lv"). Eine eigene
/// Zuordnungstabelle waere hier nur eine zweite Stelle, an der LV, RV, LH
/// und RH gepflegt werden muessten; Umlaute oder Leerzeichen, wegen denen
/// die Laschen eigene Kuerzel tragen, gibt es bei den vier Codes nicht.
/// </summary>
public static class CowDetailClaw
{
    public static string ToSlug(HoofPosition position)
        => position.ToString().ToLowerInvariant();

    /// <summary>
    /// Unbekanntes oder fehlendes Kuerzel heisst "kein Filter" - eine
    /// verunglueckte Adresse zeigt die ganze Liste und nicht eine leere.
    /// IsDefined zusaetzlich zu TryParse: das nimmt auch Zahlen an, und "?
    /// klaue=99" ergaebe sonst eine HoofPosition, die es nicht gibt.
    /// </summary>
    public static HoofPosition? Parse(string? slug)
        => Enum.TryParse<HoofPosition>(slug, ignoreCase: true, out var position)
           && Enum.IsDefined(position)
            ? position
            : null;

    /// <summary>Adresse der Lasche "Klauen", gefiltert auf eine Position.</summary>
    public static string HrefFor(string cowId, HoofPosition position)
        => $"{MeadowRoutes.CowDetailFor(cowId)}"
           + $"?tab={CowDetailTabs.ToSlug(CowDetailTab.ClawTreatments)}"
           + $"&klaue={ToSlug(position)}";
}
