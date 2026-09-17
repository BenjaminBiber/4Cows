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

    /// <summary>
    /// <paramref name="survivingName"/> setzt den Namen des Ziels - damit kann
    /// im Zusammenfuehren-Dialog die Schreibweise der Quelle gewinnen. Optional
    /// und nachtraeglich ergaenzt: der Aufruf ohne den Parameter
    /// (EditWhereHowDialog beim Umbenennen auf einen vergebenen Namen) laeuft
    /// unveraendert weiter.
    /// </summary>
    Task<bool> MergeAsync(int sourceId, int targetId, string? survivingName = null);

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
