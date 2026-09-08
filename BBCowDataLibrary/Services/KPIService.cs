using System.Collections.Immutable;
using System.Data.Common;
using BB_Cow.Class;
using BB_Cow.Kpi;
using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_KPI.Services;

public class KPIService
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
    /// </summary>
    public async Task<IReadOnlyList<KpiTileModel>> GetDashboardAsync(bool addButtonKPI = true)
    {
        await GetAllDataAsync();

        var tiles = new List<KpiTileModel>();

        // Rows are built once per distinct source, not once per KPI: five KPIs over cow treatments
        // share one projection.
        var rowsBySource = new Dictionary<KpiSourceId, IReadOnlyList<KpiRow>>();

        // Created lazily and only if some KPI still carries hand-written SQL. On a fresh database
        // that is never, so the dashboard touches the database exactly once.
        DatabaseContext? sqlContext = null;

        try
        {
            foreach (var kpi in KPIs.Values.OrderBy(x => x.SortOrder))
            {
                if (kpi.IsBuilder)
                {
                    tiles.Add(new KpiTileModel { Kpi = kpi, Result = EvaluateBuilder(kpi, rowsBySource) });
                    continue;
                }

                sqlContext ??= await _contextFactory.CreateDbContextAsync();
                var value = await GetKPIValueAsync(sqlContext, kpi);

                tiles.Add(new KpiTileModel
                {
                    Kpi = kpi,
                    // A legacy script yields a bare string, so Ok/Error cannot be told apart here -
                    // that distinction is exactly what the declarative path buys.
                    Result = new KpiResult
                    {
                        State = value == "--" ? KpiResultState.Empty : KpiResultState.Ok,
                        Display = value
                    }
                });
            }
        }
        finally
        {
            if (sqlContext is not null)
            {
                await sqlContext.DisposeAsync();
            }
        }

        if (tiles.Count < 8 && addButtonKPI)
        {
            tiles.Add(KpiTileModel.AddTile());
        }

        return tiles;
    }

    /// <summary>
    /// Evaluates one declarative KPI, reusing an already-built row set for its source.
    ///
    /// Note there is no fallback to <see cref="KPI.Script"/> when the definition is unusable: doing
    /// so would run a query the author believed to be switched off. A visible error is the honest
    /// outcome.
    /// </summary>
    private KpiResult EvaluateBuilder(KPI kpi, Dictionary<KpiSourceId, IReadOnlyList<KpiRow>> rowsBySource)
    {
        var definition = KpiDefinition.Deserialize(kpi.Definition);
        if (definition is null)
        {
            return Failed(kpi, "Die Definition dieser KPI ist unlesbar.");
        }

        var source = KpiSourceRegistry.Find(definition.Source);
        if (source is null)
        {
            return Failed(kpi, $"Unbekannte Datenquelle: {definition.Source}.");
        }

        if (!rowsBySource.TryGetValue(definition.Source, out var rows))
        {
            rows = _rowProvider.Rows(definition.Source);
            rowsBySource[definition.Source] = rows;
        }

        var result = KpiEvaluator.Evaluate(definition, source, rows, DateTime.Now);

        if (result.State == KpiResultState.Error)
        {
            LoggerService.LogError(
                typeof(KPIService),
                $"KPI '{kpi.Title}' could not be evaluated: {result.Message}",
                new InvalidOperationException(result.Message));
        }

        return result;
    }

    private static KpiResult Failed(KPI kpi, string message)
    {
        LoggerService.LogError(
            typeof(KPIService), $"KPI '{kpi.Title}': {message}", new InvalidOperationException(message));
        return KpiResult.Failed(message);
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
