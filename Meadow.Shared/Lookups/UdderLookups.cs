using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Die reinen Nachschlageregeln ueber den Euterviertel-Cache. Rumpf hier,
/// delegierende Zeile in UdderService.
/// </summary>
public static class UdderLookups
{
    /// <summary>
    /// Ob die ID fuer mindestens ein Viertel steht.
    ///
    /// Unbekannte IDs zaehlen als keines - und dazu gehoert int.MinValue,
    /// also "im Dialog noch nichts gewaehlt". Genau das unterscheidet
    /// "noch nichts gewaehlt" von der Zeile fuer "kein bestimmtes Viertel"
    /// nicht, und muss es auch nicht: beides ist keine Viertel-Angabe.
    /// </summary>
    public static bool HasAnyQuarter(IReadOnlyDictionary<int, Udder> udders, int id)
    {
        var udder = GetById(udders, id);
        return udder.QuarterLV || udder.QuarterRV || udder.QuarterLH || udder.QuarterRH;
    }

    /// <summary>
    /// Bei Fehltreffer ein frisches, leeres Udder - also UdderId int.MinValue
    /// und alle vier Viertel false. Kein null: die Aufrufstellen lesen die
    /// Viertel direkt, und "unbekannte ID" heisst hier "keine Viertel".
    /// </summary>
    public static Udder GetById(IReadOnlyDictionary<int, Udder> udders, int id)
    {
        return udders.ContainsKey(id) ? udders[id] : new Udder();
    }
}
