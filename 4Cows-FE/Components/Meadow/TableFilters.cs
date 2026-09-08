using BB_Cow.Kpi;

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
    /// Halsbandnummern. Gebraucht fuer den Drill-down der Kachel "Kuh mit den
    /// meisten Behandlungen": ueber den Suchtext war das zu unscharf, weil er
    /// als Teilstring auch in Ohrmarken trifft ("104" steckt in "...1042") und
    /// die Tabelle dann fremde Tiere mitzeigte.
    /// </summary>
    public HashSet<string> Cows { get; } = new(StringComparer.OrdinalIgnoreCase);

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
        => (Medicines.Count > 0 ? 1 : 0)
           + (Cows.Count > 0 ? 1 : 0)
           + (Range != DateRange.All ? 1 : 0);

    public bool HasAny => ActiveCount > 0 || !string.IsNullOrWhiteSpace(Search);

    /// <summary>Zuruecksetzen laesst den Suchtext stehen (wie im Prototyp).</summary>
    public void Reset()
    {
        Medicines.Clear();
        Cows.Clear();
        Range = DateRange.All;
    }
}

/// <summary>Suchtext und Befund-Filter der Klauen-Tabelle.</summary>
public sealed class ClawTableFilter
{
    // Aus KpiFlags, nicht als eigene Literale: eine KPI-Kachel verlinkt mit
    // genau diesen Werten hierher. Zwei getrennte Konstanten wuerden
    // auseinanderdriften, sobald eine davon umbenannt wird.
    public const string BandageOption = KpiFlags.Bandage;
    public const string BlockOption = KpiFlags.Block;

    public string Search { get; set; } = "";

    /// <summary>
    /// Mehrfachauswahl aus Befunden plus den beiden Zustaenden Verband und
    /// Klotz. Leer heisst "Alle". ODER innerhalb der Gruppe, damit sich
    /// "Mortellaro oder Sohlengeschwuer" in einem Durchgang zeigen laesst -
    /// mit der alten Einfachauswahl brauchte das zwei Durchgaenge.
    /// </summary>
    public HashSet<string> Findings { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Halsbandnummern - siehe <see cref="CowTableFilter.Cows"/>.</summary>
    public HashSet<string> Cows { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int ActiveCount => (Findings.Count > 0 ? 1 : 0) + (Cows.Count > 0 ? 1 : 0);

    public bool HasAny => ActiveCount > 0 || !string.IsNullOrWhiteSpace(Search);

    public void Reset()
    {
        Findings.Clear();
        Cows.Clear();
    }
}

/// <summary>
/// Zeitraum-Auswahl der GEPLANTEN Tabellen - bewusst in die andere Richtung
/// als <see cref="DateRange"/>.
///
/// DateRanges.Matches schliesst Zukunftsdaten aus, weil die Dialoge
/// vordatieren koennen und eine vordatierte Behandlung nichts in "letzte 7
/// Tage" zu suchen hat. Geplante Behandlungen sind aber ABSICHTLICH
/// zukunftsdatiert: derselbe Filter wuerde dort schlicht alles wegfiltern.
/// Fuer sie heisst "7 Tage" also "die naechsten 7 Tage".
///
/// Der Evaluator wendet fuer die geplanten Quellen dieselbe Regel an
/// (KpiSourceInfo.IsPlanned) - sonst zeigte die Tabelle eine andere Menge
/// als die Kachel, aus der man sie geoeffnet hat.
/// </summary>
public enum PlannedDateRange
{
    All,
    Next7,
    Next30
}

public static class PlannedDateRanges
{
    public const string AllLabel = "Alle";
    public const string Next7Label = "Nächste 7 Tage";
    public const string Next30Label = "Nächste 30 Tage";

    public static readonly string[] Labels = { AllLabel, Next7Label, Next30Label };

    public static PlannedDateRange Parse(string label) => label switch
    {
        Next7Label => PlannedDateRange.Next7,
        Next30Label => PlannedDateRange.Next30,
        _ => PlannedDateRange.All
    };

    public static string ToLabel(PlannedDateRange range) => range switch
    {
        PlannedDateRange.Next7 => Next7Label,
        PlannedDateRange.Next30 => Next30Label,
        _ => AllLabel
    };

    public static bool Matches(PlannedDateRange range, DateTime date)
    {
        if (range == PlannedDateRange.All)
        {
            return true;
        }

        var today = DateTime.Today;
        var days = range == PlannedDateRange.Next7 ? 7 : 30;
        return date.Date >= today && date.Date <= today.AddDays(days);
    }
}

/// <summary>
/// Dreiwertige Ja/Nein-Auswahl fuer die Booleans der geplanten Tabellen.
///
/// Bewusst eine Einfachauswahl und keine Mehrfachauswahl mit zwei Haken:
/// bei zwei Optionen ist ein Dropdown mit "Alle / Gefunden / Nicht gefunden"
/// verstaendlicher als zwei Checkboxen, deren gleichzeitige Auswahl dasselbe
/// wie "Alle" bedeutet. Die Werte sind die aus <see cref="KpiFlags"/>, damit
/// eine KPI-Kachel direkt hierher verlinken kann.
/// </summary>
public static class FlagStates
{
    public const string Any = "Alle";

    public static readonly string[] FoundLabels = { Any, KpiFlags.Found, KpiFlags.NotFound };
    public static readonly string[] TreatedLabels = { Any, KpiFlags.Treated, KpiFlags.NotTreated };

    /// <summary>Der einzige gewaehlte Wert einer KPI-Filtergruppe, sonst "Alle".</summary>
    public static string FromSelection(IReadOnlyList<string>? values, string[] allowed)
        => values is { Count: 1 } && allowed.Contains(values[0], StringComparer.OrdinalIgnoreCase)
            ? allowed.First(a => string.Equals(a, values[0], StringComparison.OrdinalIgnoreCase))
            : Any;

    public static bool Matches(string state, bool actual, string yes)
        => state == Any || (string.Equals(state, yes, StringComparison.OrdinalIgnoreCase) == actual);
}

/// <summary>Suchtext und Filter der Tabelle geplanter Kuh-Behandlungen.</summary>
public sealed class PlannedCowTableFilter
{
    public string Search { get; set; } = "";

    /// <summary>Leer heisst "Alle", ODER innerhalb der Gruppe - wie CowTableFilter.</summary>
    public HashSet<string> Medicines { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Getrennte Gruppen, damit sie sich UND-verknuepfen: "gefunden, aber
    /// noch nicht behandelt" ist der Hauptzweck dieser Tabelle.</summary>
    public string Found { get; set; } = FlagStates.Any;

    public string Treated { get; set; } = FlagStates.Any;

    public PlannedDateRange Range { get; set; } = PlannedDateRange.All;

    public int ActiveCount
        => (Medicines.Count > 0 ? 1 : 0)
           + (Found != FlagStates.Any ? 1 : 0)
           + (Treated != FlagStates.Any ? 1 : 0)
           + (Range != PlannedDateRange.All ? 1 : 0);

    public bool HasAny => ActiveCount > 0 || !string.IsNullOrWhiteSpace(Search);

    public void Reset()
    {
        Medicines.Clear();
        Found = FlagStates.Any;
        Treated = FlagStates.Any;
        Range = PlannedDateRange.All;
    }
}

/// <summary>Suchtext und Filter der Tabelle geplanter Klauen-Behandlungen.</summary>
public sealed class PlannedClawTableFilter
{
    public string Search { get; set; } = "";

    /// <summary>
    /// Klauenpositionen (LV/RV/LH/RH). Planned_Claw_Treatment speichert die
    /// Befunde als vier Booleans, nicht als Namen - die Position ist also
    /// alles, was diese Tabelle ueber einen Befund weiss.
    /// </summary>
    public HashSet<string> Positions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public PlannedDateRange Range { get; set; } = PlannedDateRange.All;

    public int ActiveCount
        => (Positions.Count > 0 ? 1 : 0) + (Range != PlannedDateRange.All ? 1 : 0);

    public bool HasAny => ActiveCount > 0 || !string.IsNullOrWhiteSpace(Search);

    public void Reset()
    {
        Positions.Clear();
        Range = PlannedDateRange.All;
    }
}
