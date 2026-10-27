using Meadow.Data.Services;
using Meadow.Shared.Models;
using Meadow.Shared.Models.Horizon;

namespace Meadow.Data.Tests.BaseData;

/// <summary>
/// Die Ausfuehrung des Abgleichs. Geprueft wird, was wirklich geschrieben wird:
/// ein abgelehnter Snapshot darf den Store nicht anfassen, und eine Anforderung
/// darf nicht ewig haengen bleiben, wenn der Agent schweigt.
/// </summary>
public class HorizonSyncServiceTests
{
    // Zeit als Parameter: kein Zusatzpaket, nur GetUtcNow ueberschrieben.
    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan by) => Now += by;
    }

    private sealed class FakeHorizonCowStore : IHorizonCowStore
    {
        private readonly Dictionary<string, Cow> _cows = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Calls { get; } = new();
        public int GetAllCount { get; private set; }

        /// <summary>Cow_IDs, deren Schreibversuch false liefern soll.</summary>
        public HashSet<string> FailFor { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, Cow> Cows => _cows;

        public void Seed(params Cow[] cows)
        {
            foreach (var cow in cows)
                _cows[cow.CowId] = cow;
        }

        public Task GetAllDataAsync()
        {
            GetAllCount++;
            return Task.CompletedTask;
        }

        public Task<bool> InsertDataAsync(Cow cow)
        {
            Calls.Add($"Insert:{cow.CowId}");
            if (FailFor.Contains(cow.CowId))
                return Task.FromResult(false);
            _cows[cow.CowId] = cow;
            return Task.FromResult(true);
        }

        public Task<bool> PromoteCalfAsync(string cowId, string earTagNumber)
        {
            Calls.Add($"Promote:{cowId}->{earTagNumber}");
            return Task.FromResult(!FailFor.Contains(cowId));
        }

        public Task<bool> ApplyHorizonAsync(string cowId, string lelyAnimalId, int collarNumber, string? earTag,
            bool isGone, string? location, string? group, string? description)
        {
            Calls.Add($"Apply:{cowId}");
            return Task.FromResult(!FailFor.Contains(cowId));
        }
    }

    private static HorizonAnimal Animal(
        string animalId,
        int collar = 0,
        string? earTag = null,
        bool isGone = false,
        string? location = null,
        string? group = null,
        string? description = null)
        => new()
        {
            AnimalId = animalId,
            CollarNumber = collar,
            EarTag = earTag,
            IsGone = isGone,
            Location = location,
            Group = group,
            Description = description,
        };

    private static HorizonSnapshot Snapshot(params HorizonAnimal[] animals)
        => new()
        {
            AgentVersion = "1.0.0",
            MachineName = "HORIZON-PC",
            ReadAtUtc = new DateTimeOffset(2026, 10, 22, 7, 0, 0, TimeSpan.Zero),
            Animals = animals.ToList(),
        };

    [Fact]
    public async Task Accepted_snapshot_writes_through_the_store_and_counts()
    {
        var store = new FakeHorizonCowStore();
        var known = new Cow("1001", 11, false) { LelyAnimalId = "A1" };
        var untagged = new Cow("1002", 12, false);
        var calf = new Cow("calf-1", null, 13, isCalv: true, isGone: false);
        store.Seed(known, untagged, calf);

        var time = new ManualTimeProvider();
        var service = new HorizonSyncService(store, time);

        var result = await service.ApplySnapshotAsync(Snapshot(
            Animal("A1", 11, location: "Stall 2"),        // Update
            Animal("A2", 12, earTag: "1002"),             // Link ueber die Ohrmarke
            Animal("A3", 13, earTag: "2001"),             // Promote des Kalbs
            Animal("A4", 14, earTag: "3001")));           // Create

        Assert.True(result.Accepted);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Linked);
        Assert.Equal(1, result.Promoted);
        Assert.Equal(1, result.Created);
        Assert.Empty(result.Warnings);

        Assert.Equal(1, store.GetAllCount);
        Assert.Equal(
            ["Apply:1001", "Apply:1002", "Promote:calf-1->2001", "Apply:calf-1", "Insert:3001"],
            store.Calls);

        Assert.True(service.LastSyncSucceeded);
        Assert.Null(service.LastSyncError);
        Assert.Equal(time.Now, service.LastSyncUtc);
        Assert.Equal(4, service.LastSnapshotAnimalCount);
        Assert.Equal("1.0.0", service.AgentVersion);
        Assert.Equal("HORIZON-PC", service.AgentMachineName);
    }

    [Fact]
    public async Task Rejected_snapshot_writes_nothing_and_records_the_reason()
    {
        var store = new FakeHorizonCowStore();
        store.Seed(new Cow("1001", 11, false));
        var time = new ManualTimeProvider();
        var service = new HorizonSyncService(store, time);

        var result = await service.ApplySnapshotAsync(Snapshot());

        Assert.False(result.Accepted);
        Assert.NotNull(result.RejectedReason);
        Assert.Empty(store.Calls);
        Assert.False(service.LastSyncSucceeded);
        Assert.Equal(result.RejectedReason, service.LastSyncError);
        Assert.Equal(time.Now, service.LastSyncUtc);
    }

    [Fact]
    public void Heartbeat_delivers_a_requested_sync_exactly_once()
    {
        var service = new HorizonSyncService(new FakeHorizonCowStore(), new ManualTimeProvider());

        Assert.False(service.RecordHeartbeat(new AgentHeartbeat()).SyncRequested);

        service.RequestSync();
        Assert.True(service.RecordHeartbeat(new AgentHeartbeat()).SyncRequested);
        Assert.False(service.RecordHeartbeat(new AgentHeartbeat()).SyncRequested);
        Assert.True(service.IsSyncPending);
    }

    [Fact]
    public async Task Snapshot_clears_the_pending_request()
    {
        var store = new FakeHorizonCowStore();
        store.Seed(new Cow("1001", 11, false) { LelyAnimalId = "A1" });
        var service = new HorizonSyncService(store, new ManualTimeProvider());

        service.RequestSync();
        Assert.True(service.IsSyncPending);

        await service.ApplySnapshotAsync(Snapshot(Animal("A1", 11, location: "Stall 2")));

        Assert.False(service.IsSyncPending);
    }

    [Fact]
    public void Pending_request_expires_after_three_minutes_with_error()
    {
        var time = new ManualTimeProvider();
        var service = new HorizonSyncService(new FakeHorizonCowStore(), time);

        service.RequestSync();
        time.Advance(HorizonSyncService.SyncRequestTimeout + TimeSpan.FromSeconds(1));

        Assert.False(service.IsSyncPending);
        Assert.False(service.LastSyncSucceeded);
        Assert.Equal(HorizonSyncService.AgentNoAnswer, service.LastSyncError);
        Assert.Equal(time.Now, service.LastSyncUtc);
    }

    [Fact]
    public void Heartbeat_records_agent_fields()
    {
        var time = new ManualTimeProvider();
        var service = new HorizonSyncService(new FakeHorizonCowStore(), time);

        service.RecordHeartbeat(new AgentHeartbeat
        {
            AgentVersion = "2.1.0",
            MachineName = "HORIZON-PC",
            LastError = "Keine Verbindung zur Horizon-DB.",
        });

        Assert.Equal("2.1.0", service.AgentVersion);
        Assert.Equal("HORIZON-PC", service.AgentMachineName);
        Assert.Equal(time.Now, service.LastHeartbeatUtc);
        Assert.Equal("Keine Verbindung zur Horizon-DB.", service.AgentLastError);
    }

    [Fact]
    public async Task Failed_store_write_marks_sync_unsuccessful_but_continues()
    {
        var store = new FakeHorizonCowStore();
        store.Seed(
            new Cow("1001", 11, false) { LelyAnimalId = "A1" },
            new Cow("1002", 12, false) { LelyAnimalId = "A2" });
        store.FailFor.Add("1001");
        var service = new HorizonSyncService(store, new ManualTimeProvider());

        var result = await service.ApplySnapshotAsync(Snapshot(
            Animal("A1", 11, location: "Stall 2"),
            Animal("A2", 12, location: "Stall 3")));

        Assert.True(result.Accepted);
        Assert.Equal(["Apply:1001", "Apply:1002"], store.Calls);
        Assert.Contains("Kuh 1001: Schreiben fehlgeschlagen.", result.Warnings);
        Assert.False(service.LastSyncSucceeded);
        Assert.Equal("1 Kühe konnten nicht geschrieben werden.", service.LastSyncError);
    }
}
