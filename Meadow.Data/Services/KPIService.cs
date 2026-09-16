using System.Collections.Immutable;
using System.Data.Common;
using Meadow.Shared.Models;
using Meadow.Shared.Kpi;
using Meadow.Shared.Services;
using Meadow.Data.Services;
using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Data.Services;

public class KPIService : IKPIService
{
    private ImmutableDictionary<int, KPI> _cachedKPIs = ImmutableDictionary<int, KPI>.Empty;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;
    private readonly KpiRowProvider _rowProvider;

    public ImmutableDictionary<int, KPI> KPIs => _cachedKPIs;

    public KPIService(
        IDbContextFactory<DatabaseContext> contextFactory,
        DatabaseStatusService databaseStatusService,
        KpiRowProvider rowProvider)
    {
        _contextFactory = contextFactory;
        _databaseStatusService = databaseStatusService;
        _rowProvider = rowProvider;
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
                // SetItem statt Add - derselbe Fall wie in ClawTreatmentService: der
                // Schluessel ist die Identity, die EF zurueckschreibt. Ueber HTTP kommt
                // sie nicht mit, und Add wirft dann beim zweiten Insert.
                _cachedKPIs = _cachedKPIs.SetItem(KPI.KPIId, KPI);
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

    /// <summary>
    /// Runs one KPI script on a context of its own. Kept for the dialog's "SQL testen" button,
    /// which evaluates a single KPI in isolation.
    /// </summary>
    public async Task<string> GetKPIValue(KPI kpi, bool throwError = false)
    {
        // Split deliberately: GetKPIValueAsync reports and formats every failure of the query
        // itself, so wrapping the call in a second catch would only double-report. This try covers
        // the one thing it cannot see - failing to obtain a context at all.
        DatabaseContext context;
        try
        {
            context = await _contextFactory.CreateDbContextAsync();
        }
        catch (Exception e)
        {
            _databaseStatusService.ReportFailure();
            if (throwError)
            {
                throw;
            }

            LoggerService.LogError(typeof(KPIService), "Failed to create context for KPI value", e);
            return "--";
        }

        await using (context)
        {
            return await GetKPIValueAsync(context, kpi, throwError);
        }
    }

    /// <summary>
    /// Runs one KPI script on a caller-owned context, so a dashboard full of SQL KPIs costs one
    /// connection instead of one per KPI.
    /// </summary>
    public async Task<string> GetKPIValueAsync(DatabaseContext context, KPI kpi, bool throwError = false)
    {
        // Checked before execution, not after: until now a script saved here ran unconditionally,
        // and the application has no authentication while the documented setup connects as root.
        var rejection = KpiScriptGuard.Reject(kpi.Script);
        if (rejection is not null)
        {
            LoggerService.LogError(
                typeof(KPIService),
                $"Refused KPI script for '{kpi.Title}': {rejection}",
                new InvalidOperationException(rejection));

            if (throwError)
            {
                throw new InvalidOperationException(rejection);
            }

            return "--";
        }

        try
        {
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

    /// <summary>
    /// Everything the dashboard needs, in one pass.
    ///
    /// This replaces GetAllKPIs, which opened a DbContext PER KPI - eight connections for a single
    /// dashboard render, plus one more for the fake "+" tile's script. Now: one round trip for the
    /// definitions, zero for every declarative KPI (they are computed from caches that are already
    /// in memory), and a single shared context for whatever hand-written SQL is left.
    ///
    /// The loop itself lives in KpiDashboard, shared with HttpKPIService. What is left here is the
    /// one thing that genuinely differs between server and browser: where a hand-written script's
    /// value comes from. The context is created LAZILY inside that delegate, so a dashboard made
    /// only of declarative KPIs still touches the database exactly once - for the definitions.
    /// </summary>
    public async Task<IReadOnlyList<KpiTileModel>> GetDashboardAsync(bool addButtonKPI = true)
    {
        await GetAllDataAsync();

        DatabaseContext? sqlContext = null;

        try
        {
            return await KpiDashboard.BuildAsync(
                KPIs.Values,
                _rowProvider.Rows,
                async kpi =>
                {
                    sqlContext ??= await _contextFactory.CreateDbContextAsync();
                    return await GetKPIValueAsync(sqlContext, kpi);
                },
                DateTime.Now,
                (kpi, message) => LoggerService.LogError(
                    typeof(KPIService),
                    $"KPI '{kpi.Title}' could not be evaluated: {message}",
                    new InvalidOperationException(message)),
                addButtonKPI);
        }
        finally
        {
            if (sqlContext is not null)
            {
                await sqlContext.DisposeAsync();
            }
        }
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
