namespace _4Cows_FE.Components.Services;

/// <summary>
/// Zustand der App-Shell: Drawer und die beiden Mobile-Overlays.
///
/// Scoped, nicht Singleton: in diesem Projekt sind alle Datenservices
/// prozessweit registriert. Ein Singleton hier wuerde bedeuten, dass der
/// Drawer eines Nutzers bei allen anderen aufgeht.
///
/// Service statt CascadingValue, weil MeadowTabbar (FAB -> Add-Menue) und
/// MeadowHeader (Titel -> Seitenmenue) Geschwister sind, nicht Eltern/Kind,
/// und weil Seiten bei Navigation alles schliessen muessen.
///
/// Der Zustand des Filter-Popovers gehoert bewusst NICHT hierher, sondern
/// bleibt seitenlokal.
/// </summary>
public sealed class LayoutState
{
    public bool DrawerOpen { get; private set; }
    public bool PageMenuOpen { get; private set; }
    public bool AddMenuOpen { get; private set; }

    /// <summary>
    /// Titel, den die aktuelle Seite selbst setzt, oder null fuer den Titel
    /// aus MeadowRoutes.TitleFor.
    ///
    /// Nur die Kuh-Seite braucht das: ihre Route traegt eine Cow_ID, und
    /// "Kuh 142" laesst sich aus einer Route nicht ableiten. MainLayout
    /// loescht den Wert bei jeder Navigation, damit er nicht auf der naechsten
    /// Seite stehen bleibt.
    /// </summary>
    public string? PageTitleOverride { get; private set; }

    /// <summary>
    /// Zweite Zeile des Kopfs, gleich neben dem Titel: die Ohrmarke der
    /// gezeigten Kuh. Sie stand frueher in einer eigenen Kachel ueber der
    /// Seite; die kostete eine Zeile Hoehe fuer eine Angabe, die neben den
    /// Titel passt.
    /// </summary>
    public string? PageSubtitle { get; private set; }

    /// <summary>Statusmarken neben dem Titel, z.B. "Kalb" oder "Abgang".</summary>
    public IReadOnlyList<PageTag> PageTags { get; private set; } = Array.Empty<PageTag>();

    /// <summary>
    /// Ziel des Zurueck-Pfeils links vom Titel, oder null fuer keinen Pfeil.
    ///
    /// Eine Adresse und nicht history.back(): eine Detailseite erreicht man
    /// auch ueber einen geteilten Link oder ein Lesezeichen, und "zurueck"
    /// waere dann die Seite davor im Browserverlauf - also irgendetwas. Der
    /// Pfeil fuehrt immer dorthin, wo das Ding in einer Liste steht.
    /// </summary>
    public string? PageBackHref { get; private set; }

    public event Action? Changed;

    /// <summary>
    /// Setzt Titel, Ohrmarke und Statusmarken in einem Zug - und meldet auch
    /// nur EINE Aenderung. Drei Setter hiessen drei Ereignisse und damit drei
    /// Renderdurchlaeufe des Layouts fuer denselben Seitenwechsel.
    /// </summary>
    public void SetPageHeading(
        string? title,
        string? subtitle = null,
        IReadOnlyList<PageTag>? tags = null,
        string? backHref = null)
    {
        tags ??= Array.Empty<PageTag>();

        // SequenceEqual und nicht ==: PageTag ist ein record (Wertgleichheit),
        // die Liste darum herum aber nicht. Ohne den Vergleich meldete jedes
        // Neuaufbauen der Kuh-Seite eine Aenderung, obwohl dieselbe Kuh
        // dasselbe Schild traegt.
        if (PageTitleOverride == title
            && PageSubtitle == subtitle
            && PageBackHref == backHref
            && PageTags.SequenceEqual(tags))
        {
            return;
        }

        PageTitleOverride = title;
        PageSubtitle = subtitle;
        PageTags = tags;
        PageBackHref = backHref;
        Changed?.Invoke();
    }

    /// <summary>Oeffnet genau ein Overlay und schliesst dabei die anderen.</summary>
    public void ToggleDrawer() => Toggle(Overlay.Drawer);

    public void TogglePageMenu() => Toggle(Overlay.PageMenu);

    public void ToggleAddMenu() => Toggle(Overlay.AddMenu);

    public void CloseAll()
    {
        if (!DrawerOpen && !PageMenuOpen && !AddMenuOpen)
        {
            return;
        }

        DrawerOpen = false;
        PageMenuOpen = false;
        AddMenuOpen = false;
        Changed?.Invoke();
    }

    private void Toggle(Overlay overlay)
    {
        var wasOpen = overlay switch
        {
            Overlay.Drawer => DrawerOpen,
            Overlay.PageMenu => PageMenuOpen,
            _ => AddMenuOpen
        };

        DrawerOpen = !wasOpen && overlay == Overlay.Drawer;
        PageMenuOpen = !wasOpen && overlay == Overlay.PageMenu;
        AddMenuOpen = !wasOpen && overlay == Overlay.AddMenu;

        Changed?.Invoke();
    }

    private enum Overlay
    {
        Drawer,
        PageMenu,
        AddMenu
    }
}

/// <summary>
/// Eine Statusmarke neben dem Seitentitel.
///
/// Ton statt CSS-Klasse: der Zustand der Shell soll nicht wissen, wie ein
/// Abgang aussieht - das entscheidet MeadowHeader.
/// </summary>
public sealed record PageTag(string Text, PageTagTone Tone = PageTagTone.Info);

public enum PageTagTone
{
    /// <summary>Neutrale Einordnung, z.B. "Kalb".</summary>
    Info,

    /// <summary>Das Tier ist nicht mehr im Bestand.</summary>
    Removed
}
