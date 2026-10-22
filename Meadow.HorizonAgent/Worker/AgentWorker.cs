using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Meadow.HorizonAgent.Worker;

/// <summary>Taktet die <see cref="AgentLoop"/> und wartet dazwischen die von ihr genannte Zeit.</summary>
public sealed class AgentWorker : BackgroundService
{
    /// <summary>Wartezeit nach einem unerwarteten Fehler - die Schleife laeuft weiter.</summary>
    private static readonly TimeSpan AfterUnexpectedError = TimeSpan.FromSeconds(30);

    private readonly AgentLoop _loop;
    private readonly TimeProvider _time;
    private readonly ILogger<AgentWorker> _logger;

    public AgentWorker(AgentLoop loop, TimeProvider time, ILogger<AgentWorker> logger)
    {
        _loop = loop;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Horizon-Agent gestartet.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = AfterUnexpectedError;
            try
            {
                wait = await _loop.TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unerwarteter Fehler im Takt des Agenten.");
            }

            try
            {
                await Task.Delay(wait, _time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Horizon-Agent beendet.");
    }
}
