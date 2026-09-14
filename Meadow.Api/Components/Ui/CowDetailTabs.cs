using Meadow.Shared.Models;

namespace Meadow.Api.Components.Ui;

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
/// Lasche "Klauen" auf eine Klaue, ?viertel= die Lasche "Kuh" auf ein
/// Euterviertel. Beide Orte sind eine HoofPosition - vier Ecken am Tier, und
/// dieselben vier Kuerzel.
///
/// Steht neben CowDetailTabs aus demselben Grund, aus dem die Lasche dort
/// steht - der Zustand gehoert in die Adresse. Nur so koennen die beiden 2x2
/// aus ECHTEN Links bestehen: Zurueck-Knopf, Mittelklick und ein geteilter
/// Link funktionieren damit von selbst.
///
/// Das Kuerzel ist der Enum-Name in Kleinbuchstaben ("lv"). Eine eigene
/// Zuordnungstabelle waere hier nur eine zweite Stelle, an der LV, RV, LH
/// und RH gepflegt werden muessten; Umlaute oder Leerzeichen, wegen denen
/// die Laschen eigene Kuerzel tragen, gibt es bei den vier Codes nicht.
/// </summary>
public static class CowDetailPosition
{
    public static string ToSlug(HoofPosition position)
        => position.ToString().ToLowerInvariant();

    /// <summary>
    /// Unbekanntes oder fehlendes Kuerzel heisst "kein Filter" - eine
    /// verunglueckte Adresse zeigt die ganze Liste und nicht eine leere.
    /// IsDefined zusaetzlich zu TryParse: das nimmt auch Zahlen an, und
    /// "?klaue=99" ergaebe sonst eine HoofPosition, die es nicht gibt.
    /// </summary>
    public static HoofPosition? Parse(string? slug)
        => Enum.TryParse<HoofPosition>(slug, ignoreCase: true, out var position)
           && Enum.IsDefined(position)
            ? position
            : null;

    /// <summary>Adresse der Lasche "Klauen", gefiltert auf eine Klaue.</summary>
    public static string ClawHref(string cowId, HoofPosition position)
        => $"{MeadowRoutes.CowDetailFor(cowId)}"
           + $"?tab={CowDetailTabs.ToSlug(CowDetailTab.ClawTreatments)}"
           + $"&klaue={ToSlug(position)}";

    /// <summary>Adresse der Lasche "Kuh", gefiltert auf ein Euterviertel.</summary>
    public static string QuarterHref(string cowId, HoofPosition position)
        => $"{MeadowRoutes.CowDetailFor(cowId)}"
           + $"?tab={CowDetailTabs.ToSlug(CowDetailTab.CowTreatments)}"
           + $"&viertel={ToSlug(position)}";

    /// <summary>
    /// Die Viertel ausgeschrieben. Bewusst NICHT HoofPositions.SideLabel,
    /// obwohl die Woerter heute uebereinstimmen: das ist Klauen-Vokabular,
    /// und ein spaeterer Umbau dort soll nicht still die Eutergrafik
    /// umbenennen.
    /// </summary>
    public static string QuarterLabel(HoofPosition position) => position switch
    {
        HoofPosition.LV => "Vorne links",
        HoofPosition.RV => "Vorne rechts",
        HoofPosition.LH => "Hinten links",
        _ => "Hinten rechts"
    };
}
