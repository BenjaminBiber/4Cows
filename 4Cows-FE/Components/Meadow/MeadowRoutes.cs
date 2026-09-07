namespace _4Cows_FE.Components.Meadow;

public enum NavGroup
{
    None,
    Dashboard,
    Cow,
    Claw,
    System
}

/// <summary>
/// Route -> Titel und Route -> Navigationsgruppe.
///
/// Ersetzt MainLayout.GetSiteName(), das vier absolute Hosts aus der Uri
/// herausersetzte und deshalb auf jedem anderen Host (z.B. dem
/// http-Launchprofil oder dem Docker-Mapping) einen leeren Titel lieferte.
/// Aufgeloest wird stattdessen ueber NavigationManager.ToBaseRelativePath.
/// </summary>
public static class MeadowRoutes
{
    public const string Dashboard = "";
    public const string CowTreatments = "Kuh_Daten";
    public const string PlannedCowTreatments = "geplante_Kuh_Daten";
    public const string Bandages = "Verband_Daten";
    public const string ClawTreatments = "Klauen_Daten";
    public const string PlannedClawTreatments = "geplante_Klauen_Daten";
    public const string Settings = "Settings";
    public const string NotFound = "nicht-gefunden";

    private static readonly Dictionary<string, string> TitleMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Dashboard] = "Dashboard",
            [CowTreatments] = "Kuh Behandlungen",
            [PlannedCowTreatments] = "Geplante Kuh Behandl.",
            [Bandages] = "Verbände",
            [ClawTreatments] = "Klauen Behandlungen",
            [PlannedClawTreatments] = "Geplante Klauen Behandl.",
            [Settings] = "Einstellungen",
            [NotFound] = "Seite nicht gefunden"
        };

    /// <summary>Route ohne Basis, ohne Query und ohne Fragment.</summary>
    public static string Normalize(string baseRelativePath)
        => baseRelativePath.Split('?', '#')[0].Trim('/');

    /// <summary>
    /// Alle sieben echten Routen stehen in der Tabelle - eine unbekannte
    /// Route ist deshalb immer ein 404. Der Fallback lautet nicht "Meadow":
    /// UseStatusCodePagesWithReExecute schreibt nur den Server-Pfad um, die
    /// Adresse im Browser bleibt die falsche. Header und Dokumenttitel
    /// leiten sich also weiter aus ihr ab und sollen dann "Seite nicht
    /// gefunden" sagen, nicht den Produktnamen doppeln ("Meadow · Meadow").
    /// </summary>
    public static string TitleFor(string route)
        => TitleMap.TryGetValue(route, out var title) ? title : TitleMap[NotFound];

    /// <summary>
    /// Gruppe fuer Drawer-Akzent, Tabbar-Hervorhebung und FAB-Aktion.
    /// Verbände zaehlt hier zur Klaue-Gruppe.
    /// </summary>
    public static NavGroup GroupFor(string route) => route switch
    {
        Dashboard => NavGroup.Dashboard,
        CowTreatments or PlannedCowTreatments => NavGroup.Cow,
        ClawTreatments or PlannedClawTreatments or Bandages => NavGroup.Claw,
        Settings => NavGroup.System,
        _ => NavGroup.None
    };

    /// <summary>
    /// Ob der Seitentitel ein Dropdown bekommt. Bewusst ein anderes Praedikat
    /// als GroupFor: das Menue zeigt nur die beiden Zweier-Gruppen, Verbände
    /// hat dort keine Geschwister.
    /// </summary>
    public static bool HasSiblings(string route) => route
        is CowTreatments or PlannedCowTreatments
        or ClawTreatments or PlannedClawTreatments;

    /// <summary>Die Geschwister-Routen fuer das Seitentitel-Dropdown.</summary>
    public static IReadOnlyList<string> SiblingsFor(string route) => route switch
    {
        CowTreatments or PlannedCowTreatments =>
            new[] { CowTreatments, PlannedCowTreatments },
        ClawTreatments or PlannedClawTreatments =>
            new[] { ClawTreatments, PlannedClawTreatments },
        _ => Array.Empty<string>()
    };
}
