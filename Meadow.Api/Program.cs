using OfficeOpenXml;
using Meadow.Api.Components;
using Meadow.Api.Components.Services;
using Meadow.Shared.Kpi;
// DatabaseStatusService liegt seit der Interface-Naht in Meadow.Shared.Services.
// Die Registrierungen unten bleiben bewusst auf den konkreten Typen.
using Meadow.Shared.Services;
using Meadow.Data.Services;
using Meadow.Data.Sql;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using Meadow.Api.Components.Ui;

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
var demoSettings = new DemoSettings
{
    Enabled = bool.TryParse(builder.Configuration["Demo:Enabled"], out var demoOn) && demoOn,
    ResetHour = int.TryParse(builder.Configuration["Demo:ResetHour"], out var resetHour)
                && resetHour is >= 0 and <= 23
        ? resetHour
        : 3
};
var connectionString = ConnectionStringFactory.Create(databaseSettings);
await DatabaseInitializer.EnsureDatabaseAsync(connectionString);
LoggerService.InitializeDBLogger(connectionString);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// Snackbar-Konfiguration gehoert hierher, nicht in die Speicher-Methoden der
// Dialoge: dort wurde bisher an acht Stellen ein Singleton aus dem Render-Pfad
// mutiert (u.a. MaxDisplayedSnackbars = 10, weshalb sich Toasts stapelten).
builder.Services.AddMudServices(cfg =>
{
    cfg.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    cfg.SnackbarConfiguration.SnackbarVariant = Variant.Outlined;
    cfg.SnackbarConfiguration.MaxDisplayedSnackbars = 4;
    cfg.SnackbarConfiguration.VisibleStateDuration = 4000;
    cfg.SnackbarConfiguration.ShowTransitionDuration = 180;
    cfg.SnackbarConfiguration.PreventDuplicates = false;
});
builder.Services.AddSingleton<DatabaseStatusService>();
builder.Services.AddDbContextFactory<DatabaseContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddSingleton<IClawTreatmentService, ClawTreatmentService>();
builder.Services.AddSingleton<ICowTreatmentService, CowTreatmentService>();
builder.Services.AddSingleton<IPClawTreatmentService, PClawTreatmentService>();
builder.Services.AddSingleton<IPCowTreatmentService, PCowTreatmentService>();
builder.Services.AddSingleton<IMedicineService, MedicineService>();
builder.Services.AddSingleton<ICowService, CowService>();
builder.Services.AddSingleton<IWhereHowService, WhereHowService>();
builder.Services.AddSingleton<ITreatmentReasonService, TreatmentReasonService>();
builder.Services.AddSingleton<IClawFindingService, ClawFindingService>();
builder.Services.AddSingleton<IUdderService, UdderService>();
// Projects KPI rows out of the caches of the eight services above; KPIService depends on it.
// Deliberately holds no cache of its own - see the comment on the class.
builder.Services.AddSingleton<KpiRowProvider>();
builder.Services.AddSingleton<IKPIService, KPIService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<DatabaseConnectionState>();
builder.Services.AddSingleton<IXLinkService, XLinkService>();
builder.Services.AddSingleton(demoSettings);

// Shell-Zustand ist Scoped, also einer pro Circuit. Als Singleton wuerde der
// Drawer oder das Theme eines Nutzers bei allen anderen mitschalten - die
// Datenservices oben sind absichtlich prozessweit, dieser Zustand nicht.
builder.Services.AddScoped<LayoutState>();
builder.Services.AddScoped<ThemeState>();
builder.Services.AddScoped<MeadowDataLoader>();
builder.Services.AddScoped<MeadowDialogLauncher>();
// Meldet der gerade gerenderten Seite, dass eine Behandlung dazugekommen ist.
// Ohne das erschien ein Eintrag, der ueber FAB, Add-Menue oder Dashboard
// angelegt wurde, erst nach dem Neuladen in der Tabelle.
builder.Services.AddScoped<MeadowDataChanges>();

// Genau ein HostedService, je nach Modus ein anderer. Im Demo-Modus wuerde
// XLinkService.SaveCowData jede Demo-Kuh als IsGone markieren, weil der
// Scraper sie nicht liefert - danach waere jede Kuh-Auswahl in jedem Dialog
// leer. Und ein nicht erreichbarer XLink-Host (der Default ist eine
// Hof-LAN-Adresse) faerbt die Zeile im Datenbank-Dialog rot.
if (demoSettings.Enabled)
{
    builder.Services.AddHostedService<DemoResetBackgroundService>();
}
else
{
    builder.Services.AddHostedService<CowSyncBackgroundService>();
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
app.UseStaticFiles();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();

// Nach UseStaticFiles, damit vorhandene Dateien normal ausgeliefert werden:
// erst eine 404-Antwort wird hierher umgeleitet. Ohne das liefert eine
// getippte Falschadresse einen leeren Body - der <NotFound>-Zweig in
// Routes.razor greift nur bei App-interner Navigation.
app.UseStatusCodePagesWithReExecute("/nicht-gefunden");

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
