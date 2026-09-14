using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die Behandlungsgruende.
///
/// Die Konstante NoReasonText steht NICHT hier: Konstanten sind keine
/// Schnittstellen-Member, und sie hat ausserhalb ihrer Klasse keine
/// Aufrufstelle. Sie bleibt deshalb am Dienst - anders als
/// ClawFinding.FailedId, die genau deswegen ans Modell gewandert ist.
/// </summary>
public interface ITreatmentReasonService
{
    ImmutableDictionary<int, TreatmentReason> Reasons { get; }

    List<string> ReasonNames { get; }

    Task GetAllDataAsync();

    Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync();

    Task<bool> InsertDataAsync(TreatmentReason reason);

    Task<bool> UpdateDataAsync(TreatmentReason reason);

    Task<bool> MergeAsync(int sourceId, int targetId);

    Task<bool> RemoveByIdAsync(int reasonId);

    string GetNameById(int? id);

    Task<int> GetIdByNameAsync(string? name);

    Task<IEnumerable<string>> SearchAsync(string value, CancellationToken token);
}
