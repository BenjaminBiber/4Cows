using _4Cows_FE.Components;
using BB_Cow;
using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration); 
builder.Configuration.AddEnvironmentVariables();
LoggerService.InitializeLogger();
DatabaseService.GetDBStringFromCSV();

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