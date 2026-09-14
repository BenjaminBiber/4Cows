using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer die Euterviertel. Die Eigenschaft heisst wie ihr Elementtyp -
/// woertlich uebernommen, damit sich an den Aufrufstellen nichts aendert.
/// </summary>
public interface IUdderService
{
    ImmutableDictionary<int, Udder> Udder { get; }

    Task GetAllDataAsync();

    Task<bool> InsertDataAsync(Udder udder);

    Task<int> GetIDByBools(Udder emptyUdder);

    Task<int> GetIdForNoQuarters();

    bool HasAnyQuarter(int id);

    Udder GetById(int id);
}
