using System.Collections.Immutable;
using System.Data.Common;
using BB_Cow.Class;
using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;

namespace BB_KPI.Services;

public class KPIService
{
    private ImmutableDictionary<int, KPI> _cachedKPIs = ImmutableDictionary<int, KPI>.Empty;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;

    public ImmutableDictionary<int, KPI> KPIs => _cachedKPIs;

    public KPIService(IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
    {
        _contextFactory = contextFactory;
        _databaseStatusService = databaseStatusService;
    }

    public async Task GetAllDataAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var kpis = await context.KPIs.AsNoTracking().ToListAsync();
            _cachedKPIs = kpis.ToImmutableDictionary(c => c.KPIId);
            _databaseStatusService.ReportSuccess();
            LoggerService.LogInformation(typeof(KPIService), $"Loaded {_cachedKPIs.Count} KPIs.");
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(KPIService), "Failed to load KPIs, with {@Message}", ex, ex.Message);
        }
    }

    public async Task<bool> InsertDataAsync(KPI KPI)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.KPIs.AddAsync(KPI);
            var isSuccess = await context.SaveChangesAsync() > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                _cachedKPIs = _cachedKPIs.Add(KPI.KPIId, KPI);
                LoggerService.LogInformation(typeof(KPIService), "Inserted KPI: {@KPI}.", KPI);
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(KPIService), "Failed to insert KPI, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    public async Task<string> GetKPIValue(KPI kpi, bool throwError = false)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var result = await context.Database.SqlQueryRaw<string>(kpi.Script).ToListAsync();
            _databaseStatusService.ReportSuccess();

            if (!result.Any() || result.FirstOrDefault() == null)
            {
                LoggerService.LogError(typeof(KPIService), "KPI didnt return Value", new NullReferenceException());
                if (throwError)
                {
                    throw new Exception("KPI didnt return Value");
                }
                return "--";
            }

            return result.First();
        }
        catch (Exception e)
        {
            _databaseStatusService.ReportFailure();
            if (throwError)
            {
                throw;
            }

            LoggerService.LogError(typeof(KPIService), "Error while getting KPI-Value", e);
            return "--";
        }
    }

    public async Task<Dictionary<KPI, string>> GetAllKPIs(bool addButtonKPI = true)
    {
        var result = new Dictionary<KPI, string>();
        await GetAllDataAsync();
        foreach (var kpi in KPIs.Values.OrderBy(x => x.SortOrder))
        {
            var item = await GetKPIValue(kpi);
            result.Add(kpi, item);
        }

        if (result.Count < 8 && addButtonKPI)
        {
            var newKPI = new KPI(int.MinValue, "KPI hinzufügen", "/Settings", "select '+' as value", 8 );
            result.Add(newKPI, "+");
        }
        return result;
    }
    
    public async Task<bool> UpdateDataAsync(KPI KPI)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.KPIs.Update(KPI);
            var isSuccess = await context.SaveChangesAsync() > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(KPIService), "Updated KPI: {@KPI}.", KPI);
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(KPIService), "Failed to update KPI, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    public async Task<bool> DeleteDataAsync(int kpiId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var affectedRows = await context.KPIs.Where(k => k.KPIId == kpiId).ExecuteDeleteAsync();
            _databaseStatusService.ReportSuccess();

            if (affectedRows > 0)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(KPIService), "Deleted KPI with ID: {KPIId}.", kpiId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(KPIService), "Failed to delete KPI, with {@Message}", ex, ex.Message);
            return false;
        }
    }

}
