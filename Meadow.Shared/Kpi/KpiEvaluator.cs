using System.Globalization;
using BB_Cow.Class;

namespace BB_Cow.Kpi;

/// <summary>
/// Turns a declarative KPI definition into a value.
///
/// Deliberately static, synchronous, and free of DI and EF: given a definition, a source descriptor
/// and a list of rows it is a pure function. That is what makes it the one genuinely testable seam
/// in this feature - a unit test hands it a hand-built List of KpiRow and no database is involved.
/// </summary>
public static class KpiEvaluator
{
    /// <param name="rows">ALL rows of the source. Timeframe and filters are applied here, because
    /// distinguishing "no rows in this period" from "this filter value no longer exists" needs both
    /// the windowed and the unwindowed set.</param>
    /// <param name="now">Injected rather than read from the clock, so timeframe boundaries are testable.</param>
    public static KpiResult Evaluate(
        KpiDefinition definition,
        KpiSourceInfo source,
        IReadOnlyList<KpiRow> rows,
        DateTime now)
    {
        var invalid = Validate(definition, source);
        if (invalid is not null)
        {
            return invalid;
        }

        var current = Select(definition, source, rows, now, periodsBack: 0);

        var result = definition.Measure switch
        {
            KpiMeasure.Count => FromCount(definition, current.Count),
            KpiMeasure.CountDistinctCows => FromCount(definition, DistinctCows(current)),
            KpiMeasure.SumDosage => FromDosage(definition, current, average: false),
            KpiMeasure.AvgDosage => FromDosage(definition, current, average: true),
            KpiMeasure.TopValue => FromTop(definition, current),
            _ => KpiResult.Failed($"Unbekannte Kennzahl: {definition.Measure}.")
        };

        result = result with { MatchedRows = current.Count };
        result = WithComparison(result, definition, source, rows, now);

        return result with { Message = result.Message ?? StaleFilterHint(definition, rows) };
    }

    // ---- Validation ----------------------------------------------------

    /// <summary>
    /// Rejects definitions the source cannot compute, instead of silently producing a number that
    /// looks fine. Every one of these is reachable by hand-editing the JSON or by a source losing a
    /// capability, so none of them may fall through to a default.
    /// </summary>
    private static KpiResult? Validate(KpiDefinition definition, KpiSourceInfo source)
    {
        if (!source.Supports(definition.Measure))
        {
            return KpiResult.Failed($"„{source.Label}“ kennt die Kennzahl „{definition.Measure}“ nicht.");
        }

        if (definition.RequiresDosage && !source.HasDosage)
        {
            return KpiResult.Failed($"„{source.Label}“ hat kein Mengenfeld zum Summieren.");
        }

        if (definition.Timeframe != KpiTimeframe.All && !source.SupportsTimeframe)
        {
            return KpiResult.Failed($"„{source.Label}“ hat keine Datumsspalte, ein Zeitraum ist nicht möglich.");
        }

        if (definition.RequiresGroupBy)
        {
            if (KpiTagKeys.ForGroupBy(definition.GroupBy) is null)
            {
                return KpiResult.Failed("Für „Häufigster Wert“ fehlt die Gruppierung.");
            }

            if (!source.Supports(definition.GroupBy))
            {
                return KpiResult.Failed(
                    $"„{source.Label}“ lässt sich nicht nach „{definition.GroupBy}“ gruppieren.");
            }
        }

        return null;
    }

    // ---- Row selection -------------------------------------------------

    private static List<KpiRow> Select(
        KpiDefinition definition,
        KpiSourceInfo source,
        IReadOnlyList<KpiRow> rows,
        DateTime now,
        int periodsBack)
    {
        IEnumerable<KpiRow> query = rows;

        var window = Window(definition.Timeframe, source.IsPlanned, now, periodsBack);
        if (window is not null)
        {
            var (from, to) = window.Value;
            query = query.Where(r => r.Date.HasValue && r.Date.Value.Date >= from && r.Date.Value.Date <= to);
        }

        foreach (var (key, values) in definition.Filters)
        {
            // Empty or missing means "all", exactly as MeadowMultiSelect renders it. Groups are
            // AND-ed, values inside a group OR-ed.
            if (values is null || values.Count == 0)
            {
                continue;
            }

            var selected = values;
            query = query.Where(r =>
                r.TagValues(key).Any(v => selected.Contains(v, StringComparer.OrdinalIgnoreCase)));
        }

        return query.ToList();
    }

    /// <summary>
    /// The date window, or null for <see cref="KpiTimeframe.All"/>.
    ///
    /// For recorded treatments this reproduces DateRanges.Matches exactly - inclusive at both ends
    /// and EXCLUDING the future, because the dialogs can post-date and a future row has no business
    /// in "last 7 days". The tile and the table it drills into must agree, so this is a copy of that
    /// rule, not an approximation of it.
    ///
    /// For planned treatments the window points the other way: they are deliberately future-dated,
    /// so "30 days" means the next 30 days. Looking backwards there would return nothing at all.
    /// </summary>
    private static (DateTime From, DateTime To)? Window(
        KpiTimeframe timeframe, bool planned, DateTime now, int periodsBack)
    {
        if (timeframe == KpiTimeframe.All)
        {
            return null;
        }

        var today = now.Date;
        var days = timeframe == KpiTimeframe.Days7 ? 7 : 30;

        // The window is inclusive at both ends, so it spans days + 1 calendar days. The previous
        // period must be shifted by that full length, not by "days" - otherwise the two windows
        // overlap on one day and a row gets counted twice.
        var shift = periodsBack * (days + 1);

        return planned
            ? (today.AddDays(shift), today.AddDays(days + shift))
            : (today.AddDays(-days - shift), today.AddDays(-shift));
    }

    private static int DistinctCows(IEnumerable<KpiRow> rows)
        => rows.Select(r => r.CowId).Distinct(StringComparer.Ordinal).Count();

    // ---- Measures ------------------------------------------------------

    private static KpiResult FromCount(KpiDefinition definition, int count) => new()
    {
        State = count == 0 ? KpiResultState.Empty : KpiResultState.Ok,
        Display = WithUnit(count.ToString("N0", CultureInfo.CurrentCulture), definition.Unit),
        Number = count
    };

    private static KpiResult FromDosage(KpiDefinition definition, List<KpiRow> rows, bool average)
    {
        var values = rows.Where(r => r.Dosage.HasValue).Select(r => r.Dosage!.Value).ToList();

        if (values.Count == 0)
        {
            // An average over nothing is not 0, it is undefined - so it must not be printed as a
            // number. A sum over nothing legitimately is 0.
            return new KpiResult
            {
                State = KpiResultState.Empty,
                Display = average ? "–" : WithUnit(Format(0, definition.Decimals), definition.Unit),
                Number = average ? null : 0
            };
        }

        var value = average ? values.Average() : values.Sum();

        // Seit die Dosiereinheit am Medikament haengt, koennen hier ml und
        // Stueck zusammenfallen. Der Wert wird weiter berechnet - er ist ja
        // angefordert - aber er bekommt keine Einheit angeheftet, die nur fuer
        // einen Teil der Zeilen gilt, und die Kachel sagt, dass gemischt wurde.
        // Ohne das waere "3 Tabletten + 20 ml = 23 ml" eine voellig plausibel
        // aussehende Falschaussage.
        var units = rows
            .Where(r => r.Dosage.HasValue && !string.IsNullOrWhiteSpace(r.DosageUnit))
            .Select(r => r.DosageUnit!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (units.Count > 1)
        {
            return new KpiResult
            {
                State = KpiResultState.Ok,
                Display = $"{Format(value, definition.Decimals)} (gemischte Einheiten)",
                Number = value,
                Message = $"Die Behandlungen dieser Kennzahl verwenden {units.Count} verschiedene "
                          + $"Dosiereinheiten ({string.Join(", ", units)}). "
                          + (average ? "Der Durchschnitt" : "Die Summe")
                          + " darüber ist nicht aussagekräftig — bitte nach Medikament gruppieren "
                          + "oder auf ein Medikament filtern."
            };
        }

        return new KpiResult
        {
            State = KpiResultState.Ok,
            // Ist am Medikament genau eine Einheit hinterlegt, gewinnt die -
            // sie ist naeher an den Daten als das Textfeld der Definition, das
            // jemand vor der Einfuehrung der Einheiten getippt hat.
            Display = WithUnit(Format(value, definition.Decimals),
                units.Count == 1 ? units[0] : definition.Unit),
            Number = value
        };
    }

    private readonly record struct Ranked(string Label, int Count);

    private static KpiResult FromTop(KpiDefinition definition, List<KpiRow> rows)
    {
        var groups = Group(definition, rows)
            .Where(g => !string.IsNullOrWhiteSpace(g.Label))
            .OrderByDescending(g => g.Count)
            // The tiebreaker the old SQL never had: "ORDER BY COUNT(*) DESC LIMIT 1" leaves the
            // winner arbitrary on a tie, so the tile could change between two renders on unchanged
            // data. Ordinal on the label makes it deterministic.
            .ThenBy(g => g.Label, StringComparer.Ordinal)
            .ToList();

        if (groups.Count == 0)
        {
            return new KpiResult { State = KpiResultState.Empty, Display = "–" };
        }

        return new KpiResult
        {
            State = KpiResultState.Ok,
            Display = groups[0].Label,
            Label = groups[0].Label,
            Number = groups[0].Count
        };
    }

    private static IEnumerable<Ranked> Group(KpiDefinition definition, List<KpiRow> rows)
    {
        // Ranking cows groups by the ANIMAL and only displays its collar number, which is what the
        // old query did with "GROUP BY ct.Ear_Tag_Number" while selecting Collar_Number. Grouping by
        // the collar instead would silently merge two animals whenever a number is re-issued - and
        // re-issuing is expected, since CowService.IsCollarInUse only reserves numbers of cows that
        // have not left the herd.
        if (definition.GroupBy == KpiGroupBy.Cow)
        {
            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.CowId))
                .GroupBy(r => r.CowId, StringComparer.Ordinal)
                .Select(g => new Ranked(g.First().CowLabel, g.Count()));
        }

        var key = KpiTagKeys.ForGroupBy(definition.GroupBy)!;

        return rows
            .SelectMany(r => r.TagValues(key))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Ranked(g.Key, g.Count()));
    }

    // ---- Comparison ----------------------------------------------------

    private static KpiResult WithComparison(
        KpiResult result,
        KpiDefinition definition,
        KpiSourceInfo source,
        IReadOnlyList<KpiRow> rows,
        DateTime now)
    {
        // Silently skipped rather than an error: a definition can legitimately keep the flag set
        // while the author switches to "all time" or to a ranking, and losing the trend line is the
        // correct outcome, not a broken tile.
        if (!definition.CompareToPrevious
            || !definition.AllowsComparison
            || !source.SupportsCompare
            || result.State == KpiResultState.Error
            || result.Number is null)
        {
            return result;
        }

        var previousRows = Select(definition, source, rows, now, periodsBack: 1);

        double previous = definition.Measure switch
        {
            KpiMeasure.Count => previousRows.Count,
            KpiMeasure.CountDistinctCows => DistinctCows(previousRows),
            KpiMeasure.SumDosage => previousRows.Where(r => r.Dosage.HasValue).Sum(r => r.Dosage!.Value),
            KpiMeasure.AvgDosage => previousRows.Where(r => r.Dosage.HasValue)
                .Select(r => r.Dosage!.Value)
                .DefaultIfEmpty(double.NaN)
                .Average(),
            _ => double.NaN
        };

        // A previous value of zero has no percentage: the change from 0 to anything is not "infinite
        // growth", it is simply not a ratio. Showing no trend beats showing a meaningless one.
        if (double.IsNaN(previous) || previous == 0)
        {
            return result with { Previous = double.IsNaN(previous) ? null : previous };
        }

        var delta = (result.Number.Value - previous) / previous * 100.0;
        var rounded = Math.Round(delta, MidpointRounding.AwayFromZero);

        return result with
        {
            Previous = previous,
            DeltaPercent = delta,
            DeltaDisplay = $"{(rounded > 0 ? "+" : "")}{rounded.ToString("N0", CultureInfo.CurrentCulture)} %",
            Trend = rounded switch
            {
                > 0 => KpiTrend.Up,
                < 0 => KpiTrend.Down,
                _ => KpiTrend.Flat
            }
        };
    }

    // ---- Diagnostics ---------------------------------------------------

    /// <summary>
    /// Filter values that occur in NO row of the whole source - which is what a renamed medicine
    /// looks like from here, since definitions store display names (they have to: the nightly demo
    /// reset re-assigns Medicine ids while KPI rows survive).
    ///
    /// Checked against every row, not against the windowed ones: "no such value anywhere" means the
    /// filter is broken, whereas "none in this period" is a perfectly good zero.
    /// </summary>
    private static string? StaleFilterHint(KpiDefinition definition, IReadOnlyList<KpiRow> rows)
    {
        var stale = new List<string>();

        foreach (var (key, values) in definition.Filters)
        {
            if (values is null || values.Count == 0)
            {
                continue;
            }

            var present = rows.SelectMany(r => r.TagValues(key)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            stale.AddRange(values.Where(v => !present.Contains(v)));
        }

        if (stale.Count == 0)
        {
            return null;
        }

        return stale.Count == 1
            ? $"Der Filterwert „{stale[0]}“ kommt in den Daten nicht vor."
            : $"{stale.Count} Filterwerte kommen in den Daten nicht vor: {string.Join(", ", stale)}.";
    }

    // ---- Formatting ----------------------------------------------------

    private static string Format(double value, int decimals)
        => value.ToString("N" + Math.Clamp(decimals, 0, 4), CultureInfo.CurrentCulture);

    private static string WithUnit(string text, string? unit)
        => string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit.Trim()}";
}
