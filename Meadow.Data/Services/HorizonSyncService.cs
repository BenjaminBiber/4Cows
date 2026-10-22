using Meadow.Shared.Lookups;
using Meadow.Shared.Models.Horizon;
using Meadow.Shared.Services;

namespace Meadow.Data.Services;

/// <summary>
/// Fuehrt den Horizon-Abgleich aus: plant ueber den <see cref="HorizonSyncPlanner"/> und
/// schreibt das Ergebnis ueber den <see cref="IHorizonCowStore"/>. Haelt ausserdem den
/// Zustand des Agenten (Lebenszeichen, Version, letzter Fehler) und das Flag
/// "Abgleich angefordert", das der Agent beim naechsten Lebenszeichen abholt.
/// </summary>
public sealed class HorizonSyncService : ICowSyncService
{
    /// <summary>So lange darf eine Anforderung auf einen Snapshot warten.</summary>
    public static readonly TimeSpan SyncRequestTimeout = TimeSpan.FromMinutes(3);

    public const string AgentNoAnswer = "Der Horizon-Agent hat nicht geantwortet.";

    private readonly IHorizonCowStore _store;
    private readonly TimeProvider _time;

    // Zwei Snapshots duerfen nicht gleichzeitig auf demselben Cache arbeiten.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DateTimeOffset? _syncRequestedAt;
    private bool _requestDelivered;

    public HorizonSyncService(IHorizonCowStore store, TimeProvider time)
    {
        _store = store;
        _time = time;
    }

    public CowSyncSource Source => CowSyncSource.HorizonAgent;

    /// <summary>Zeitpunkt des letzten Abgleichs, null vor dem ersten Lauf.</summary>
    public DateTimeOffset? LastSyncUtc { get; private set; }

    public bool LastSyncSucceeded { get; private set; }

    /// <summary>Begruendung des letzten fehlgeschlagenen oder abgelehnten Abgleichs.</summary>
    public string? LastSyncError { get; private set; }

    public string? AgentVersion { get; private set; }

    public string? AgentMachineName { get; private set; }

    public DateTimeOffset? LastHeartbeatUtc { get; private set; }

    public int? LastSnapshotAnimalCount { get; private set; }

    /// <summary>Was der Agent zuletzt als eigenen Fehler gemeldet hat.</summary>
    public string? AgentLastError { get; private set; }

    /// <summary>Wirft nur bei technischen Fehlern; Schutzregel -> Result mit RejectedReason.</summary>
    public async Task<HorizonSyncResult> ApplySnapshotAsync(HorizonSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ApplyCoreAsync(snapshot, cancellationToken);
        }
        catch (Exception ex)
        {
            // Wie im XLinkService: den Fehlschlag merken, dann weiterwerfen.
            LastSyncUtc = _time.GetUtcNow();
            LastSyncSucceeded = false;
            LastSyncError = ex.Message;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<HorizonSyncResult> ApplyCoreAsync(HorizonSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.AgentVersion))
            AgentVersion = snapshot.AgentVersion;
        if (!string.IsNullOrWhiteSpace(snapshot.MachineName))
            AgentMachineName = snapshot.MachineName;

        await _store.GetAllDataAsync();
        var plan = HorizonSyncPlanner.Plan(_store.Cows.Values.ToList(), snapshot);

        var now = _time.GetUtcNow();

        if (plan.RejectedReason is not null)
        {
            // Abgelehnt heisst: kein einziger Schreibzugriff.
            LastSyncUtc = now;
            LastSyncSucceeded = false;
            LastSyncError = plan.RejectedReason;
            return new HorizonSyncResult { RejectedReason = plan.RejectedReason };
        }

        var result = new HorizonSyncResult
        {
            Unchanged = plan.Unchanged,
            SkippedGone = plan.SkippedGone,
            Warnings = plan.Warnings.ToList(),
        };

        var failed = 0;

        foreach (var action in plan.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var animal = action.Animal;
            bool ok;

            switch (action.Kind)
            {
                case HorizonActionKind.Promote:
                    // Erst zur Kuh machen (Ohrmarke wird Cow_ID), dann die Horizon-Werte uebernehmen.
                    ok = await _store.PromoteCalfAsync(action.CowId, animal.EarTag!);
                    if (ok)
                        ok = await ApplyAsync(action.CowId, animal);
                    result.Promoted++;
                    break;

                case HorizonActionKind.Create:
                    ok = await _store.InsertDataAsync(action.NewCow!);
                    result.Created++;
                    break;

                case HorizonActionKind.Link:
                    ok = await ApplyAsync(action.CowId, animal);
                    result.Linked++;
                    break;

                default:
                    ok = await ApplyAsync(action.CowId, animal);
                    result.Updated++;
                    break;
            }

            if (!ok)
            {
                failed++;
                result.Warnings.Add($"Kuh {action.CowId}: Schreiben fehlgeschlagen.");
            }
        }

        LastSnapshotAnimalCount = snapshot.Animals?.Count ?? 0;
        LastSyncUtc = now;
        LastSyncSucceeded = failed == 0;
        LastSyncError = failed == 0 ? null : $"{failed} Kühe konnten nicht geschrieben werden.";

        // Der Snapshot beantwortet eine offene Anforderung.
        _syncRequestedAt = null;
        _requestDelivered = false;

        return result;
    }

    private Task<bool> ApplyAsync(string cowId, HorizonAnimal animal)
        => _store.ApplyHorizonAsync(cowId, animal.AnimalId, animal.CollarNumber, animal.EarTag,
            animal.IsGone, animal.Location, animal.Group, animal.Description);

    /// <summary>Merkt das Lebenszeichen und liefert (und verbraucht) das Flag "angefordert".</summary>
    public AgentHeartbeatResponse RecordHeartbeat(AgentHeartbeat heartbeat)
    {
        if (!string.IsNullOrWhiteSpace(heartbeat.AgentVersion))
            AgentVersion = heartbeat.AgentVersion;
        if (!string.IsNullOrWhiteSpace(heartbeat.MachineName))
            AgentMachineName = heartbeat.MachineName;
        AgentLastError = heartbeat.LastError;
        LastHeartbeatUtc = _time.GetUtcNow();

        // Nur das erste Lebenszeichen nach der Anforderung holt sie ab.
        var requested = _syncRequestedAt is not null && !_requestDelivered;
        if (requested)
            _requestDelivered = true;

        return new AgentHeartbeatResponse { SyncRequested = requested };
    }

    /// <summary>
    /// Ueber die Naht angefordert: der Abgleich laeuft nicht hier, sondern
    /// beim Agenten. Hier wird nur das Flag gesetzt, das er beim naechsten
    /// Lebenszeichen abholt - deshalb ohne await.
    /// </summary>
    public Task RefreshCowsAsync(CancellationToken cancellationToken = default)
    {
        RequestSync();
        return Task.CompletedTask;
    }

    /// <summary>Auf dem Server nichts zu tun - die Felder stehen hier schon.</summary>
    public Task LoadStatusAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void RequestSync()
    {
        _syncRequestedAt = _time.GetUtcNow();
        _requestDelivered = false;
    }

    /// <summary>true, solange eine Anforderung noch nicht durch einen Snapshot beantwortet ist (max. 3 min).</summary>
    public bool IsSyncPending
    {
        get
        {
            if (_syncRequestedAt is null)
                return false;

            var now = _time.GetUtcNow();
            if (now - _syncRequestedAt.Value <= SyncRequestTimeout)
                return true;

            // Abgelaufen: der Agent hat geschwiegen, das merkt sich der Status.
            _syncRequestedAt = null;
            _requestDelivered = false;
            LastSyncUtc = now;
            LastSyncSucceeded = false;
            LastSyncError = AgentNoAnswer;
            return false;
        }
    }
}
