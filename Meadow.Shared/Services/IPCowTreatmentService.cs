using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die geplanten Kuhbehandlungen. <see cref="GetById"/> kann
/// <c>null</c> liefern und steht trotzdem als <c>PlannedCowTreatment</c> -
/// siehe die Begruendung an <see cref="ICowService"/>.
/// </summary>
public interface IPCowTreatmentService
{
    ImmutableDictionary<int, PlannedCowTreatment> Treatments { get; }

    ImmutableList<string> CowMedicineTreatmentList { get; }

    ImmutableList<int> CowWhereHowList { get; }

    Task GetAllDataAsync();

    Task<bool> InsertRangeAsync(IReadOnlyCollection<PlannedCowTreatment> treatments);

    Task<bool> RemoveByIDAsync(int id);

    PlannedCowTreatment GetById(int id);
}
