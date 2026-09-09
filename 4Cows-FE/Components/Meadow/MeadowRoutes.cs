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
    /// <summary>
    /// Die Startadresse. Ohne Demo zeigt sie das Dashboard, mit Demo die
    /// Landing-Page - entschieden wird das im Dispatcher Pages/Home.razor,
    /// nicht hier.
    /// </summary>
    public const string Root = "";

    /// <summary>
    /// Das Dashboard unter /app. Existiert in beiden Modi; im Demo-Modus ist
    /// es die einzige Dashboard-Adresse, im Betriebsmodus ein Alias auf das,
    /// was auch unter Root liegt. Wer den Wert aendert, muss das @page in
    /// Components/Pages/Index.razor mitziehen.
    /// </summary>
    public const string Dashboard = "app";

    /// <summary>
    /// Absoluter Pfad zum Dashboard - fuer Kontexte OHNE das
    /// &lt;base href="/"&gt; aus App.razor, also Error.cshtml.
    /// </summary>
    public const string DashboardPath = "/" + Dashboard;

    public const string CowTreatments = "Kuh_Daten";
    public const string PlannedCowTreatments = "geplante_Kuh_Daten";
    public const string Bandages = "Verband_Daten";
    public const string ClawTreatments = "Klauen_Daten";
    public const string PlannedClawTreatments = "geplante_Klauen_Daten";
    public const string Settings = "Settings";
    public const string NotFound = "nicht-gefunden";

    /// <summary>
    /// Die Kuh-Uebersicht. Steht auch in KpiSourceRegistry.Cows().Route - dort
    /// als Literal, weil die Bibliothek diese Klasse nicht kennen kann.
    /// </summary>
    public const string Cows = "Kuehe";

    /// <summary>
    /// Erstes Segment der Kuh-Seite; danach folgt die Cow_ID. Eigene
    /// Konstante, weil TitleFor, GroupFor und IsWide ueber BaseOf nur dieses
    /// Segment zu sehen bekommen.
    /// </summary>
    public const string CowDetail = "Kuh";

    /// <summary>
    /// Adresse einer Kuh. EscapeDataString ist Pflicht und keine Vorsicht:
    /// fuer erfasste Tiere IST die Cow_ID die Ohrmarke, und die enthaelt
    /// Leerzeichen ("DE 08 1523 4567").
    /// </summary>
    public static string CowDetailFor(string cowId)
        => $"{CowDetail}/{Uri.EscapeDataString(cowId)}";

    private static readonly Dictionary<string, string> TitleMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Beide Dashboard-Adressen. Root wird nur im Betriebsmodus
            // gelesen: im Demo-Modus rendert "/" die Landing-Page im
            // LandingLayout, das TitleFor gar nicht erst aufruft. Fehlt der
            // Eintrag trotzdem, liefert TitleFor unten den 404-Titel.
            [Root] = "Dashboard",
            [Dashboard] = "Dashboard",
            [Cows] = "Kühe",
            // Ohne die Cow_ID - die steht in der Route, nicht in der Tabelle.
            // Den Titel mit Halsbandnummer setzt die Seite selbst ueber
            // LayoutState.SetPageTitle; das hier ist der Wert fuer das eine
            // Bild davor und fuer den Fall, dass die Kuh es nicht gibt.
            [CowDetail] = "Kuh",
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
    /// Nur das erste Segment: "Kuh/DE%2008%201523%204567" wird zu "Kuh".
    ///
    /// Die Kuh-Seite ist die einzige Route mit Parameter. Ohne diesen Schritt
    /// faende TitleFor sie nicht in der Tabelle (also 404-Titel) und GroupFor
    /// gaebe NavGroup.None zurueck - Drawer, Tabbar und Titel-Dropdown
    /// verloeren auf ihr die Orientierung. Und der Parameter kommt escaped
    /// an: ToBaseRelativePath liefert "Kuh/DE%2008...", nicht "Kuh/DE 08...".
    /// </summary>
    public static string BaseOf(string route)
    {
        var slash = route.IndexOf('/');
        return slash < 0 ? route : route[..slash];
    }

    /// <summary>
    /// Alle echten Routen stehen in der Tabelle - Root und Dashboard zeigen
    /// dabei auf dieselbe Seite, und die Kuh-Seite steht ueber BaseOf mit
    /// ihrem ersten Segment darin. Eine unbekannte
    /// Route ist deshalb immer ein 404. Der Fallback lautet nicht "Meadow":
    /// UseStatusCodePagesWithReExecute schreibt nur den Server-Pfad um, die
    /// Adresse im Browser bleibt die falsche. Header und Dokumenttitel
    /// leiten sich also weiter aus ihr ab und sollen dann "Seite nicht
    /// gefunden" sagen, nicht den Produktnamen doppeln ("Meadow · Meadow").
    /// </summary>
    public static string TitleFor(string route)
        => TitleMap.TryGetValue(BaseOf(route), out var title) ? title : TitleMap[NotFound];

    /// <summary>
    /// Gruppe fuer Drawer-Akzent, Tabbar-Hervorhebung, FAB-Aktion und das
    /// Titel-Dropdown. Verbände zaehlt zur Klaue-Gruppe.
    /// </summary>
    public static NavGroup GroupFor(string route) => BaseOf(route) switch
    {
        Root or Dashboard => NavGroup.Dashboard,
        Cows or CowDetail or CowTreatments or PlannedCowTreatments => NavGroup.Cow,
        ClawTreatments or PlannedClawTreatments or Bandages => NavGroup.Claw,
        Settings => NavGroup.System,
        _ => NavGroup.None
    };

    /// <summary>
    /// Ob die Seite die volle Inhaltsbreite bekommt statt des 1260px-Deckels
    /// aus --mw-shell-max. Dashboard und Kuh-Seite: deren Kacheln und
    /// Diagramme fuellen die Breite, waehrend die Tabellen mit vier Spalten
    /// davon nichts haetten ausser laengeren Zeilen. Die Kuh-UEBERSICHT ist
    /// deshalb bewusst nicht dabei - die ist wieder eine Tabelle.
    /// </summary>
    public static bool IsWide(string route) => BaseOf(route) is Root or Dashboard or CowDetail;

    // Reihenfolge wie im Drawer, damit man dieselbe Liste nicht in zwei
    // Anordnungen lernen muss.
    private static readonly string[] CowGroup =
        { Cows, CowTreatments, PlannedCowTreatments };

    private static readonly string[] ClawGroup =
        { Bandages, ClawTreatments, PlannedClawTreatments };

    /// <summary>
    /// Ob der Seitentitel ein Dropdown bekommt (nur mobil - die Kopfzeile
    /// zeigt den Titel am Desktop als reinen Text).
    /// </summary>
    public static bool HasSiblings(string route) => SiblingsFor(route).Count > 0;

    /// <summary>
    /// Die Geschwister-Routen fuer das Seitentitel-Dropdown.
    ///
    /// Leitet sich aus GroupFor ab statt die Routen noch einmal
    /// aufzuzaehlen: vorher standen dieselben Namen in drei Listen, und
    /// Verbände fehlte in zweien davon - man kam ueber die Tabbar auf die
    /// Seite, aber nicht per Dropdown wieder weg.
    /// </summary>
    public static IReadOnlyList<string> SiblingsFor(string route) => GroupFor(route) switch
    {
        NavGroup.Cow => CowGroup,
        NavGroup.Claw => ClawGroup,
        _ => Array.Empty<string>()
    };
}
