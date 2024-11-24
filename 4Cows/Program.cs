using _4Cows.Components;
using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.Extensions.Caching.Memory;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
DatabaseService.GetDBStringFromCSV();

// Add services to the container.
builder.Services.AddMemoryCache();
builder.Services.AddMudServices();
builder.Services.AddSingleton<ClawTreatmentService>();
builder.Services.AddSingleton<CowTreatmentService>();
builder.Services.AddSingleton<PClawTreatmentService>();
builder.Services.AddSingleton<PCowTreatmentService>();

var memoryCache = new MemoryCache(new MemoryCacheOptions());
builder.Services.AddSingleton<IMemoryCache>(memoryCache);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();

// Add Razor Components services.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
    app.UseHsts();
}
else
{
    Console.WriteLine("Debug");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRouting();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapBlazorHub();
});

app.Run();