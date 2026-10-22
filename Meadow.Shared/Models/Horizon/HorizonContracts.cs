namespace Meadow.Shared.Models.Horizon;

/// <summary>Ein Tier aus Horizon, wie der Agent es liest.</summary>
public sealed class HorizonAnimal
{
    public string AnimalId { get; set; } = "";
    public int CollarNumber { get; set; }
    public string? EarTag { get; set; }
    public bool IsGone { get; set; }
    public string? Location { get; set; }
    public string? Group { get; set; }
    public string? Description { get; set; }
}

public sealed class HorizonSnapshot
{
    public string AgentVersion { get; set; } = "";
    public string MachineName { get; set; } = "";
    public DateTimeOffset ReadAtUtc { get; set; }
    public List<HorizonAnimal> Animals { get; set; } = new();
}

public sealed class AgentHeartbeat
{
    public string AgentVersion { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string? LastError { get; set; }
}

public sealed class AgentHeartbeatResponse
{
    public bool SyncRequested { get; set; }
}

/// <summary>Antwort auf POST /api/horizon/snapshot.</summary>
public sealed class HorizonSyncResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Linked { get; set; }
    public int Promoted { get; set; }
    public int Unchanged { get; set; }
    public int SkippedGone { get; set; }
    public List<string> Warnings { get; set; } = new();
    /// <summary>Gesetzt, wenn die Schutzregel den Snapshot abgelehnt hat; dann wurde nichts geschrieben.</summary>
    public string? RejectedReason { get; set; }
    public bool Accepted => RejectedReason is null;
}
