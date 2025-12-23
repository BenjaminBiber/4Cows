using _4Cows_FE.Components;
using _4Cows_FE.Components.Services;
using BB_Cow;
using BB_Cow.Services;
using BB_KPI.Services;
using BBCowDataLibrary.SQL;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
LoggerService.InitializeLogger();
DatabaseService.GetDBStringFromEnvironment();
LoggerService.InitializeDBLogger();
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration); 

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
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
