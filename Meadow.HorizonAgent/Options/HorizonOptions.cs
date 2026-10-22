namespace Meadow.HorizonAgent.Options;

/// <summary>Zugang zur Horizon-Datenbank (Lely) und die Abfrage der Tiere.</summary>
public sealed class HorizonOptions
{
    public const string SectionName = "Horizon";

    public string ConnectionString { get; set; } = "";
    public string AnimalQuery { get; set; } = "";
    public int CommandTimeoutSeconds { get; set; } = 30;
}
