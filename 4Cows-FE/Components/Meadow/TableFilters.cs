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

/// <summary>Suchtext und Chips der Kuh-Tabelle.</summary>
public sealed class CowTableFilter
{
    public string Search { get; set; } = "";
    public string Medicine { get; set; } = DateRanges.AllLabel;
    public DateRange Range { get; set; } = DateRange.All;

    /// <summary>Nur die Chips zaehlen in das Badge, nicht der Suchtext.</summary>
    public int ActiveCount
        => (Medicine != DateRanges.AllLabel ? 1 : 0) + (Range != DateRange.All ? 1 : 0);

    public bool HasAny => ActiveCount > 0 || !string.IsNullOrWhiteSpace(Search);

    /// <summary>Zuruecksetzen laesst den Suchtext stehen (wie im Prototyp).</summary>
    public void Reset()
    {
        Medicine = DateRanges.AllLabel;
        Range = DateRange.All;
    }
}

/// <summary>Suchtext und Befund-Chip der Klauen-Tabelle.</summary>
public sealed class ClawTableFilter
{
    public const string BandageOption = "Verband";
    public const string BlockOption = "Klotz";

    public string Search { get; set; } = "";
    public string Finding { get; set; } = DateRanges.AllLabel;

    public int ActiveCount => Finding != DateRanges.AllLabel ? 1 : 0;

    public bool HasAny => ActiveCount > 0 || !string.IsNullOrWhiteSpace(Search);

    public void Reset() => Finding = DateRanges.AllLabel;
}
