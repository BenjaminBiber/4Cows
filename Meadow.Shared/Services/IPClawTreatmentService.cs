using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die geplanten Klauenbehandlungen. <see cref="GetById"/> kann
/// <c>null</c> liefern und steht trotzdem als <c>PlannedClawTreatment</c> -
/// siehe die Begruendung an <see cref="ICowService"/>.
/// </summary>
public interface IPClawTreatmentService
{
    ImmutableDictionary<int, PlannedClawTreatment> Treatments { get; }

    Task GetAllDataAsync();

    Task<bool> InsertDataAsync(PlannedClawTreatment clawTreatment);

    Task<bool> RemoveByIDAsync(int id);

    PlannedClawTreatment GetById(int id);
}
