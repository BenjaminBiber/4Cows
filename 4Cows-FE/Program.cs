using _4Cows_FE.Components;
using _4Cows_FE.Components.Services;
using BB_Cow;
using BB_Cow.Services;
using BB_KPI.Services;
using BBCowDataLibrary.SQL;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using _4Cows_FE.Components.Meadow;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
LoggerService.InitializeLogger();
var databaseSettings = new DatabaseConnectionSettings
{
    Server = builder.Configuration["DB_SERVER"] ?? "127.0.0.1",
    User = builder.Configuration["DB_User"] ?? "root",
    Password = builder.Configuration["DB_Password"] ?? "admin",
    Database = builder.Configuration["DB_DB"] ?? "4cows_v2",
    Port = uint.TryParse(builder.Configuration["DB_PORT"], out var port) ? port : 3306
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
builder.Services.AddSingleton<ClawTreatmentService>();
builder.Services.AddSingleton<CowTreatmentService>();
builder.Services.AddSingleton<PClawTreatmentService>();
builder.Services.AddSingleton<PCowTreatmentService>();
builder.Services.AddSingleton<MedicineService>();
builder.Services.AddSingleton<CowService>();
builder.Services.AddSingleton<WhereHowService>();
builder.Services.AddSingleton<UdderService>();
builder.Services.AddSingleton<KPIService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<DatabaseConnectionState>();
builder.Services.AddSingleton<XLinkService>();

// Shell-Zustand ist Scoped, also einer pro Circuit. Als Singleton wuerde der
// Drawer oder das Theme eines Nutzers bei allen anderen mitschalten - die
// Datenservices oben sind absichtlich prozessweit, dieser Zustand nicht.
builder.Services.AddScoped<LayoutState>();
builder.Services.AddScoped<ThemeState>();
builder.Services.AddScoped<MeadowDataLoader>();
builder.Services.AddScoped<MeadowDialogLauncher>();

builder.Services.AddHostedService<CowSyncBackgroundService>();
builder.WebHost.UseStaticWebAssets();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    await MigrationHelper.EnsureInitialMigrationRecordedAsync(context, "20251223183944_InitialCreate", "8.0.6");
    await context.Database.MigrateAsync();
    await DataSeeder.SeedAsync(context);
}
app.UseStaticFiles();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

// Nach UseStaticFiles, damit vorhandene Dateien normal ausgeliefert werden:
// erst eine 404-Antwort wird hierher umgeleitet. Ohne das liefert eine
// getippte Falschadresse einen leeren Body - der <NotFound>-Zweig in
// Routes.razor greift nur bei App-interner Navigation.
app.UseStatusCodePagesWithReExecute("/nicht-gefunden");

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
