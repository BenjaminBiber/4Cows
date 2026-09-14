using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die Kuhbehandlungen.
///
/// <see cref="SearchCowTreatmentMedicaments"/> nimmt einen zweiten Dienst als
/// Parameter. Der Parameter wird hier auf <see cref="IMedicineService"/>
/// verbreitert statt entfernt: das Entfernen waere eine Signaturaenderung und
/// damit eine Aenderung an jeder Aufrufstelle in den Dialogen.
/// </summary>
public interface ICowTreatmentService
{
    ImmutableDictionary<int, CowTreatment> Treatments { get; }

    ImmutableList<int> DistinctWhereHows { get; }

    Task GetAllDataAsync();

    Task<bool> InsertRangeAsync(IReadOnlyCollection<CowTreatment> treatments);

    Task<CowTreatment> GetByIdAsync(int id);

    Task DeleteDataAsync(int id);

    int[] GetCowTreatmentChartData(int? year = null);

    int[] GetCowTreatmentMedicineChartData(int medicine, int? year = null);

    Task<IEnumerable<string>> SearchCowTreatmentMedicaments(string value, CancellationToken token, IMedicineService medicineService);

    Task<IEnumerable<string>> SearchCowTreatmentWhereHow(string value, CancellationToken token);

    int GetMinYear();
}
