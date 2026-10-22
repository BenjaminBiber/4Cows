using Meadow.HorizonAgent.Options;
using Meadow.HorizonAgent.Reading;
using Meadow.Shared.Models.Horizon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(o => o.ServiceName = "MeadowHorizonAgent");

builder.Services.Configure<HorizonOptions>(builder.Configuration.GetSection(HorizonOptions.SectionName));
builder.Services.Configure<MeadowOptions>(builder.Configuration.GetSection(MeadowOptions.SectionName));
// Auf Entwicklerrechnern gibt es keine Horizon-Datenbank; der Dienst laeuft nie in "Development".
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IHorizonReader, FakeHorizonReader>();
}
else
{
    builder.Services.AddSingleton<IHorizonReader, HorizonReader>();
}

var host = builder.Build();

// --dry-run: einmal lesen, zeigen was ankommt, beenden. Fuer die Einrichtung am Horizon-PC.
if (args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        var reader = host.Services.GetRequiredService<IHorizonReader>();
        var animals = await reader.ReadAnimalsAsync(CancellationToken.None);

        Console.WriteLine($"{animals.Count} Tiere gelesen");
        Console.WriteLine();
        Console.WriteLine($"{"AnimalId",-12} {"Halsband",8} {"Ohrmarke",-16} {"Abgang",-7} {"Ort",-16} {"Gruppe",-16} Beschreibung");
        foreach (var animal in animals.Take(20))
        {
            Console.WriteLine(
                $"{Cut(animal.AnimalId, 12),-12} {animal.CollarNumber,8} {Cut(animal.EarTag, 16),-16} " +
                $"{(animal.IsGone ? "ja" : "nein"),-7} {Cut(animal.Location, 16),-16} {Cut(animal.Group, 16),-16} " +
                $"{Cut(animal.Description, 40)}");
        }

        if (animals.Count > 20)
        {
            Console.WriteLine($"… und {animals.Count - 20} weitere.");
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Lesen aus Horizon fehlgeschlagen: {ex.Message}");
        return 1;
    }
}

host.Run();
return 0;

static string Cut(string? value, int length)
{
    if (string.IsNullOrEmpty(value))
    {
        return "";
    }

    return value.Length <= length ? value : value[..(length - 1)] + "…";
}
