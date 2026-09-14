using Microsoft.JSInterop;

namespace _4Cows_FE.Components.Services;

/// <summary>
/// Dark Mode. Die Wahl liegt in localStorage; gesetzt wird sie als
/// data-mw-theme-Attribut auf &lt;html&gt;.
///
/// Das Attribut selbst setzt bereits das Inline-Skript im &lt;head&gt; von
/// App.razor, also VOR dem ersten Paint. Dieser Service liest den Wert
/// danach nur nach, damit MudThemeProvider mitzieht - deshalb ist ein
/// spaeter korrektes IsDark unproblematisch: sichtbar ist nichts davon
/// abhaengig, weil alle Mud-Farben auf var(--mw-*) zeigen.
/// </summary>
public sealed class ThemeState
{
    private readonly IJSRuntime _js;
    private bool _initialized;

    public ThemeState(IJSRuntime js) => _js = js;

    public bool IsDark { get; private set; }

    public event Action? Changed;

    /// <summary>Aus OnAfterRenderAsync(firstRender) aufrufen - vorher gibt es kein JS.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            var theme = await _js.InvokeAsync<string>("meadowTheme.get");
            var isDark = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase);
            if (isDark == IsDark)
            {
                return;
            }

            IsDark = isDark;
            Changed?.Invoke();
        }
        catch (JSException)
        {
            // Kein JS erreichbar (Prerender, blockierte Skripte): hell bleiben.
        }
    }

    public async Task ToggleAsync()
    {
        IsDark = !IsDark;
        Changed?.Invoke();

        try
        {
            await _js.InvokeVoidAsync("meadowTheme.set", IsDark ? "dark" : "light");
        }
        catch (JSException)
        {
            // localStorage kann im privaten Modus werfen; das Attribut setzt
            // meadowTheme.set selbst im catch, die UI bleibt konsistent.
        }
    }
}
