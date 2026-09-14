using System.Collections.Immutable;
using Meadow.Shared.Models;

namespace Meadow.Shared.Services;

/// <summary>
/// Die Naht fuer Wie/Wo.
///
/// GetFullWhereHowName nimmt einen zweiten Dienst als Parameter. Der Parameter
/// wird hier auf IUdderService verbreitert statt entfernt: das Entfernen waere
/// eine Signaturaenderung und damit eine Aenderung an jeder Aufrufstelle.
///
/// GetWhereHowNamesByIds behaelt das grosse Ids seines Parameters. Benannte
/// Argumente an den Aufrufstellen wuerden sonst brechen, und dieser Schritt
/// soll rein additiv sein.
/// </summary>
public interface IWhereHowService
{
    ImmutableDictionary<int, WhereHow> WhereHows { get; }

    List<string> WhereHowNames { get; }

    Task GetAllDataAsync();

    Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync();

    Task<bool> InsertDataAsync(WhereHow whereHow);

    Task<bool> UpdateDataAsync(WhereHow whereHow);

    Task<bool> MergeAsync(int sourceId, int targetId);

    Task<bool> RemoveByIdAsync(int whereHowId);

    WhereHow GetById(int whereHowID);

    string GetWhereHowNameById(int id);

    List<string> GetWhereHowNamesByIds(List<int> Ids);

    Task<int> GetWhereHowIDByName(string name, bool showDialog = true);

    string GetFullWhereHowName(int whereHow_id, IUdderService udderService, int? udder_id = null);

    /// <summary>
    /// Reine Funktion ihres Arguments und trotzdem auf der Naht: es gibt zwei
    /// Aufrufstellen ausserhalb der Klasse, die sie ueber den eingespritzten
    /// Dienst erreichen.
    /// </summary>
    string GetUdderString(Udder udder);
}
