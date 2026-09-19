namespace Meadow.Client.Components.Ui;

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

    public const string CowTreatments = "Kuh_Daten";
    public const string PlannedCowTreatments = "geplante_Kuh_Daten";
    public const string Bandages = "Verband_Daten";
    public const string ClawTreatments = "Klauen_Daten";
    public const string PlannedClawTreatments = "geplante_Klauen_Daten";
    public const string Settings = "Settings";

    /// <summary>
    /// Die Liste der Uebertragungen, die allein nicht mehr weiterkommen.
    ///
    /// Bewusst eine eigene Adresse und kein Dialog: Kriterium 3 der Abnahme
    /// lautet "sichtbar, auch nach einem Neuladen" - ein Dialog ist nach dem
    /// naechsten Start weg, eine Adresse laesst sich aufheben und noch einmal
    /// oeffnen. Erreichbar ueber das Statusband; im Drawer steht sie nicht,
    /// weil sie die meiste Zeit leer ist.
    /// </summary>
    public const string Transfers = "uebertragung";

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

    /// <summary>
    /// Die Cow_ID aus einer Kuh-Route, oder null, wenn die Route keine
    /// Kuh-Seite ist. Gegenstueck zu <see cref="CowDetailFor"/>: die Tabbar
    /// bekommt die Route escaped ("Kuh/DE%2008%201523%204567"), also wird das
    /// erste Segment abgeschnitten und der Rest wieder entschluesselt.
    ///
    /// Gebraucht, damit der FAB auf der Kuh-Seite die Hinzufuegen-Dialoge mit
    /// genau diesem Tier vorbelegt - ohne dass das Layout die Route selbst
    /// zerlegen muss.
    /// </summary>
    public static string? CowIdFrom(string route)
    {
        if (!BaseOf(route).Equals(CowDetail, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var slash = route.IndexOf('/');
        if (slash < 0 || slash + 1 >= route.Length)
        {
            return null;
        }

        return Uri.UnescapeDataString(route[(slash + 1)..]);
    }

    /// <summary>
    /// Erstes Segment der Kennzahl-Seite; danach folgt die KPI_ID. Wie bei
    /// <see cref="CowDetail"/> eine eigene Konstante, weil TitleFor, GroupFor und IsWide ueber
    /// BaseOf nur dieses Segment sehen - ohne Eintrag hier liefert TitleFor den 404-Titel und
    /// GroupFor NavGroup.None, und Drawer wie Tabbar verloeren ihre Markierung.
    /// </summary>
    public const string KpiDetail = "kennzahl";

    public static string KpiDetailFor(int kpiId) => $"{KpiDetail}/{kpiId}";

    /// <summary>
    /// Die Einstellungen, direkt auf dem KPI-Reiter. Gebraucht vom Leerzustand des Dashboards und
    /// von der Fehleranzeige einer Kachel - ohne den Query-Parameter landete man auf dem ersten
    /// Reiter und muesste selbst weiterklicken.
    /// </summary>
    public const string SettingsKpis = Settings + "?tab=kpis";

    private static readonly Dictionary<string, string> TitleMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Beide Dashboard-Adressen. Root wird nur im Betriebsmodus
            // gelesen: im Demo-Modus rendert "/" die Landing-Page im
            // LandingLayout, das TitleFor gar nicht erst aufruft. Fehlt der
            // Eintrag trotzdem, liefert TitleFor unten den 404-Titel.
            [Root] = "Dashboard",
            [Dashboard] = "Dashboard",
            // Ohne die KPI_ID - den Titel mit dem Namen der Kennzahl setzt die Seite selbst
            // ueber LayoutState.SetPageTitle, genau wie die Kuh-Seite.
            [KpiDetail] = "Kennzahl",
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
            [Transfers] = "Übertragung",
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
    /// Ob diese Route bereits das Dashboard ist. Beide Adressen zaehlen: Root rendert es im
    /// Betriebsmodus, im Demo-Modus liegt dort die Landing-Page.
    /// </summary>
    public static bool IsDashboard(string route) => BaseOf(Normalize(route)) is Root or Dashboard;

    /// <summary>
    /// Wohin der Titel im Kopf fuehrt, oder null, wenn man schon dort ist.
    ///
    /// Die Geste, die man von jedem Produktnamen oben links kennt. Und die Entscheidung gehoert
    /// hierher und nicht ins Layout: ob eine Route das Dashboard IST, ist eine Routenfrage, und
    /// alle anderen werden auch hier beantwortet.
    /// </summary>
    public static string? HomeFor(string route) => IsDashboard(route) ? null : Dashboard;

    /// <summary>
    /// Zeigt diese Adresse auf eine Seite, die es gibt?
    ///
    /// Gebraucht im Expertenmodus des KPI-Dialogs, wo die URL weiter Freitext ist. Eine
    /// Builder-Kennzahl leitet ihr Klickziel aus der Definition ab; ein handgeschriebenes Skript
    /// darf dagegen bewusst irgendwohin zeigen, deshalb ist das eine WARNUNG und kein Verbot.
    /// </summary>
    public static bool Exists(string? route)
        => !string.IsNullOrWhiteSpace(route)
           && TitleMap.ContainsKey(BaseOf(Normalize(route)));

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
        Root or Dashboard or KpiDetail => NavGroup.Dashboard,
        Cows or CowDetail or CowTreatments or PlannedCowTreatments => NavGroup.Cow,
        ClawTreatments or PlannedClawTreatments or Bandages => NavGroup.Claw,
        Settings or Transfers => NavGroup.System,
        _ => NavGroup.None
    };

    /// <summary>
    /// Ob die Seite die volle Inhaltsbreite bekommt statt des 1260px-Deckels
    /// aus --mw-shell-max. Dashboard und Kuh-Seite: deren Kacheln und
    /// Diagramme fuellen die Breite, waehrend die Tabellen mit vier Spalten
    /// davon nichts haetten ausser laengeren Zeilen. Die Kuh-UEBERSICHT ist
    /// deshalb bewusst nicht dabei - die ist wieder eine Tabelle.
    /// </summary>
    public static bool IsWide(string route)
        => BaseOf(route) is Root or Dashboard or CowDetail or KpiDetail;

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
    public static IReadOnlyList<string> SiblingsFor(string route) => BaseOf(route) switch
    {
        // Die Kuh-Seite gehoert zwar zur Kuh-Gruppe, ist aber keine ihrer
        // Listen, sondern EIN Tier daraus. Ein Dropdown mit "Kühe / Kuh
        // Behandlungen / Geplante" am Titel "Kuh 103" verspricht ein
        // Umschalten zwischen Gleichrangigen - es gibt hier aber nur einen
        // Weg heraus, und der steht als Zurueck-Pfeil daneben.
        CowDetail => Array.Empty<string>(),

        // Aus demselben Grund: die Kennzahl-Seite ist EINE Kennzahl, keine Liste. Die
        // Dashboard-Gruppe hat ohnehin keine Geschwister, das hier steht als Absicht da.
        KpiDetail => Array.Empty<string>(),

        _ => GroupFor(route) switch
        {
            NavGroup.Cow => CowGroup,
            NavGroup.Claw => ClawGroup,
            _ => Array.Empty<string>()
        }
    };
}
