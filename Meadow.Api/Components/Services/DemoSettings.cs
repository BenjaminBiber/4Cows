using _4Cows_FE.Components.Meadow;

namespace _4Cows_FE.Components.Services;

/// <summary>
/// Demo-Modus. Steuert, ob "/" die Marketing-Seite zeigt (true) oder wie
/// bisher das Dashboard (false).
///
/// appsettings.json:  "Demo": { "Enabled": true, "ResetHour": 3 }
/// Docker / ENV:      Demo__Enabled=true          (DOPPELTER Unterstrich)
/// User Secrets:      dotnet user-secrets set "Demo:Enabled" "true"
///
/// Bewusst dieselbe Bauweise wie DatabaseConnectionSettings in Program.cs:
/// ein POCO von Hand statt IOptions - die App kennt dieses Muster sonst
/// nirgends, und der Wert kann zur Laufzeit ohnehin nicht wechseln. Was am
/// Flag haengt (welcher HostedService laeuft, ob das Landing-Stylesheet
/// verlinkt wird), entscheidet sich beim Start.
/// </summary>
public sealed class DemoSettings
{
    public bool Enabled { get; init; }

    /// <summary>
    /// Stunde (Ortszeit, 0-23), zu der DemoResetBackgroundService die
    /// Demo-Daten zuruecksetzt. Nur im Demo-Modus ueberhaupt registriert.
    /// </summary>
    public int ResetHour { get; init; } = 3;

    /// <summary>
    /// Wohin "Dashboard" in Drawer und Tabbar zeigt. Ohne Demo ist die
    /// kanonische Adresse des Dashboards "/" (MeadowRoutes.Root), mit Demo
    /// "/app" - dort gehoert "/" der Landing-Page. Ein einziger Ort dafuer,
    /// damit die Bedingung nicht in drei Komponenten steht.
    ///
    /// Wichtig fuer die Hervorhebung: MeadowDrawer vergleicht per
    /// NavLinkMatch.All, MeadowTabbar ueber MeadowRoutes.GroupFor. Zeigte der
    /// Link im Betriebsmodus auf "app", waere der Eintrag auf "/" - der
    /// Adresse, unter der die Nutzer das Dashboard tatsaechlich aufrufen -
    /// nicht aktiv.
    /// </summary>
    public string DashboardHref => Enabled ? MeadowRoutes.Dashboard : MeadowRoutes.Root;
}
