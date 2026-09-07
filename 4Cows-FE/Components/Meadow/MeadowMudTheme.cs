using MudBlazor;

namespace _4Cows_FE.Components.Meadow;

/// <summary>
/// Das MudTheme haelt bewusst KEINE Palette.
///
/// Alle --mud-palette-* zeigen in meadow-mud.css auf var(--mw-*), damit die
/// Farben nur an einer Stelle stehen und Dark Mode allein ueber das
/// data-mw-theme-Attribut laeuft. Hier bleibt, was CSS nicht liefern kann:
/// Typografie, Standardradius und die z-Ebenen.
/// </summary>
public static class MeadowMudTheme
{
    public static readonly MudTheme Instance = new()
    {
        Typography = new Typography
        {
            Default = new Default
            {
                FontFamily = new[] { "Barlow", "-apple-system", "Segoe UI", "Roboto", "sans-serif" }
            },
            Button = new Button
            {
                TextTransform = "none"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            // = st.input / st.primaryBtn. Damit trifft
            // .mud-input-outlined-border den Meadow-Radius ohne Override.
            DefaultBorderRadius = "8px"
        },
        ZIndex = new ZIndex
        {
            // Muds Standard laesst .mud-popover bei 1201 landen, also UNTER
            // .mud-dialog (1402). Jeder Hinzufuegen-Dialog enthaelt aber ein
            // MudAutocomplete und einen MudDatePicker - deren Dropdowns
            // muessen ueber dem Dialog liegen. 1450: ueber Dialog, unter
            // Snackbar (1500).
            Popover = 1450
        }
    };
}
