namespace _4Cows_FE.Components.Meadow;

/// <summary>
/// Beschriftung einer rollenden Monatsreihe.
///
/// Die Namen stehen fest verdrahtet auf Deutsch da und kommen NICHT aus
/// ToString("MMMM"): Program.cs legt keine Kultur fest, auf einem
/// Docker-Host stuende dort "October". Index.razor macht es aus demselben
/// Grund seit jeher genauso.
/// </summary>
public static class MeadowMonthAxis
{
    private static readonly string[] Initials =
        { "J", "F", "M", "A", "M", "J", "J", "A", "S", "O", "N", "D" };

    private static readonly string[] Names =
    {
        "Januar", "Februar", "März", "April", "Mai", "Juni",
        "Juli", "August", "September", "Oktober", "November", "Dezember"
    };

    /// <summary>
    /// Nur der Anfangsbuchstabe, wie auf dem Dashboard. Nicht aus Treue zum
    /// Entwurf: zwoelf Balken teilen sich auf einem 360px-Telefon rund 284px
    /// Zeichenflaeche, ein "S 25" braeuchte in 10px etwa 22px gegen 23px
    /// Teilung - Chart.js begaenne dann still, Beschriftungen auszulassen.
    /// Eine Achse, die neun von zwoelf Monaten zeigt, ist schlechter als eine
    /// mit Initialen. Das Jahr steht dafuer im Tooltip und im Kartentitel.
    /// </summary>
    public static string[] TickLabels(DateTime start, int count = 12)
        => Series(start, count).Select(m => Initials[m.Month - 1]).ToArray();

    /// <summary>Voller Monat mit Jahr - das dritte Argument von meadowChart.render.</summary>
    public static string[] TooltipTitles(DateTime start, int count = 12)
        => Series(start, count).Select(m => $"{Names[m.Month - 1]} {m.Year}").ToArray();

    /// <summary>
    /// "10.2025 – 09.2026" fuer den Kartentitel. Numerisch und damit
    /// kulturunabhaengig; ein rollendes Fenster ueberschreitet den
    /// Jahreswechsel, das Jahr muss also irgendwo sichtbar sein.
    /// </summary>
    public static string RangeCaption(DateTime start, int count = 12)
    {
        var last = start.AddMonths(count - 1);
        return $"{start.Month:00}.{start.Year} – {last.Month:00}.{last.Year}";
    }

    private static IEnumerable<DateTime> Series(DateTime start, int count)
    {
        var first = new DateTime(start.Year, start.Month, 1);
        for (var i = 0; i < count; i++)
        {
            yield return first.AddMonths(i);
        }
    }
}
