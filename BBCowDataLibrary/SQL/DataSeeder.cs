using System.Collections.Generic;
using System.Threading.Tasks;
using BB_Cow.Class;
using BB_Cow.Kpi;
using Microsoft.EntityFrameworkCore;

namespace BBCowDataLibrary.SQL;

public static class DataSeeder
{
    public static async Task SeedAsync(DatabaseContext context)
    {
        await SeedKpisAsync(context);
        await SeedSettingsAsync(context);
    }

    /// <summary>
    /// Legt fehlende Standardwerte an - je Schluessel einzeln, damit ein
    /// spaeter ergaenzter Wert auch in bestehenden Datenbanken auftaucht.
    /// Vorhandene Werte werden nie ueberschrieben.
    /// </summary>
    private static async Task SeedSettingsAsync(DatabaseContext context)
    {
        var defaults = new Dictionary<string, string>
        {
            [AppSetting.ClawFindingFallbackKey] = "Pflege"
        };

        var existing = await context.AppSettings
            .Select(s => s.SettingKey)
            .ToListAsync();

        var missing = defaults
            .Where(d => !existing.Contains(d.Key))
            .Select(d => new AppSetting(d.Key, d.Value))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        await context.AppSettings.AddRangeAsync(missing);
        await context.SaveChangesAsync();
    }

    private static async Task SeedKpisAsync(DatabaseContext context)
    {
        if (await context.KPIs.AnyAsync())
        {
            return;
        }

        // The seven shipped KPIs, now declarative instead of hand-written SQL. The definitions live
        // in KpiSeeds so a test can assert that all seven evaluate - they are the acceptance test
        // for the builder being complete enough.
        //
        // Only FRESH databases get these: the AnyAsync guard above leaves existing installations on
        // their hand-written scripts, which keep working unchanged as Kind = Sql.
        var defaultKpis = KpiSeeds.Default
            .Select(seed => Builder(seed.Title, seed.SortOrder, seed.Definition))
            .ToList();

        await context.KPIs.AddRangeAsync(defaultKpis);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// A declarative KPI. The click target comes from KpiSourceRegistry rather than a literal, so
    /// the route lives in one place and a seed cannot point at a page that does not exist - the
    /// failure mode of the old free-text Url, where a typo silently produced a 404.
    ///
    /// Script stays an empty string: the column is NOT NULL, and there is nothing to gain from
    /// making it nullable.
    /// </summary>
    private static KPI Builder(string title, int sortOrder, KpiDefinition definition) => new()
    {
        Title = title,
        Url = KpiSourceRegistry.Find(definition.Source)?.Route ?? string.Empty,
        Script = string.Empty,
        SortOrder = sortOrder,
        Kind = (int)KpiKind.Builder,
        Definition = KpiDefinition.Serialize(definition)
    };
}
