using BB_Cow.Class;
using BB_Cow.Kpi;

namespace BB_Cow.Profile;

/// <summary>
/// Baut aus den vier Behandlungslisten das Profil einer Kuh.
///
/// Statisch, ohne DI, ohne EF, ohne async - wie KpiEvaluator. Das ist kein
/// Stilentscheid: BBCowDataLibrary.Tests referenziert nur diese Bibliothek,
/// alles was in 4Cows-FE liegt waere nicht testbar. Und der Stichtag ist ein
/// Parameter statt DateTime.Today im Rumpf, damit Tests eine feste Gegenwart
/// setzen koennen (siehe KpiTestData.Now).
/// </summary>
public static class CowProfileBuilder
{
    /// <summary>Laenge der beiden Vergleichsfenster in Monaten.</summary>
    public const int WindowMonths = 12;

    /// <param name="cowId">
    /// Die Cow_ID. NICHT die Ohrmarke und nicht die Halsbandnummer: Behandlungen
    /// tragen die Cow_ID in der Spalte Ear_Tag_Number (siehe die Migration
    /// AddCowIdAndIsCalv), Halsbandnummern werden nach einem Abgang neu
    /// vergeben, und Kaelber haben ueberhaupt keine Ohrmarke.
    /// </param>
    /// <param name="udders">
    /// Die Viertel-Kombinationen, direkt aus UdderService.Udder. Unbekannte IDs
    /// zaehlen als "keine Viertelangabe".
    /// </param>
    public static CowProfile Build(
        string cowId,
        IEnumerable<CowTreatment> allCowTreatments,
        IEnumerable<ClawTreatment> allClawTreatments,
        IEnumerable<PlannedCowTreatment> allPlannedCow,
        IEnumerable<PlannedClawTreatment> allPlannedClaw,
        IReadOnlyDictionary<int, Udder> udders,
        DateTime today)
    {
        // Ordinal und nicht OrdinalIgnoreCase: die Cow_ID ist ein
        // Primaerschluessel, kein Anzeigetext. Und Gleichheit, nicht StartsWith
        // oder Contains - Ohrmarken teilen sich lange Praefixe.
        bool Mine(string? id) => string.Equals(id, cowId, StringComparison.Ordinal);

        var cowTreatments = allCowTreatments
            .Where(t => Mine(t.EarTagNumber))
            .OrderByDescending(t => t.AdministrationDate)
            .ToList();

        var clawTreatments = allClawTreatments
            .Where(t => Mine(t.EarTagNumber))
            .OrderByDescending(t => t.TreatmentDate)
            .ToList();

        var plannedCow = allPlannedCow
            .Where(t => Mine(t.EarTagNumber))
            .OrderBy(t => t.AdministrationDate)
            .ToList();

        var plannedClaw = allPlannedClaw
            .Where(t => Mine(t.EarTagNumber))
            .OrderBy(t => t.TreatmentDate)
            .ToList();

        var (last12, previous12, delta, trend) = Windows(cowTreatments, clawTreatments, today);
        var quarters = UdderQuarters(cowTreatments, udders, out var udderTreatments);

        return new CowProfile
        {
            CowId = cowId,
            CowTreatments = cowTreatments,
            ClawTreatments = clawTreatments,
            PlannedCowTreatments = plannedCow,
            PlannedClawTreatments = plannedClaw,
            LastTreatment = LastOf(cowTreatments, clawTreatments),
            Last12Months = last12,
            Previous12Months = previous12,
            DeltaDisplay = delta,
            Trend = trend,
            Months = MonthSeries(cowTreatments, clawTreatments, today),
            Hoofs = Hoofs(clawTreatments),
            Medicines = Tally(cowTreatments.Select(t => t.MedicineId)),
            Reasons = Tally(cowTreatments.Select(t => t.TreatmentReasonId ?? CowProfile.NoReasonId)),
            Findings = Findings(clawTreatments),
            UdderQuarters = quarters,
            UdderTreatments = udderTreatments,
            OpenBandages = clawTreatments.Sum(t => t.OpenBandages().Count()),
            // PlannedClawTreatment kennt kein Erledigt-Kennzeichen - dort
            // loescht der Abschluss die Zeile -, deshalb zaehlen davon alle.
            // IsTreatet ist inzwischen ebenfalls vestigial (auch
            // Planned_Cow_Table.Complete loescht), bleibt aber als Schutz
            // gegen Altbestand stehen.
            OpenPlanned = plannedCow.Count(p => !p.IsTreatet) + plannedClaw.Count
        };
    }

    /// <summary>Ein Profil ohne jede Behandlung - fuer eine Kuh, die es nicht gibt.</summary>
    public static CowProfile Empty(string cowId, DateTime today) => Build(
        cowId,
        Array.Empty<CowTreatment>(),
        Array.Empty<ClawTreatment>(),
        Array.Empty<PlannedCowTreatment>(),
        Array.Empty<PlannedClawTreatment>(),
        new Dictionary<int, Udder>(),
        today);

    // ---- Zeitfenster ----------------------------------------------------

    private static DateTime? LastOf(
        IReadOnlyList<CowTreatment> cow, IReadOnlyList<ClawTreatment> claw)
    {
        // Beide Listen sind absteigend sortiert, das erste Element ist also das
        // juengste. Vordatierte Behandlungen zaehlen mit: "letzte Behandlung"
        // ist der letzte Eintrag, nicht der letzte vergangene.
        DateTime? a = cow.Count > 0 ? cow[0].AdministrationDate : null;
        DateTime? b = claw.Count > 0 ? claw[0].TreatmentDate : null;

        if (a is null) return b;
        if (b is null) return a;
        return a > b ? a : b;
    }

    private static (int Last, int Previous, string? Delta, KpiTrend Trend) Windows(
        IEnumerable<CowTreatment> cow, IEnumerable<ClawTreatment> claw, DateTime today)
    {
        // Halboffene Fenster [von, bis): so kann ein Datum nicht in beiden
        // landen, und die Grenze braucht keine Uhrzeitbetrachtung.
        var end = today.Date.AddDays(1);
        var start = end.AddMonths(-WindowMonths);
        var previousStart = start.AddMonths(-WindowMonths);

        var dates = cow.Select(t => t.AdministrationDate)
            .Concat(claw.Select(t => t.TreatmentDate))
            .Select(d => d.Date)
            .ToList();

        var last = dates.Count(d => d >= start && d < end);
        var previous = dates.Count(d => d >= previousStart && d < start);

        return (last, previous, Delta(last, previous), Trend(last, previous));
    }

    /// <summary>
    /// Absolute Differenz mit Vorzeichen. Kein Vergleich, wenn beide Fenster
    /// leer sind: der Sprung von "noch nie behandelt" auf "zweimal" ist kein
    /// Wachstum, sondern der Anfang der Aufzeichnung.
    /// </summary>
    private static string? Delta(int last, int previous)
    {
        if (last == 0 && previous == 0)
        {
            return null;
        }

        var difference = last - previous;
        return difference switch
        {
            > 0 => $"+{difference}",
            // Echtes Minuszeichen (U+2212), kein Bindestrich: der Bindestrich
            // steht in dieser Anwendung bereits fuer "kein Wert".
            < 0 => $"−{-difference}",
            _ => "±0"
        };
    }

    private static KpiTrend Trend(int last, int previous)
    {
        if (last == 0 && previous == 0) return KpiTrend.None;
        if (last > previous) return KpiTrend.Up;
        if (last < previous) return KpiTrend.Down;
        return KpiTrend.Flat;
    }

    private static IReadOnlyList<CowProfileMonth> MonthSeries(
        IEnumerable<CowTreatment> cow, IEnumerable<ClawTreatment> claw, DateTime today)
    {
        // Rollierend statt Kalenderjahr wie auf dem Dashboard: bei einer
        // einzelnen Kuh waere ein Kalenderjahr im Januar zu elf Zwoelfteln leer.
        var first = new DateTime(today.Year, today.Month, 1).AddMonths(-(WindowMonths - 1));

        var index = new Dictionary<(int Year, int Month), int>(WindowMonths);
        var cowCounts = new int[WindowMonths];
        var clawCounts = new int[WindowMonths];

        for (var i = 0; i < WindowMonths; i++)
        {
            var month = first.AddMonths(i);
            index[(month.Year, month.Month)] = i;
        }

        // Vordatierte Behandlungen liegen ausserhalb und fallen hier heraus, in
        // TotalTreatments sind sie enthalten. Die Balken koennen sich also zu
        // weniger summieren als die Gesamtzahl - das ist gewollt, nicht kaputt.
        foreach (var date in cow.Select(t => t.AdministrationDate))
        {
            if (index.TryGetValue((date.Year, date.Month), out var i)) cowCounts[i]++;
        }

        foreach (var date in claw.Select(t => t.TreatmentDate))
        {
            if (index.TryGetValue((date.Year, date.Month), out var i)) clawCounts[i]++;
        }

        return Enumerable.Range(0, WindowMonths)
            .Select(i =>
            {
                var month = first.AddMonths(i);
                return new CowProfileMonth(month.Year, month.Month, cowCounts[i], clawCounts[i]);
            })
            .ToList();
    }

    // ---- Klauen ---------------------------------------------------------

    private static IReadOnlyList<CowProfileHoof> Hoofs(IReadOnlyList<ClawTreatment> claw)
        => HoofPositions.All
            .Select(position => new CowProfileHoof(
                position,
                claw.Count(t => t.HasData(position)),
                claw.Count(t => t.GetBandage(position)),
                claw.Count(t => t.OpenBandages().Contains(position)),
                // Der Klotz haengt NICHT an IsBandageRemoved - genau diese
                // Vermischung war der Anzeigefehler, den ClawSummary behoben hat.
                claw.Count(t => t.GetBlock(position)),
                Findings(claw, position)))
            .ToList();

    private static IReadOnlyList<CowProfileTally> Findings(
        IReadOnlyList<ClawTreatment> claw, HoofPosition? position = null)
    {
        var positions = position is null ? HoofPositions.All : new[] { position.Value };

        return TallyStrings(claw
            .SelectMany(t => positions.Select(p => t.GetFinding(p)?.Trim()))
            // Ein leerer Befund heisst "an dieser Klaue nichts erfasst". Der
            // Anzeige-Ersatz aus AppSetting (Vorgabe "Pflege") gehoert NUR in
            // die Anzeige: kaeme er hier herein, hiesse der haeufigste
            // Klauenbefund jeder Kuh "Pflege".
            .Where(f => !string.IsNullOrEmpty(f))
            .Select(f => f!));
    }

    // ---- Euterviertel ---------------------------------------------------

    private static IReadOnlyDictionary<HoofPosition, int> UdderQuarters(
        IReadOnlyList<CowTreatment> cow,
        IReadOnlyDictionary<int, Udder> udders,
        out int withQuarter)
    {
        var counts = HoofPositions.All.ToDictionary(p => p, _ => 0);
        withQuarter = 0;

        foreach (var treatment in cow)
        {
            // Drei Faelle fallen hier gemeinsam heraus: die Sentinel-ID
            // int.MinValue ("im Dialog nichts gewaehlt"), die Zeile mit vier
            // false ("kein bestimmtes Viertel") und eine unbekannte ID. Alle
            // drei bedeuten dasselbe - keine Viertelangabe - und keiner davon
            // ist ein Ort, den man zeichnen koennte.
            if (!udders.TryGetValue(treatment.UdderId, out var udder) || !udder.HasAnyQuarter())
            {
                continue;
            }

            withQuarter++;
            foreach (var position in HoofPositions.All)
            {
                if (udder.GetQuarter(position)) counts[position]++;
            }
        }

        return counts;
    }

    // ---- Zaehlen --------------------------------------------------------

    /// <summary>
    /// Absteigend nach Anzahl, bei Gleichstand aufsteigend nach ID. Der
    /// Tie-Break ist nicht Kosmetik: ohne ihn haengt die Kachel "haeufigstes
    /// Medikament" von der Aufzaehlungsreihenfolge ab und wechselt zwischen
    /// zwei Aufrufen.
    /// </summary>
    private static IReadOnlyList<CowProfileIdTally> Tally(IEnumerable<int> ids)
        => ids.GroupBy(id => id)
            .Select(g => new CowProfileIdTally(g.Key, g.Count()))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Id)
            .ToList();

    /// <summary>
    /// Wie <see cref="Tally(System.Collections.Generic.IEnumerable{int})"/>,
    /// aber ueber Text. Gruppiert ohne Ruecksicht auf Gross- und
    /// Kleinschreibung, weil Klauenbefunde frei eingetippt werden -
    /// "Mortellaro" und "mortellaro" sind derselbe Befund. Angezeigt wird die
    /// erste vorkommende Schreibweise, wie in ClawFindingSummary.
    /// </summary>
    private static IReadOnlyList<CowProfileTally> TallyStrings(IEnumerable<string> values)
        => values.GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CowProfileTally(g.First(), g.Count()))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Key, StringComparer.CurrentCulture)
            .ToList();
}
