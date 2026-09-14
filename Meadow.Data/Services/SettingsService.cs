using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Data.Services;

/// <summary>
/// Pflegbare Standardwerte. Gleiche Bauweise wie die uebrigen Services:
/// Singleton mit prozessweitem, unveraenderlichem Cache.
/// </summary>
public class SettingsService : ISettingsService
{
    private ImmutableDictionary<string, string> _cached = ImmutableDictionary<string, string>.Empty;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;

    public SettingsService(
        IDbContextFactory<DatabaseContext> contextFactory,
        DatabaseStatusService databaseStatusService)
    {
        _contextFactory = contextFactory;
        _databaseStatusService = databaseStatusService;
    }

    public ImmutableDictionary<string, string> Settings => _cached;

    public async Task GetAllDataAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var settings = await context.AppSettings.AsNoTracking().ToListAsync();
            _cached = settings.ToImmutableDictionary(s => s.SettingKey, s => s.SettingValue);
            _databaseStatusService.ReportSuccess();
            LoggerService.LogInformation(typeof(SettingsService), $"Loaded {_cached.Count} settings.");
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(SettingsService), "Failed to load settings, with {@Message}", ex, ex.Message);
        }
    }

    // Weiterleitungen; die Rumpfe stehen in SettingsLookups. Auch die beiden
    // abgeleiteten Werte: ihr Rueckfallwert ist je ein Literal, und zwei
    // Kopien von "Pflege" oder "ml" waeren genau die Drift, die die Naht
    // verhindern soll.
    public string Get(string key, string fallback = "")
        => SettingsLookups.Get(_cached, key, fallback);

    /// <summary>Anzeigetext fuer Klauen ohne erfassten Befund.</summary>
    public string ClawFindingFallback
        => SettingsLookups.ClawFindingFallback(_cached);

    /// <summary>
    /// Einheit fuer Mengen, deren Medikament keine Dosiereinheit hinterlegt
    /// hat. War vorher eine Konstante in Cow_Table.
    /// </summary>
    public string DefaultDosageUnit
        => SettingsLookups.DefaultDosageUnit(_cached);

    public async Task<bool> SetAsync(string key, string value)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.AppSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (existing is null)
            {
                await context.AppSettings.AddAsync(new AppSetting(key, value));
            }
            else
            {
                existing.SettingValue = value;
                context.AppSettings.Update(existing);
            }

            var isSuccess = await context.SaveChangesAsync() > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                _cached = _cached.SetItem(key, value);
                LoggerService.LogInformation(typeof(SettingsService), "Updated setting {Key}.", key);
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(SettingsService), "Failed to update setting, with {@Message}", ex, ex.Message);
            return false;
        }
    }
}
