using Meadow.Shared.Kpi;
using Microsoft.AspNetCore.Components;

namespace Meadow.Client.Components.Ui;

/// <summary>
/// Verbindet eine KPI-Kachel mit der Tabelle dahinter: der Link kommt aus
/// <see cref="KpiDrillDownUrl"/>, hier stehen nur die Adapter, die den Query-String auf die
/// vorhandenen Filterobjekte der Seiten legen.
///
/// Bisher war das Klick-Ziel ein Freitext-Feld an der KPI. Ein Tippfehler fuehrte still ins 404,
/// und die Zielseite wusste nichts von den KPI-Filtern - wer auf "Kuh Behandlungen" klickte,
/// landete auf der ungefilterten Tabelle und musste die Einschraenkung von Hand nachbauen.
///
/// Alle ApplyTo-Ueberladungen MUTIEREN das vorhandene Filterobjekt der Seite, statt eine eigene
/// Abfrage zu bauen. Dadurch leuchtet die ganze Filter-UI von selbst auf - Badge,
/// "Zuruecksetzen", Trefferzaehler und Leerzustand lesen alle dasselbe Objekt - und das Apply()
/// der Seite bleibt unangetastet.
/// </summary>
public static class KpiDrillDown
{
    public static string Href(KpiTileModel tile) => KpiDrillDownUrl.Build(tile);

    public static void ApplyTo(CowTableFilter filter, NavigationManager nav)
    {
        var query = Parse(nav);
        Fill(filter.Medicines, query, KpiTagKeys.Medicine);
        Fill(filter.Cows, query, KpiTagKeys.Cow);
        Fill(filter.Reasons, query, KpiTagKeys.Reason);
        filter.Range = Range(query, filter.Range);
        ReadSearch(query, s => filter.Search = s);
    }

    public static void ApplyTo(ClawTableFilter filter, NavigationManager nav)
    {
        var query = Parse(nav);
        // Befunde UND die synthetischen Zustaende Verband/Klotz liegen in EINER Gruppe - genau wie
        // im Filterpanel der Seite, wo sie mit ODER verknuepft sind.
        Fill(filter.Findings, query, KpiTagKeys.ClawFinding);
        Fill(filter.Cows, query, KpiTagKeys.Cow);
        // Neu, und bis hierher ein echter Fehler: diese Tabelle hatte als einzige der vier keinen
        // Zeitraum, also fiel range= wortlos unter den Tisch. Eine Klauen-Kachel mit "letzte 30
        // Tage" zeigte 4 und oeffnete alle 80 Zeilen.
        filter.Range = Range(query, filter.Range);
        ReadSearch(query, s => filter.Search = s);
    }

    public static void ApplyTo(PlannedCowTableFilter filter, NavigationManager nav)
    {
        var query = Parse(nav);
        Fill(filter.Medicines, query, KpiTagKeys.Medicine);
        Fill(filter.Reasons, query, KpiTagKeys.Reason);
        filter.Found = FlagStates.FromSelection(
            KpiDrillDownUrl.Values(query, KpiTagKeys.Found), FlagStates.FoundLabels);
        filter.Treated = FlagStates.FromSelection(
            KpiDrillDownUrl.Values(query, KpiTagKeys.Treated), FlagStates.TreatedLabels);
        filter.Range = PlannedRange(query, filter.Range);
        ReadSearch(query, s => filter.Search = s);
    }

    public static void ApplyTo(PlannedClawTableFilter filter, NavigationManager nav)
    {
        var query = Parse(nav);
        Fill(filter.Positions, query, KpiTagKeys.ClawPosition);
        filter.Range = PlannedRange(query, filter.Range);
        ReadSearch(query, s => filter.Search = s);
    }

    public static void ApplyTo(CowOverviewFilter filter, NavigationManager nav)
    {
        var query = Parse(nav);
        // Die einzige Filtergruppe dieser Seite. FromKpi darf die Vorgabe
        // "Nur im Bestand" ueberschreiben - ein Drill-down auf "Abgaenge"
        // zeigte sonst garantiert null Zeilen.
        filter.Status = CowStatusFilter.FromKpi(
            KpiDrillDownUrl.Values(query, KpiTagKeys.Calf),
            KpiDrillDownUrl.Values(query, KpiTagKeys.Herd),
            filter.Status);
        ReadSearch(query, s => filter.Search = s);
    }

    // ---- Hilfsfunktionen -----------------------------------------------

    private static IReadOnlyDictionary<string, string[]> Parse(NavigationManager nav)
        => KpiDrillDownUrl.Parse(new Uri(nav.Uri).Query);

    // Ueber die Tabelle in KpiTimeframes und nicht als eigener switch: hier stand
    // "7 => Days7, 30 => Days30, _ => unveraendert", und ein range=90 aenderte damit still gar
    // nichts. Kommt kein oder ein unbekannter Zeitraum an, bleibt stehen, was die Seite hatte.
    private static DateRange Range(IReadOnlyDictionary<string, string[]> query, DateRange current)
        => KpiTimeframes.FromDays(KpiDrillDownUrl.Range(query)) is { } timeframe
            ? DateRanges.FromTimeframe(timeframe)
            : current;

    private static PlannedDateRange PlannedRange(
        IReadOnlyDictionary<string, string[]> query, PlannedDateRange current)
        => KpiTimeframes.FromDays(KpiDrillDownUrl.Range(query)) is { } timeframe
            ? PlannedDateRanges.FromTimeframe(timeframe)
            : current;

    private static void Fill(
        HashSet<string> target, IReadOnlyDictionary<string, string[]> query, string key)
    {
        if (KpiDrillDownUrl.Values(query, key) is not { } values)
        {
            return;
        }

        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static void ReadSearch(IReadOnlyDictionary<string, string[]> query, Action<string> set)
    {
        if (KpiDrillDownUrl.Values(query, KpiDrillDownUrl.SearchKey) is { Count: > 0 } values)
        {
            set(values[0]);
        }
    }
}
