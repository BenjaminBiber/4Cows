namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer den XLink-Abgleich. Die drei Eigenschaften haben in der
/// Implementierung einen privaten Setter; auf der Naht steht nur der Getter,
/// weil ein privater Setter kein Schnittstellen-Member ist.
/// </summary>
public interface IXLinkService
{
    /// <summary>Zeitpunkt des letzten Abgleichs, null vor dem ersten Lauf.</summary>
    DateTimeOffset? LastSyncUtc { get; }

    bool LastSyncSucceeded { get; }

    /// <summary>Fehlermeldung des letzten fehlgeschlagenen Abgleichs.</summary>
    string? LastSyncError { get; }

    Task RefreshCowsAsync(CancellationToken cancellationToken = default);
}
