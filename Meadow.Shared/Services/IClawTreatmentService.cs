using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die Klauenbehandlungen. Signaturen woertlich aus der heutigen
/// Implementierung, inklusive des grossen ID in <see cref="GetByIDAsync"/>.
/// </summary>
public interface IClawTreatmentService
{
    ImmutableDictionary<int, ClawTreatment> Treatments { get; }

    Task GetAllDataAsync();

    Task<bool> InsertDataAsync(ClawTreatment clawTreatment);

    Task<ClawTreatment> GetByIDAsync(int id);

    Task<bool> UpdateDataAsync(ClawTreatment clawTreatment);

    Task<bool> RemoveBandageAsync(int id);

    Task<int> RemoveBandagesAsync(IReadOnlyCollection<int> ids);

    Task DeleteDataAsync(int id);

    int[] GetClawTreatmentChartData(int? year = null);

    List<ClawTreatment> GetClawTreatments();

    List<ClawTreatment> GetClawTreatmentsWithBandage();
}
