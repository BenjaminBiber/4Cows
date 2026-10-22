using System.Text.Json.Serialization;

namespace Meadow.Shared.Services;

/// <summary>
/// Woher die Kuh-Daten kommen. Als Zeichenkette ueber die Naht, damit eine
/// Antwort lesbar bleibt und ein neuer Wert keine Zahl verschiebt.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CowSyncSource
{
    XLink,
    HorizonAgent
}

/// <summary>
/// Die Naht fuer den Kuh-Abgleich, egal aus welcher Quelle (vormals
/// IXLinkService). Die Eigenschaften haben in der Implementierung einen
/// privaten Setter; auf der Naht steht nur der Getter, weil ein privater
/// Setter kein Schnittstellen-Member ist.
/// </summary>
public interface ICowSyncService
{
    /// <summary>Welche Quelle in diesem Prozess gilt.</summary>
    CowSyncSource Source { get; }

    /// <summary>Zeitpunkt des letzten Abgleichs, null vor dem ersten Lauf.</summary>
    DateTimeOffset? LastSyncUtc { get; }

    bool LastSyncSucceeded { get; }

    /// <summary>Fehlermeldung des letzten fehlgeschlagenen Abgleichs.</summary>
    string? LastSyncError { get; }

    // Nur bei HorizonAgent belegt, sonst null.

    string? AgentVersion { get; }

    string? AgentMachineName { get; }

    DateTimeOffset? LastHeartbeatUtc { get; }

    int? LastSnapshotAnimalCount { get; }

    /// <summary>Was der Agent zuletzt als eigenen Fehler gemeldet hat.</summary>
    string? AgentLastError { get; }

    /// <summary>XLink: Lauf starten. HorizonAgent: Abgleich beim Agenten anfordern.</summary>
    Task RefreshCowsAsync(CancellationToken cancellationToken = default);

    /// <summary>Server: nichts zu tun. Browser: /api/cowsync/status lesen und die Felder setzen.</summary>
    Task LoadStatusAsync(CancellationToken cancellationToken = default);
}
