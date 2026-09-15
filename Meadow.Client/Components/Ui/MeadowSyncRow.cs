using Meadow.Client.Services;

namespace Meadow.Client.Components.Ui;

/// <summary>
/// Die Zeilenklasse zum Uebertragungszustand.
///
/// Eigene Klasse und keine Methode in den vier Tabellen: die Zuordnung
/// Zustand -> CSS-Klasse viermal zu schreiben hiesse, sie dreimal falsch
/// nachziehen zu koennen. MeadowTable bleibt davon unberuehrt - RowClass gibt
/// es dort laengst.
/// </summary>
public static class MeadowSyncRow
{
    /// <summary>
    /// <c>null</c> fuer synchronisierte Zeilen, und zwar absichtlich: RowClass
    /// setzt dann gar kein class-Attribut, und die Zeile ist byte-identisch zu
    /// der von vorher.
    /// </summary>
    public static string? Class(MeadowRowState state) => state switch
    {
        MeadowRowState.Pending => "mw-row--warten",
        MeadowRowState.Failed => "mw-row--fehler",
        _ => null
    };
}
