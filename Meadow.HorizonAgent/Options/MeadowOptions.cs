namespace Meadow.HorizonAgent.Options;

/// <summary>Zugang zur Meadow-API, an die der Agent Snapshots und Herzschlaege schickt.</summary>
public sealed class MeadowOptions
{
    public const string SectionName = "Meadow";

    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int SnapshotIntervalMinutes { get; set; } = 15;
    public int HeartbeatSeconds { get; set; } = 60;
}
