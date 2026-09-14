using Meadow.Shared.Services;
using Meadow.Data.Services;
using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Api.Components.Services;

/// <summary>
/// Periodically fetches cow data from XLink (HTTP GET + Regex) and syncs it into the database.
/// Replaces the former Selenium-based XLinkScraper service and its Hangfire "Cron.Daily" job.
/// </summary>
public class CowSyncBackgroundService : BackgroundService
{
    private readonly IXLinkService _xLinkService;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly TimeSpan _interval;

    public CowSyncBackgroundService(IXLinkService xLinkService, IDbContextFactory<DatabaseContext> contextFactory)
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
