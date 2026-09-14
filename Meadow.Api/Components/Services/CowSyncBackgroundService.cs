using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace _4Cows_FE.Components.Services;

/// <summary>
/// Periodically fetches cow data from XLink (HTTP GET + Regex) and syncs it into the database.
/// Replaces the former Selenium-based XLinkScraper service and its Hangfire "Cron.Daily" job.
/// </summary>
public class CowSyncBackgroundService : BackgroundService
{
    private readonly XLinkService _xLinkService;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly TimeSpan _interval;

    public CowSyncBackgroundService(XLinkService xLinkService, IDbContextFactory<DatabaseContext> contextFactory)
    {
        _xLinkService = xLinkService;
        _contextFactory = contextFactory;
        _interval = TimeSpan.FromHours(
            double.TryParse(Environment.GetEnvironmentVariable("XLinkSyncIntervalHours"), out var hours) && hours > 0
                ? hours
                : 24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            await SyncAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                await _xLinkService.RefreshCowsAsync(cancellationToken);
            }
            else
            {
                LoggerService.LogWarning(typeof(CowSyncBackgroundService), "Database connection unavailable. Skipping XLink sync.");
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown, nothing to log.
        }
        catch (Exception e)
        {
            LoggerService.LogError(typeof(CowSyncBackgroundService), "Error during XLink sync: {@Message}", e, e.Message);
        }
    }
}
