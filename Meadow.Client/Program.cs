using System.Globalization;
using System.Net.Http.Json;
using Meadow.Client.Components;
using Meadow.Client.Components.Services;
using Meadow.Client.Components.Ui;
using Meadow.Client.Services;
using Meadow.Shared;
using Meadow.Shared.Kpi;
using Meadow.Shared.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ---------------------------------------------------------------------------
// Kultur. Muss vor allem stehen, was formatiert.
//
// KpiEvaluator formatiert mit CultureInfo.CurrentCulture, KpiSqlBuilder
// schreibt FORMAT(..., 'de_DE') fest - mit dem ausdruecklichen Hinweis im Code,
// dass beide zusammen zu aendern sind. Unter Blazor Server war das die Kultur
// des Serverprozesses; im Browser waere es die Sprache des GERAETS. Auf einem
// englisch gestellten Telefon zeigte dieselbe Zahl dann "1,240" auf der Kachel
// und "1.240" in der SQL daneben.
//
// Welcher ICU-Scherben ueberhaupt geladen wird, entscheidet
// BlazorIcuDataFileName in der .csproj, nicht diese Zeilen - das passiert im
// Boot, bevor hier irgendetwas laeuft.
// ---------------------------------------------------------------------------
var german = new CultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = german;
CultureInfo.DefaultThreadCurrentUICulture = german;
CultureInfo.CurrentCulture = german;
CultureInfo.CurrentUICulture = german;

builder.RootComponents.Add<Routes>("#app");

// Ohne diese Zeile hoert <PageTitle> WORTLOS auf zu wirken. Kein Fehler, keine
// Warnung - der Titel bleibt einfach auf dem <title> aus index.html stehen, auf
// jeder Route. Vier Dateien setzen ihn: MainLayout, MeadowHeader, MeadowLanding
// und Cow_Detail.
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = new Uri(builder.HostEnvironment.BaseAddress);

// Singleton, nicht Scoped wie in der Projektvorlage. Die dreizehn Dienste sind
// Singletons - sie halten die Tabellen-Caches - und ein Singleton darf keine
// Scoped-Abhaengigkeit annehmen. Der Container prueft das und wirft beim ersten
// Rendern ScopedInSingletonException; die Seite bleibt dann beim Splash stehen.
//
// In WebAssembly gibt es ohnehin genau einen Bereich pro Tab, Scoped und
// Singleton sind hier also dasselbe - nur die Pruefung unterscheidet sie.
//
// Der HttpClient haengt seit Phase 4 an MeadowOfflineHandler. Der spiegelt
// jede Tabellenantwort nach IndexedDB und beantwortet eine ausgefallene
// Tabellenanfrage aus dem Schnappschuss - dadurch kommt die App ohne Netz mit
// Daten hoch, ohne dass einer der dreizehn Dienste davon etwas wissen muss.
builder.Services.AddSingleton(sp => new HttpClient(sp.GetRequiredService<MeadowOfflineHandler>())
{
    BaseAddress = apiBase
});

// ---------------------------------------------------------------------------
// Konfiguration vom Server nachladen.
//
// IConfiguration EXISTIERT in WebAssembly und wirft nicht - sie ist nur leer.
// DatabaseInfoDialog liest Configuration["DB_SERVER"] und faellt still auf
// "127.0.0.1" zurueck; der Dialog zeigte damit fuer immer die Platzhalter, egal
// was im Container steht. Genau deshalb liefert die API die Werte unter
// denselben SCHLUESSELN, unter denen der Dialog sie liest: die beiden Zeilen im
// Dialog bleiben damit unveraendert.
//
// Scheitert der Aufruf, bleibt die Konfiguration leer und der Dialog zeigt
// seine Rueckfallwerte - dasselbe Verhalten wie ohne diesen Block, nur dass
// die App startet.
// ---------------------------------------------------------------------------
using (var bootstrapHttp = new HttpClient { BaseAddress = apiBase })
{
    try
    {
        var info = await bootstrapHttp.GetFromJsonAsync<Dictionary<string, string?>>("api/system/info");
        if (info is not null)
        {
            builder.Configuration.AddInMemoryCollection(info!);
        }
    }
    catch (Exception)
    {
        // Bewusst verschluckt: eine nicht erreichbare API darf den Start nicht
        // verhindern. Offline soll die App spaeter aus IndexedDB hochkommen.
    }
}

builder.Services.AddSingleton(new DemoSettings
{
    Enabled = bool.TryParse(builder.Configuration["Demo:Enabled"], out var demoOn) && demoOn,
    ResetHour = int.TryParse(builder.Configuration["Demo:ResetHour"], out var resetHour)
                && resetHour is >= 0 and <= 23
        ? resetHour
        : 3
});

builder.Services.AddMudServices(cfg =>
{
    cfg.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
    cfg.SnackbarConfiguration.SnackbarVariant = MudBlazor.Variant.Outlined;
    cfg.SnackbarConfiguration.MaxDisplayedSnackbars = 4;
    cfg.SnackbarConfiguration.VisibleStateDuration = 4000;
    cfg.SnackbarConfiguration.ShowTransitionDuration = 180;
    cfg.SnackbarConfiguration.PreventDuplicates = false;
});

// DatabaseStatusService ist dieselbe Klasse wie serverseitig - ein bool mit
// einem Ereignis. "Der letzte Datenaufruf hat geklappt" bedeutet dasselbe, egal
// ob er zu MariaDB oder zu /api ging.
builder.Services.AddSingleton<DatabaseStatusService>();

// ---------------------------------------------------------------------------
// Lokaler Speicher und Outbox.
//
// Alle vier Singleton, und zwar aus demselben Grund wie die dreizehn Dienste:
// sie halten Zustand, der zum TAB gehoert und nicht zu einer Seite - den
// geoeffneten IndexedDB-Handle, den Zaehler der vorlaeufigen Ids, den
// laufenden Abgleich. MeadowSyncState ist deshalb ebenfalls Singleton und
// nicht Scoped wie LayoutState: die Dienste lesen ihn, und ein Singleton darf
// keine Scoped-Abhaengigkeit annehmen.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<MeadowLocalStore>();
builder.Services.AddSingleton<MeadowSyncState>();
builder.Services.AddSingleton<MeadowOutbox>();
builder.Services.AddSingleton<MeadowOfflineHandler>();
builder.Services.AddSingleton<OutboxProcessor>();

// Die dreizehn Dienste. Singleton, weil sie die Tabellen-Caches halten - genau
// wie ihre EF-Gegenstuecke im Serverprozess, nur dass "Prozess" hier "Browsertab"
// heisst.
builder.Services.AddSingleton<ICowService, HttpCowService>();
builder.Services.AddSingleton<ICowTreatmentService, HttpCowTreatmentService>();
builder.Services.AddSingleton<IClawTreatmentService, HttpClawTreatmentService>();
builder.Services.AddSingleton<IPCowTreatmentService, HttpPCowTreatmentService>();
builder.Services.AddSingleton<IPClawTreatmentService, HttpPClawTreatmentService>();
builder.Services.AddSingleton<IMedicineService, HttpMedicineService>();
builder.Services.AddSingleton<IWhereHowService, HttpWhereHowService>();
builder.Services.AddSingleton<ITreatmentReasonService, HttpTreatmentReasonService>();
builder.Services.AddSingleton<IClawFindingService, HttpClawFindingService>();
builder.Services.AddSingleton<IUdderService, HttpUdderService>();
builder.Services.AddSingleton<ISettingsService, HttpSettingsService>();
builder.Services.AddSingleton<IKPIService, HttpKPIService>();
builder.Services.AddSingleton<IXLinkService, HttpXLinkService>();

// Dieselben vier Instanzen noch einmal, unter der Naht, an der der
// OutboxProcessor sie erreicht. Ueber GetRequiredService und nicht als zweite
// Registrierung: sonst legte der Container ein ZWEITES Exemplar an, und das
// haette seinen eigenen Cache - die zurueckgeschriebene Server-Id landete dann
// in einem Dienst, den keine Seite je zu sehen bekommt.
builder.Services.AddSingleton<IMeadowSyncTarget>(sp => (HttpCowTreatmentService)sp.GetRequiredService<ICowTreatmentService>());
builder.Services.AddSingleton<IMeadowSyncTarget>(sp => (HttpClawTreatmentService)sp.GetRequiredService<IClawTreatmentService>());
builder.Services.AddSingleton<IMeadowSyncTarget>(sp => (HttpPCowTreatmentService)sp.GetRequiredService<IPCowTreatmentService>());
builder.Services.AddSingleton<IMeadowSyncTarget>(sp => (HttpPClawTreatmentService)sp.GetRequiredService<IPClawTreatmentService>());

// KpiRowProvider MUSS hier laufen und nicht serverseitig. Er projiziert aus den
// Caches der Dienste - also aus den Caches DIESES Browsers. Bliebe er auf dem
// Server, laese er dort Caches, die niemand mehr waermt, und jede
// Builder-Kennzahl zeigte eine voellig plausible 0. In Phase 1 wurde genau das
// am Endpunkt /api/kpi/dashboard einmal live vorgefuehrt.
builder.Services.AddSingleton<KpiRowProvider>();

// Schalenzustand: einer pro Nutzer. Unter Blazor Server war das "pro Circuit"
// und deshalb Scoped. Im Browser ist ein Tab ohnehin ein Nutzer; Scoped bleibt
// trotzdem stehen, damit die Registrierung dieselbe Aussage macht wie vorher.
builder.Services.AddScoped<LayoutState>();
builder.Services.AddScoped<ThemeState>();
builder.Services.AddScoped<MeadowDataLoader>();
builder.Services.AddScoped<MeadowDialogLauncher>();
builder.Services.AddScoped<MeadowDataChanges>();
builder.Services.AddScoped<DatabaseConnectionState>();

var host = builder.Build();

// ---------------------------------------------------------------------------
// Outbox anwerfen, BEVOR die App laeuft - RunAsync kehrt nie zurueck.
//
// In WebAssembly gibt es kein Prerendering: JS-Interop steht ab hier bereit,
// und host.Services IST der Bereich, aus dem auch die Komponenten aufloesen.
// Deshalb wird MeadowDataChanges hier einmal angefasst: es haengt sich im
// Konstruktor an MeadowSyncState, und ohne diese Zeile entstuende es erst,
// wenn die erste Seite es injiziert - eine Behandlung, die in der Zwischenzeit
// fertig wird, meldete sich dann an niemanden.
//
// InitializeAsync selbst wartet nicht auf das Netz; es liest die Outbox,
// abonniert online und visibilitychange und stoesst den ersten Durchlauf an.
// ---------------------------------------------------------------------------
host.Services.GetRequiredService<MeadowDataChanges>();
await host.Services.GetRequiredService<OutboxProcessor>().InitializeAsync();

await host.RunAsync();
