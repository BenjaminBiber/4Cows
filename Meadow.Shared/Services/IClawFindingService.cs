using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die Klauenbefunde.
///
/// Der Fehlerwert des Anlegens heisst ClawFinding.FailedId und sitzt am
/// Modell, nicht hier: Konstanten sind keine Schnittstellen-Member, und die
/// eine Aufrufstelle im Klauenbehandlungs-Dialog braucht ihn weiter.
/// </summary>
public interface IClawFindingService
{
    ImmutableDictionary<int, ClawFinding> Findings { get; }

    List<string> FindingNames { get; }

    Task GetAllDataAsync();

    string GetNameById(int? id);

    Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync();

    Task<bool> InsertDataAsync(ClawFinding finding);

    Task<bool> UpdateDataAsync(ClawFinding finding);

    Task<bool> MergeAsync(int sourceId, int targetId, string? survivingName = null);

    Task<bool> RemoveByIdAsync(int findingId);

    Task<int?> GetIdByNameAsync(string? name);

    Task<IEnumerable<string>> SearchAsync(string value, CancellationToken token);
}
