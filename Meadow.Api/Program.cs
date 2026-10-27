using Meadow.Api.Endpoints;
using Meadow.Api.Infrastructure;
using Meadow.Api.Mcp;
using Meadow.Shared.Mcp;
using ModelContextProtocol.Protocol;
using Meadow.Shared;
using OfficeOpenXml;
using Meadow.Api.BackgroundServices;
using Meadow.Shared.Kpi;
// DatabaseStatusService liegt seit der Interface-Naht in Meadow.Shared.Services.
// Die Registrierungen unten bleiben bewusst auf den konkreten Typen.
using Meadow.Shared.Services;
using Meadow.Data.Services;
using Meadow.Data.Sql;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
LoggerService.InitializeLogger();

// EPPlus 7 verlangt einen Lizenzkontext, bevor das erste ExcelPackage entsteht.
// Er war nirgends gesetzt - "git grep LicenseContext" fand vor diesem Commit null
// Treffer. Damit warf "new ExcelPackage()" im Klauen-Export beim ersten Kopffeld,
// der catch-Block machte daraus einen roten Toast, und der in der README
// beworbene Excel-Export hat mit dieser Paketversion nie funktioniert.
//
// NonCommercial ist die einzige Einstellung, die ohne gekauften Schluessel laeuft.
// Das ist eine Lizenzentscheidung, keine technische: wer Meadow kommerziell
// betreibt, setzt EPPlus__LicenseContext=Commercial und braucht dafuer einen
// Schluessel von EPPlus Software.
ExcelPackage.LicenseContext =
    string.Equals(builder.Configuration["EPPlus:LicenseContext"], "Commercial", StringComparison.OrdinalIgnoreCase)
        ? LicenseContext.Commercial
        : LicenseContext.NonCommercial;
var databaseSettings = new DatabaseConnectionSettings
{
    Server = builder.Configuration["DB_SERVER"] ?? "127.0.0.1",
    User = builder.Configuration["DB_User"] ?? "root",
    Password = builder.Configuration["DB_Password"] ?? "admin",
    Database = builder.Configuration["DB_DB"] ?? "4cows_v2",
    Port = uint.TryParse(builder.Configuration["DB_PORT"], out var port) ? port : 3306
};
// TryParse statt GetValue<bool>: ein Tippfehler in Demo__Enabled ("1", "yes",
// "True " mit Leerzeichen) soll den Start nicht abreissen, sondern still auf
// den sicheren Wert false fallen - gleiche Haltung wie beim DB_PORT darueber.
var demoSettings = new DemoOptions(
    Enabled: bool.TryParse(builder.Configuration["Demo:Enabled"], out var demoOn) && demoOn,
    ResetHour: int.TryParse(builder.Configuration["Demo:ResetHour"], out var resetHour)
                && resetHour is >= 0 and <= 23
        ? resetHour
        : 3);
// Woher die Kuehe kommen. Standard ist der Horizon-Agent; XLink ist der alte
// Weg und bleibt nur noch ueber diesen Schalter erreichbar. Anders als bei
// Demo:Enabled ist ein unleserlicher Wert hier eine WARNUNG wert: wer
// CowSyncSource setzt, meint etwas damit, und still auf den Standard zu
// fallen hiesse, den XLink-Abgleich ohne Hinweis abzuschalten.
var cowSyncSource = CowSyncSource.HorizonAgent;
var cowSyncRaw = builder.Configuration["CowSyncSource"];
if (!string.IsNullOrWhiteSpace(cowSyncRaw) && !Enum.TryParse(cowSyncRaw, ignoreCase: true, out cowSyncSource))
{
    cowSyncSource = CowSyncSource.HorizonAgent;
    LoggerService.LogWarning(typeof(Program),
        "CowSyncSource = {@Value} ist unbekannt. Es gilt {@Fallback}.", cowSyncRaw, cowSyncSource.ToString());
}

// VAPID aus der Konfiguration. Gleiche Haltung wie DB_PORT/Demo:Enabled: fehlt
// einer der drei Werte (PublicKey/PrivateKey/Subject), faellt Push still auf
// "aus" - der Start reisst NICHT ab. Die Entscheidung steckt in
// PushOptions.FromConfiguration ueber die reine Regel PushLogic.IsConfigured.
var pushOptions = PushOptions.FromConfiguration(builder.Configuration);
if (pushOptions.Enabled)
{
    LoggerService.LogInformation(typeof(Program), "Web Push ist konfiguriert (VAPID-Schluessel vorhanden).");
}
else
{
    LoggerService.LogInformation(typeof(Program),
        "Web Push ist deaktiviert - VAPID-Schluessel (Vapid:PublicKey/PrivateKey/Subject) fehlen oder sind unvollstaendig.");
}

// MCP-Server fuer Claude. Standard: an, ohne Token (wie die uebrige API).
// Mcp__Token setzt einen Bearer-Token voraus, Mcp__Enabled=false schaltet ab.
var mcpOptions = McpOptions.FromConfiguration(builder.Configuration);
LoggerService.LogInformation(typeof(Program), mcpOptions.Enabled
    ? $"MCP-Server aktiv unter /api/mcp ({(mcpOptions.TokenRequired ? "mit" : "ohne")} Token)."
    : "MCP-Server ist abgeschaltet (Mcp:Enabled=false).");

var connectionString = ConnectionStringFactory.Create(databaseSettings);
await DatabaseInitializer.EnsureDatabaseAsync(connectionString);
LoggerService.InitializeDBLogger(connectionString);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

// Der Zaehler, an dem ein Client erkennt, ob sich etwas geaendert hat. Muss ein
// Singleton sein - ein Zaehler pro Anfrage zaehlt nichts.
builder.Services.AddSingleton<IDataVersion, DataVersion>();

// Ein JSON-Vertrag fuer beide Seiten. Die Begruendung der Einstellungen steht
// in Meadow.Shared/MeadowJson.cs; die wichtigste ist DefaultIgnoreCondition.
builder.Services.ConfigureHttpJsonOptions(o => MeadowJson.Apply(o.SerializerOptions));
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<DatabaseStatusService>();
builder.Services.AddDbContextFactory<DatabaseContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddSingleton<IClawTreatmentService, ClawTreatmentService>();
builder.Services.AddSingleton<ICowTreatmentService, CowTreatmentService>();
builder.Services.AddSingleton<IPClawTreatmentService, PClawTreatmentService>();
builder.Services.AddSingleton<IPCowTreatmentService, PCowTreatmentService>();
builder.Services.AddSingleton<IMedicineService, MedicineService>();
// Eine Instanz, zwei Nahten: ICowService fuer alle Endpunkte, IHorizonCowStore fuer den
// Horizon-Abgleich. Zwei AddSingleton<..., CowService> ergaeben zwei Caches.
builder.Services.AddSingleton<CowService>();
builder.Services.AddSingleton<ICowService>(sp => sp.GetRequiredService<CowService>());
builder.Services.AddSingleton<IHorizonCowStore>(sp => sp.GetRequiredService<CowService>());
builder.Services.AddSingleton<IWhereHowService, WhereHowService>();
builder.Services.AddSingleton<ITreatmentReasonService, TreatmentReasonService>();
builder.Services.AddSingleton<IClawFindingService, ClawFindingService>();
builder.Services.AddSingleton<IUdderService, UdderService>();
// Die abgebrochenen Behandlungsplan-Durchlaeufe. Singleton wie die uebrigen
// Dienste, aus demselben Grund: der Cache ist prozessweit.
builder.Services.AddSingleton<ITreatmentPlanCancellationService, TreatmentPlanCancellationService>();
// Projects KPI rows out of the caches of the eight services above; KPIService depends on it.
// Deliberately holds no cache of its own - see the comment on the class.
builder.Services.AddSingleton<KpiRowProvider>();
builder.Services.AddSingleton<IKPIService, KPIService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
// Beide Quellen immer als konkrete Singletons - die Endpunkte nehmen sie
// konkret entgegen, weil sie je Quelle etwas anderes koennen (TryStart beim
// Runner, IsSyncPending beim Agenten). Nur ICowSyncService haengt am Schalter.
builder.Services.AddSingleton(new CowSyncOptions(cowSyncSource));
builder.Services.AddSingleton<XLinkService>();
// Muss Singleton sein: er haelt das "laeuft gerade"-Flag, an dem ein zweiter
// POST auf /api/cowsync/refresh seine 409 erkennt. Einer pro Anfrage wuesste
// von keinem anderen Lauf.
builder.Services.AddSingleton<XLinkRunner>();

// Der Horizon-Abgleich. Singleton, weil er den Agent-Status und die offene
// Abgleichs-Anforderung zwischen den Anfragen haelt. Zeit als Dienst, damit
// die Ablaufregel testbar bleibt.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<HorizonSyncService>();

// Der Schluessel des Agenten. Einmal beim Start gelesen: ist er nicht gesetzt,
// antworten /api/horizon/snapshot und /heartbeat mit 404, es gibt sie auf
// dieser Instanz dann also gar nicht.
builder.Services.AddSingleton(new HorizonAgentOptions(builder.Configuration["HorizonAgent:ApiKey"]));

// Die Naht zeigt auf eine der beiden Instanzen von oben, nicht auf eine
// dritte: zwei AddSingleton auf denselben Typ ergaeben zwei Zustaende, und der
// Status-Endpunkt laese dann den eines Dienstes, den sonst niemand benutzt.
builder.Services.AddSingleton<ICowSyncService>(sp => cowSyncSource == CowSyncSource.XLink
    ? sp.GetRequiredService<XLinkService>()
    : sp.GetRequiredService<HorizonSyncService>());

// Der geltende Push-Zustand (inkl. Rueckfall auf "aus") und der Sender. Beide
// Singleton: PushOptions ist unveraenderlich, WebPushSender haelt keinen Cache,
// liest je Sendung frisch aus der Datenbank und ist damit prozessweit teilbar.
builder.Services.AddSingleton(pushOptions);
builder.Services.AddSingleton<IPushSender, WebPushSender>();


// Hoechstens ein HostedService fuer den Abgleich, je nach Modus ein anderer.
// Im Demo-Modus wuerde XLinkService.SaveCowData jede Demo-Kuh als IsGone
// markieren, weil der Scraper sie nicht liefert - danach waere jede
// Kuh-Auswahl in jedem Dialog leer. Und ein nicht erreichbarer XLink-Host
// (der Default ist eine Hof-LAN-Adresse) faerbt die Zeile im
// Datenbank-Dialog rot.
//
// Bei CowSyncSource = HorizonAgent laeuft GAR KEINER: der Agent schickt den
// Snapshot von sich aus an /api/horizon, es gibt auf dem Server nichts
// abzuholen. Ein Takt hier liefe ins Leere - und zoege ueber XLinkService den
// Scraper an, also genau die Quelle, die abgeschaltet sein soll.
// Unbedingt registrieren, nicht nur im Demo-Betrieb. /api/config und
// /api/cowsync/refresh nehmen DemoOptions als Parameter; fehlt die
// Registrierung, haelt Minimal API den Parameter fuer einen Anfragerumpf und
// wirft "Body was inferred but the method does not allow inferred body
// parameters" - und zwar bei JEDER Anfrage, auch bei / und /api/health, weil
// die Endpunkt-Metadaten beim ersten Zugriff fuer die ganze Pipeline aufgebaut
// werden. Stand vorher im if-Zweig und fiel deshalb nur im Nicht-Demo-Betrieb
// auf, also genau dort, wo die Anwendung produktiv laeuft.
builder.Services.AddSingleton(demoSettings);

if (demoSettings.Enabled)
{
    builder.Services.AddHostedService<DemoResetBackgroundService>();
}
else if (cowSyncSource == CowSyncSource.XLink)
{
    builder.Services.AddHostedService<CowSyncBackgroundService>();
}

// ADDITIV zum Entweder-Oder oben: der Verband-Push-Scheduler (Task 6) laeuft
// UNABHAENGIG vom Demo-/Sync-Modus. Das "genau ein HostedService" oben meint
// nur Demo-XOR-CowSync (die einander ausschliessen); mehrere HostedServices
// nebeneinander sind erlaubt und der Host startet alle. Ist Push nicht
// konfiguriert (VAPID fehlt), tut der Dienst je Runde still nichts - der Start
// reisst nicht ab, gleiche Haltung wie ueberall beim Push.
builder.Services.AddHostedService<BandageReminderPushBackgroundService>();
// Dasselbe fuer die taegliche Sammel-Erinnerung der Behandlungsplaene.
builder.Services.AddHostedService<TreatmentPlanPushBackgroundService>();

builder.Services.AddSingleton(mcpOptions);
builder.Services.AddSingleton<McpData>();
if (mcpOptions.Enabled)
{
    // Zustandslos: jede Anfrage steht fuer sich, es gibt keine Sitzung, die
    // ein Neustart des Containers verlieren koennte.
    builder.Services.AddMcpServer(o =>
        {
            o.ServerInfo = new Implementation { Name = McpSetupGuide.ServerName, Version = "1.0.0" };
            o.ServerInstructions = McpEndpoints.ServerInstructions;
        })
        .WithHttpTransport(o => o.Stateless = true)
        .WithTools<MeadowReadTools>()
        .WithTools<MeadowWriteTools>();
}
builder.WebHost.UseStaticWebAssets();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    await MigrationHelper.EnsureInitialMigrationRecordedAsync(context, "20251223183944_InitialCreate", "8.0.6");
    await context.Database.MigrateAsync();
    await DataSeeder.SeedAsync(context);

    // Fachliche Beispieldaten nur fuer die oeffentliche Demo-Instanz.
    // Idempotent: legt nichts an, sobald Kuehe in der Datenbank stehen.
    if (demoSettings.Enabled)
    {
        await DemoDataSeeder.SeedAsync(context);
    }
}
if (!app.Environment.IsDevelopment())
{
    // Kein UseExceptionHandler("/Error") mehr: die Fehlerseite war eine Razor
    // Page und ist mit dem Umzug der Oberflaeche in den Browser entfallen.
    app.UseHsts();
}
app.UseHttpsRedirection();

// Der Client liegt als statische Dateien daneben. UseBlazorFrameworkFiles
// bedient /_framework, UseStaticFiles alles andere aus seinem wwwroot.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Ganz vorn, damit auch Fehlerantworten den Header tragen.
app.UseMiddleware<DataVersionHeaderMiddleware>();
app.UseMcpAccess(mcpOptions);

// UseStatusCodePagesWithReExecute ist ersatzlos weg. Es war die Kruecke dafuer,
// dass der <NotFound>-Zweig in Routes.razor nur bei App-interner Navigation
// griff; jetzt liefert MapFallbackToFile jede unbekannte Adresse an den Router
// im Browser aus, und der Zweig deckt endlich beide Faelle ab. Damit faellt
// auch die Sonderbehandlung weg, die ihn fuer /api wieder abschalten musste.
//
// UseAntiforgery ebenfalls: es gehoerte zu den Blazor-Server-Formularen, die es
// hier nicht mehr gibt.
var api = app.MapGroup("/api");
api.MapInfrastructureEndpoints();
api.MapCowEndpoints();
api.MapMedicineEndpoints();
api.MapWhereHowEndpoints();
api.MapTreatmentReasonEndpoints();
api.MapTreatmentPlanCancellationEndpoints();
api.MapClawFindingEndpoints();
api.MapUdderEndpoints();
api.MapCowTreatmentEndpoints();
api.MapClawTreatmentEndpoints();
api.MapPlannedCowTreatmentEndpoints();
api.MapPlannedClawTreatmentEndpoints();
api.MapSettingsEndpoints();
api.MapKpiEndpoints();
api.MapKpiEvaluationEndpoints();
api.MapClawExportEndpoints();
api.MapCowExportEndpoints();
api.MapPushEndpoints();
api.MapMcpEndpoints(app, mcpOptions);


api.MapCowSyncEndpoints();
api.MapHorizonEndpoints();


// Ein Tippfehler in einer API-Adresse muss als 404 zurueckkommen und NICHT als
// index.html mit Status 200. Ohne diese Zeile bekaeme der Client HTML, wo er
// JSON erwartet, und meldete einen Parser-Fehler statt "Endpunkt gibt es
// nicht". Konkrete Routen schlagen den Catch-all, /api/cows trifft also weiter
// seinen Endpunkt.
app.Map("/api/{**rest}", () => Results.NotFound());

// Zuletzt: MapFallbackToFile haengt sich mit Order int.MaxValue ein und kann
// deshalb keinen der Endpunkte darueber verschlucken. Jede unbekannte Adresse
// liefert damit index.html, und der Router im Browser entscheidet - inklusive
// des <NotFound>-Zweigs in Routes.razor, der bisher nur bei App-interner
// Navigation griff und deshalb UseStatusCodePagesWithReExecute als Kruecke
// brauchte. Die ist damit weg.
app.MapFallbackToFile("index.html");

app.Run();
