using _4Cows_FE.Components;
using _4Cows_FE.Components.Services;
using BB_Cow;
using BB_Cow.Services;
using BB_KPI.Services;
using BBCowDataLibrary.SQL;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

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
builder.Services.AddMudServices();
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
builder.Services.AddSingleton<DatabaseConnectionState>();
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
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
