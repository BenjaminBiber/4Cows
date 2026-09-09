namespace BB_Cow.Class;

/// <summary>
/// Positionsbasierter Zugriff auf die vier Booleans von Udder
/// (Quarter_LV, Quarter_RV, Quarter_LH, Quarter_RH).
///
/// Bewusst dasselbe HoofPosition-Enum wie an der Klaue: es sind dieselben
/// vier Buchstaben, WhereHowService.GetUdderString schreibt sie laengst so
/// ("(LV/ RH)"), und ein zweites Enum mit identischen Membern waere nur eine
/// Falle - man kann die beiden dann verwechseln, ohne dass der Compiler
/// meckert. Das Enum heisst historisch "Hoof"; gemeint ist hier das
/// Euterviertel.
/// </summary>
public static class UdderExtensions
{
    public static bool GetQuarter(this Udder u, HoofPosition p) => p switch
    {
        HoofPosition.LV => u.QuarterLV,
        HoofPosition.RV => u.QuarterRV,
        HoofPosition.LH => u.QuarterLH,
        _ => u.QuarterRH
    };

    /// <summary>
    /// Ist ueberhaupt ein Viertel gesetzt? Die Alle-vier-false-Zeile bedeutet
    /// "kein bestimmtes Viertel" und ist damit dasselbe wie gar keine Angabe.
    /// </summary>
    public static bool HasAnyQuarter(this Udder u)
        => u.QuarterLV || u.QuarterRV || u.QuarterLH || u.QuarterRH;
}
