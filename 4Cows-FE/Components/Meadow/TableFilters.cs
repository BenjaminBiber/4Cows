namespace _4Cows_FE.Components.Meadow;

/// <summary>
/// Zeitraum-Chips der Kuh-Tabelle.
///
/// Bewusst MIT Obergrenze: der Prototyp filtert nur "hoechstens N Tage alt",
/// wodurch zukunftsdatierte Behandlungen in "7 Tage" auftauchten. Die
/// Dialoge koennen vordatieren, also gehoeren Zukunftszeilen nur unter "Alle".
/// </summary>
public enum DateRange
{
    All,
    Days7,
    Days30
}

public static class DateRanges
{
    public const string AllLabel = "Alle";
    public const string Days7Label = "7 Tage";
    public const string Days30Label = "30 Tage";

    public static readonly string[] Labels = { AllLabel, Days7Label, Days30Label };

    public static DateRange Parse(string label) => label switch
    {
        Days7Label => DateRange.Days7,
        Days30Label => DateRange.Days30,
        _ => DateRange.All
    };

    public static string ToLabel(DateRange range) => range switch
    {
        DateRange.Days7 => Days7Label,
        DateRange.Days30 => Days30Label,
        _ => AllLabel
    };

    public static bool Matches(DateRange range, DateTime date)
    {
        if (range == DateRange.All)
        {
            return true;
        }

        var today = DateTime.Today;
        var days = range == DateRange.Days7 ? 7 : 30;
        return date.Date >= today.AddDays(-days) && date.Date <= today;
    }
}

/// <summary>Suchtext und Filter der Kuh-Tabelle.</summary>
public sealed class CowTableFilter
{
    public string Search { get; set; } = "";

    /// <summary>
    /// Mehrfachauswahl. Leer heisst "Alle", also kein Filter - vorher war
    /// "Alle" ein Sentinel-Wert im Feld selbst. Innerhalb der Gruppe gilt
    /// ODER: eine Zeile passt, wenn ihr Medikament EINES der gewaehlten ist.
    /// OrdinalIgnoreCase, damit der Vergleich sich wie das fruehere
    /// string.Equals(..., OrdinalIgnoreCase) verhaelt.
    /// </summary>
    public HashSet<string> Medicines { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Einfachauswahl: die Zeitraeume schliessen sich gegenseitig aus, "7 Tage"
    /// UND "30 Tage" waere dasselbe wie "30 Tage".
    /// </summary>
    public DateRange Range { get; set; } = DateRange.All;

    /// <summary>
    /// Nur die Filter zaehlen in das Badge, nicht der Suchtext. Gezaehlt werden
    /// aktive Gruppen, nicht einzelne Werte - das Badge beantwortet "wie viele
    /// Filter engen die Liste ein", nicht "wie viele Haken sind gesetzt".
    /// </summary>
    public int ActiveCount
        => (Medicines.Count > 0 ? 1 : 0) + (Range != DateRange.All ? 1 : 0);

    public bool HasAny => ActiveCount > 0 || !string.IsNullOrWhiteSpace(Search);

    /// <summary>Zuruecksetzen laesst den Suchtext stehen (wie im Prototyp).</summary>
    public void Reset()
    {
        Medicines.Clear();
        Range = DateRange.All;
    }
}

/// <summary>Suchtext und Befund-Filter der Klauen-Tabelle.</summary>
public sealed class ClawTableFilter
{
    public const string BandageOption = "Verband";
    public const string BlockOption = "Klotz";

    public string Search { get; set; } = "";

    /// <summary>
    /// Mehrfachauswahl aus Befunden plus den beiden Zustaenden Verband und
    /// Klotz. Leer heisst "Alle". ODER innerhalb der Gruppe, damit sich
    /// "Mortellaro oder Sohlengeschwuer" in einem Durchgang zeigen laesst -
    /// mit der alten Einfachauswahl brauchte das zwei Durchgaenge.
    /// </summary>
    public HashSet<string> Findings { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int ActiveCount => Findings.Count > 0 ? 1 : 0;

    public bool HasAny => ActiveCount > 0 || !string.IsNullOrWhiteSpace(Search);

    public void Reset() => Findings.Clear();
}
