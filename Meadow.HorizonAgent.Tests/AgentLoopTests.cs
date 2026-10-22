using System.Net;
using Meadow.HorizonAgent.Options;
using Meadow.HorizonAgent.Reading;
using Meadow.HorizonAgent.Sending;
using Meadow.HorizonAgent.Worker;
using Meadow.Shared.Models.Horizon;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Meadow.HorizonAgent.Tests;

public class AgentLoopTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task First_tick_sends_heartbeat_and_snapshot()
    {
        var (loop, reader, client, _) = Build();

        var wait = await loop.TickAsync(CancellationToken.None);

        Assert.Equal(1, client.Heartbeats);
        Assert.Equal(1, client.Snapshots);
        Assert.Equal(1, reader.Reads);
        Assert.Null(loop.LastError);
        // Naechster Takt ist der Herzschlag nach 60 s.
        Assert.Equal(TimeSpan.FromSeconds(60), wait);
    }

    [Fact]
    public async Task Snapshot_follows_interval_heartbeat_follows_its_own_interval()
    {
        var (loop, _, client, time) = Build();

        await loop.TickAsync(CancellationToken.None);

        // 14 Herzschlaege in 14 Minuten, der Snapshot bleibt bei einem.
        for (var i = 0; i < 14; i++)
        {
            time.Advance(TimeSpan.FromSeconds(60));
            await loop.TickAsync(CancellationToken.None);
        }

        Assert.Equal(15, client.Heartbeats);
        Assert.Equal(1, client.Snapshots);

        // Minute 15: Herzschlag und Snapshot.
        time.Advance(TimeSpan.FromSeconds(60));
        await loop.TickAsync(CancellationToken.None);

        Assert.Equal(16, client.Heartbeats);
        Assert.Equal(2, client.Snapshots);
    }

    [Fact]
    public async Task Sync_requested_triggers_immediate_snapshot()
    {
        var (loop, _, client, time) = Build();

        await loop.TickAsync(CancellationToken.None);
        Assert.Equal(1, client.Snapshots);

        client.SyncRequested = true;
        time.Advance(TimeSpan.FromSeconds(60));
        await loop.TickAsync(CancellationToken.None);

        Assert.Equal(2, client.Snapshots);
    }

    [Fact]
    public async Task Unreachable_server_backs_off_1_2_4_5_5_minutes_and_resets_after_success()
    {
        var (loop, _, client, time) = Build();

        client.HeartbeatFault = () => new HttpRequestException("Verbindung abgelehnt");

        var expected = new[] { 1, 2, 4, 5, 5 };
        foreach (var minutes in expected)
        {
            var wait = await loop.TickAsync(CancellationToken.None);
            Assert.Equal(TimeSpan.FromMinutes(minutes), wait);
            Assert.NotNull(loop.LastError);
            // Waehrend der Server weg ist, wird kein Snapshot versucht.
            Assert.Equal(0, client.Snapshots);
            time.Advance(wait);
        }

        client.HeartbeatFault = null;
        var afterSuccess = await loop.TickAsync(CancellationToken.None);

        Assert.Equal(1, client.Snapshots);
        Assert.Null(loop.LastError);
        Assert.Equal(TimeSpan.FromSeconds(60), afterSuccess);

        // Der naechste Ausfall faengt wieder bei einer Minute an.
        client.HeartbeatFault = () => new HttpRequestException("Verbindung abgelehnt");
        time.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(TimeSpan.FromMinutes(1), await loop.TickAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Read_error_is_sent_with_next_heartbeat_and_no_snapshot_is_sent()
    {
        var (loop, reader, client, time) = Build();

        reader.Fault = () => new InvalidOperationException("Horizon-DB nicht erreichbar");

        await loop.TickAsync(CancellationToken.None);

        Assert.Equal(0, client.Snapshots);
        Assert.NotNull(loop.LastError);
        Assert.StartsWith("Lesen:", loop.LastError);

        time.Advance(TimeSpan.FromSeconds(60));
        await loop.TickAsync(CancellationToken.None);

        Assert.Equal(2, client.Heartbeats);
        Assert.Contains("Horizon-DB nicht erreichbar", client.LastHeartbeat!.LastError);
    }

    [Fact]
    public async Task Rejected_snapshot_keeps_normal_interval_and_reports_error()
    {
        var (loop, _, client, time) = Build();

        client.SnapshotFault = () => new MeadowRejectedException(
            (HttpStatusCode)422, "Snapshot ist leer");

        var wait = await loop.TickAsync(CancellationToken.None);

        Assert.Equal(1, client.Snapshots);
        Assert.NotNull(loop.LastError);
        Assert.Contains("Snapshot ist leer", loop.LastError);
        // Kein Backoff: der naechste Herzschlag kommt nach 60 s.
        Assert.Equal(TimeSpan.FromSeconds(60), wait);

        // Der Fehler geht mit dem naechsten Herzschlag raus.
        time.Advance(TimeSpan.FromSeconds(60));
        await loop.TickAsync(CancellationToken.None);
        Assert.Contains("Snapshot ist leer", client.LastHeartbeat!.LastError);
    }

    [Fact]
    public async Task Successful_snapshot_clears_last_error()
    {
        var (loop, reader, _, time) = Build();

        reader.Fault = () => new InvalidOperationException("Horizon-DB nicht erreichbar");
        await loop.TickAsync(CancellationToken.None);
        Assert.NotNull(loop.LastError);

        reader.Fault = null;
        time.Advance(TimeSpan.FromMinutes(15));
        await loop.TickAsync(CancellationToken.None);

        Assert.Null(loop.LastError);
    }

    private static (AgentLoop Loop, FakeReader Reader, FakeClient Client, ManualTimeProvider Time) Build()
    {
        var time = new ManualTimeProvider(Start);
        var reader = new FakeReader();
        var client = new FakeClient();
        var options = Microsoft.Extensions.Options.Options.Create(new MeadowOptions
        {
            BaseUrl = "http://localhost/",
            ApiKey = "test-key",
            HeartbeatSeconds = 60,
            SnapshotIntervalMinutes = 15,
        });

        var loop = new AgentLoop(reader, client, options, time, NullLogger<AgentLoop>.Instance);
        return (loop, reader, client, time);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class FakeReader : IHorizonReader
    {
        public int Reads { get; private set; }

        public Func<Exception>? Fault { get; set; }

        public Task<List<HorizonAnimal>> ReadAnimalsAsync(CancellationToken cancellationToken)
        {
            Reads++;
            if (Fault is not null)
            {
                throw Fault();
            }

            return Task.FromResult(new List<HorizonAnimal>
            {
                new() { AnimalId = "1", CollarNumber = 1, EarTag = "DE 0909 0001" },
            });
        }
    }

    private sealed class FakeClient : IMeadowClient
    {
        public int Heartbeats { get; private set; }

        public int Snapshots { get; private set; }

        public bool SyncRequested { get; set; }

        public AgentHeartbeat? LastHeartbeat { get; private set; }

        public Func<Exception>? HeartbeatFault { get; set; }

        public Func<Exception>? SnapshotFault { get; set; }

        public Task<AgentHeartbeatResponse> SendHeartbeatAsync(AgentHeartbeat heartbeat, CancellationToken cancellationToken)
        {
            Heartbeats++;
            LastHeartbeat = heartbeat;
            if (HeartbeatFault is not null)
            {
                throw HeartbeatFault();
            }

            var response = new AgentHeartbeatResponse { SyncRequested = SyncRequested };
            SyncRequested = false;
            return Task.FromResult(response);
        }

        public Task<HorizonSyncResult> SendSnapshotAsync(HorizonSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshots++;
            if (SnapshotFault is not null)
            {
                throw SnapshotFault();
            }

            return Task.FromResult(new HorizonSyncResult { Created = snapshot.Animals.Count });
        }
    }
}
