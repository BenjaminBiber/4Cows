namespace BB_Cow.Class;

/// <summary>
/// Die vier Klauenpositionen. Bisher waren das im ganzen Projekt Magic
/// Strings ("LV" | "LH" | "RV" | "RH") - es gibt sonst kein einziges Enum.
/// </summary>
public enum HoofPosition
{
    LV,
    RV,
    LH,
    RH
}

public static class HoofPositions
{
    /// <summary>Zeichenreihenfolge des 2x2-Gitters: LV, RV, LH, RH.</summary>
    public static readonly HoofPosition[] All =
    {
        HoofPosition.LV, HoofPosition.RV, HoofPosition.LH, HoofPosition.RH
    };

    /// <summary>Ausgeschrieben, fuer Ueberschriften und Bestaetigungstexte.</summary>
    public static string Label(HoofPosition position) => position switch
    {
        HoofPosition.LV => "Links Vorne",
        HoofPosition.RV => "Rechts Vorne",
        HoofPosition.LH => "Links Hinten",
        HoofPosition.RH => "Rechts Hinten",
        _ => ""
    };

    /// <summary>Kurzform unter dem Huf im Selektor (st.hoofLbl).</summary>
    public static string CellLabel(HoofPosition position) => position switch
    {
        HoofPosition.LV => "LV · vorne",
        HoofPosition.RV => "RV · vorne",
        HoofPosition.LH => "LH · hinten",
        HoofPosition.RH => "RH · hinten",
        _ => ""
    };

    /// <summary>Positionscode plus Seite, fuer die Verbaende-Tabelle.</summary>
    public static string SideLabel(HoofPosition position) => position switch
    {
        HoofPosition.LV => "Vorne links",
        HoofPosition.RV => "Vorne rechts",
        HoofPosition.LH => "Hinten links",
        HoofPosition.RH => "Hinten rechts",
        _ => ""
    };
}
