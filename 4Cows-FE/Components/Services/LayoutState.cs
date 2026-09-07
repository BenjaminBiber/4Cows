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

    public event Action? Changed;

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
