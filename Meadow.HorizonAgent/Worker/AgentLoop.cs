using Meadow.HorizonAgent.Options;
using Meadow.HorizonAgent.Reading;
using Meadow.HorizonAgent.Sending;
using Meadow.Shared.Models.Horizon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meadow.HorizonAgent.Worker;

/// <summary>Ein Takt der Agent-Schleife, ohne Warten - damit testbar.</summary>
public sealed class AgentLoop
{
    /// <summary>Hoechste Wartezeit, wenn der Server nicht erreichbar ist.</summary>
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    private readonly IHorizonReader _reader;
    private readonly IMeadowClient _client;
    private readonly MeadowOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<AgentLoop> _logger;

    // DateTimeOffset.MinValue: der erste Takt schickt sofort Lebenszeichen und Snapshot.
    private DateTimeOffset _nextHeartbeat = DateTimeOffset.MinValue;
    private DateTimeOffset _nextSnapshot = DateTimeOffset.MinValue;
    private int _fails;

    public AgentLoop(
        IHorizonReader reader,
        IMeadowClient client,
        IOptions<MeadowOptions> options,
        TimeProvider time,
        ILogger<AgentLoop> logger)
    {
        _reader = reader;
        _client = client;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    /// <summary>Der zuletzt aufgetretene Fehler; geht mit dem naechsten Lebenszeichen an den Server.</summary>
    public string? LastError { get; private set; }

    private TimeSpan HeartbeatInterval =>
        TimeSpan.FromSeconds(Math.Max(5, _options.HeartbeatSeconds));

    private TimeSpan SnapshotInterval =>
        TimeSpan.FromMinutes(Math.Max(1, _options.SnapshotIntervalMinutes));

    /// <summary>Fuehrt faellige Arbeit aus und liefert die Wartezeit bis zum naechsten Takt (mind. 1 s).</summary>
    public async Task<TimeSpan> TickAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();

        if (now >= _nextHeartbeat)
        {
            var heartbeat = new AgentHeartbeat { LastError = LastError };
            try
            {
                var response = await _client.SendHeartbeatAsync(heartbeat, cancellationToken);
                _fails = 0;
                _nextHeartbeat = now + HeartbeatInterval;
                if (response.SyncRequested)
                {
                    _logger.LogInformation("Meadow fordert einen Snapshot an.");
                    _nextSnapshot = now;
                }
            }
            catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
            {
                var delay = NextBackoff();
                LastError = $"Senden: {ex.Message}";
                _logger.LogWarning(ex, "Meadow nicht erreichbar, naechster Versuch in {Delay}.", delay);
                _nextHeartbeat = now + delay;
                _nextSnapshot = now + delay;
                return WaitFrom(now);
            }
            catch (MeadowRejectedException ex)
            {
                LastError = $"Server: {ex.Message}";
                _logger.LogError(ex, "Meadow hat das Lebenszeichen abgewiesen.");
                _nextHeartbeat = now + HeartbeatInterval;
            }
        }

        if (now >= _nextSnapshot)
        {
            List<HorizonAnimal> animals;
            try
            {
                animals = await _reader.ReadAnimalsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LastError = $"Lesen: {ex.Message}";
                _logger.LogError(ex, "Lesen aus Horizon fehlgeschlagen.");
                _nextSnapshot = now + SnapshotInterval;
                return WaitFrom(now);
            }

            var snapshot = new HorizonSnapshot { ReadAtUtc = now, Animals = animals };
            try
            {
                var result = await _client.SendSnapshotAsync(snapshot, cancellationToken);
                _fails = 0;
                _nextSnapshot = now + SnapshotInterval;

                if (result.Accepted)
                {
                    LastError = null;
                    _logger.LogInformation(
                        "Snapshot mit {Count} Tieren uebernommen: {Created} neu, {Updated} geaendert, " +
                        "{Linked} verknuepft, {Promoted} uebernommen, {Unchanged} unveraendert, {Skipped} abgegangen.",
                        animals.Count, result.Created, result.Updated, result.Linked, result.Promoted,
                        result.Unchanged, result.SkippedGone);
                }
                else
                {
                    LastError = $"Server: {result.RejectedReason}";
                    _logger.LogWarning("Meadow hat den Snapshot abgelehnt: {Reason}", result.RejectedReason);
                }
            }
            catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
            {
                var delay = NextBackoff();
                LastError = $"Senden: {ex.Message}";
                _logger.LogWarning(ex, "Meadow nicht erreichbar, naechster Versuch in {Delay}.", delay);
                _nextHeartbeat = now + delay;
                _nextSnapshot = now + delay;
                return WaitFrom(now);
            }
            catch (MeadowRejectedException ex)
            {
                LastError = $"Server: {ex.Message}";
                _logger.LogError(ex, "Meadow hat den Snapshot abgewiesen.");
                _nextSnapshot = now + SnapshotInterval;
            }
        }

        return WaitFrom(now);
    }

    /// <summary>Nicht erreichbar heisst: Netzwerk weg oder Zeitueberschreitung - hier hilft nur warten.</summary>
    private static bool IsUnreachable(Exception ex, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && ex is HttpRequestException or TaskCanceledException;

    /// <summary>1, 2, 4, 5, 5 … Minuten. Ein Erfolg setzt den Zaehler zurueck.</summary>
    private TimeSpan NextBackoff()
    {
        _fails++;
        if (_fails >= 4)
        {
            return MaxBackoff;
        }

        return TimeSpan.FromMinutes(Math.Pow(2, _fails - 1));
    }

    private TimeSpan WaitFrom(DateTimeOffset now)
    {
        var next = _nextHeartbeat < _nextSnapshot ? _nextHeartbeat : _nextSnapshot;
        var wait = next - now;
        return wait < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : wait;
    }
}
