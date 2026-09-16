using System.Globalization;
using Meadow.Shared.Models;

namespace Meadow.Shared.Kpi;

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
        result = result with { Message = result.Message ?? StaleFilterHint(definition, rows) };

        return WithStatus(result, definition);
    }

    // ---- Traffic light -------------------------------------------------

    /// <summary>
    /// Applies the target, if there is one that can be applied.
    ///
    /// Withheld - deliberately, and each for its own reason:
    /// - no target, or a measure that yields a label rather than a number;
    /// - an error, which has no number at all;
    /// - a null Number, which is what an average over nothing is;
    /// - thresholds in the wrong order, which is a mistake in the definition rather than in the data;
    /// - ANY message. Mixed dosage units and a dead filter value both say "this number is doubtful",
    ///   and a green light on a doubtful number reads as reassurance that nobody checked.
    ///
    /// Note what is NOT withheld: a zero. FromCount reports State = Empty for it, but Number is 0,
    /// and "0 open bandages" is the single most useful green tile on the dashboard. That is exactly
    /// why the condition hangs on Number and not on State.
    /// </summary>
    private static KpiResult WithStatus(KpiResult result, KpiDefinition definition)
    {
        if (!definition.HasTarget
            || !definition.AllowsTarget
            || result.State == KpiResultState.Error
            || result.Number is not double value)
        {
            return result;
        }

        if (!definition.TargetIsConsistent)
        {
            return result with
            {
                Message = result.Message ?? InconsistentTargetHint(definition)
            };
        }

        return result.Message is null
            ? result with { Status = Rate(definition, value) }
            : result;
    }

    /// <summary>Bands are inclusive at their boundary: with "good up to 3", a 3 is good.</summary>
    private static KpiStatus Rate(KpiDefinition definition, double value)
    {
        var good = definition.TargetGood!.Value;
        var lower = definition.TargetDirection == KpiTargetDirection.LowerIsBetter;

        if (lower ? value <= good : value >= good)
        {
            return KpiStatus.Good;
        }

        if (definition.TargetWarning is not double warning)
        {
            return KpiStatus.Bad;
        }

        return (lower ? value <= warning : value >= warning) ? KpiStatus.Warning : KpiStatus.Bad;
    }

    private static string InconsistentTargetHint(KpiDefinition definition)
        => definition.TargetDirection == KpiTargetDirection.LowerIsBetter
            ? "Die Warnschwelle liegt unter dem Zielwert - so gibt es keinen Warnbereich."
            : "Die Warnschwelle liegt über dem Zielwert - so gibt es keinen Warnbereich.";

    // ---- Series --------------------------------------------------------

    /// <summary>Rolling month window for <see cref="KpiTimeframe.All"/>.</summary>
    /// <remarks>
    /// Deliberately the same number as CowProfileBuilder.WindowMonths, and a test asserts they stay
    /// equal. Not a reference to it: Meadow.Shared.Profile already depends on Meadow.Shared.Kpi
    /// (for KpiTrend), and pointing back would be a cycle straight through the library.
    /// </remarks>
    public const int MonthWindow = 12;

    /// <summary>
    /// The same KPI, sliced into periods.
    ///
    /// The contract that makes it trustworthy: the buckets TILE the window that
    /// <see cref="Evaluate"/> uses - same lower bound, same step - so for a count or a sum the
    /// points add up to the number on the tile, and the last bucket alone equals the tile value for
    /// a single-bucket window. A sparkline that disagrees with the figure above it is worse than no
    /// sparkline, so that is a test and not a hope.
    ///
    /// CountDistinctCows is the documented exception: counted per bucket, not cumulatively, so the
    /// same cow treated in two months counts twice and the points do NOT add up. The alternative -
    /// a running distinct count - would be a different measure from the one on the tile.
    /// </summary>
    /// <param name="now">Injected like everywhere else here, so bucket boundaries are testable.</param>
    public static KpiSeries Series(
        KpiDefinition definition,
        KpiSourceInfo source,
        IReadOnlyList<KpiRow> rows,
        DateTime now)
    {
        var invalid = Validate(definition, source);
        if (invalid is not null)
        {
            return KpiSeries.Unavailable(invalid.Message ?? "Diese Kennzahl lässt sich nicht auswerten.");
        }

        if (!source.HasDate)
        {
            // Cow has Cow_ID, Ear_Tag_Number, Collar_Number, Is_Calv, IsGone - and no date at all,
            // not even an arrival. "37 Kühe" has no past in this schema.
            return KpiSeries.Unavailable($"„{source.Label}“ hat keine Datumsspalte - dafür gibt es keinen Verlauf.");
        }

        if (!definition.YieldsNumber)
        {
            // The winner changes from period to period, so a curve of "how often did the OVERALL
            // winner occur each month" would be a different KPI from the one on the tile. Same
            // restraint as SupportsCompare showing nothing for planned sources.
            return KpiSeries.Unavailable(
                "„Häufigster Wert“ liefert einen Namen, keine Zahl - dafür gibt es keinen Verlauf.");
        }

        var bucket = BucketOf(definition.Timeframe);
        var spans = Buckets(definition.Timeframe, source.IsPlanned, now);
        var selected = ApplyFilters(definition, rows).Where(r => r.Date.HasValue).ToList();

        var points = new List<KpiSeriesPoint>(spans.Count);
        foreach (var (from, to) in spans)
        {
            // One pass over the rows per bucket, but only ONE filtering pass overall - that is the
            // part that used to be quadratic if written the obvious way.
            var inBucket = selected
                .Where(r => r.Date!.Value.Date >= from && r.Date.Value.Date <= to)
                .ToList();

            points.Add(new KpiSeriesPoint(from, MeasureOf(definition.Measure, inBucket), inBucket.Count));
        }

        return new KpiSeries
        {
            Bucket = bucket,
            Points = points,
            CoversTimeframe = definition.Timeframe != KpiTimeframe.All
        };
    }

    /// <summary>
    /// The filtered rows tallied by one dimension, largest first - what the detail page shows next
    /// to the chart.
    ///
    /// Takes the dimension as a PARAMETER rather than reading definition.GroupBy, because the two
    /// answer different questions: GroupBy is the ranking a Top-1 KPI is defined by, this is "what
    /// is this number made of". A count of cow treatments has no GroupBy at all, and breaking it
    /// down by medicine is exactly what makes the detail page worth opening.
    ///
    /// Empty when the source does not carry that dimension, so a caller can offer only the ones
    /// that yield something.
    /// </summary>
    public static IReadOnlyList<(string Label, int Count)> Breakdown(
        KpiDefinition definition,
        KpiSourceInfo source,
        IReadOnlyList<KpiRow> rows,
        KpiGroupBy dimension,
        DateTime now)
    {
        if (dimension == KpiGroupBy.None || !source.Supports(dimension))
        {
            return Array.Empty<(string, int)>();
        }

        var selected = Select(definition, source, rows, now, periodsBack: 0);

        return Group(dimension, selected)
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.Label, StringComparer.CurrentCulture)
            .Select(r => (r.Label, r.Count))
            .ToList();
    }

    private static KpiBucket BucketOf(KpiTimeframe timeframe) => timeframe switch
    {
        KpiTimeframe.All => KpiBucket.Month,
        // 91 days are exactly thirteen weeks, so the window divides without a remainder. Ninety-one
        // daily points would also be unreadable at 72 pixels wide.
        KpiTimeframe.Days90 => KpiBucket.Week,
        _ => KpiBucket.Day
    };

    /// <summary>
    /// The sections, oldest first.
    ///
    /// For a bounded timeframe they are cut out of <see cref="Window"/> itself, which is what
    /// guarantees they tile exactly what the tile counts. For "all" there is no window, so a rolling
    /// twelve months is used - copied from CowProfileBuilder.MonthSeries, including "rolling and not
    /// calendar year", because in January a calendar year is eleven twelfths empty.
    /// </summary>
    private static IReadOnlyList<(DateTime From, DateTime To)> Buckets(
        KpiTimeframe timeframe, bool planned, DateTime now)
    {
        if (timeframe == KpiTimeframe.All)
        {
            return MonthBuckets(planned, now);
        }

        var window = Window(timeframe, planned, now, periodsBack: 0)!.Value;
        var size = BucketOf(timeframe) == KpiBucket.Week ? 7 : 1;

        var spans = new List<(DateTime, DateTime)>();
        for (var start = window.From; start <= window.To; start = start.AddDays(size))
        {
            // Clamped at the window's end so a size that does not divide evenly produces a shorter
            // last section rather than reaching past what the tile counted.
            var end = start.AddDays(size - 1);
            spans.Add((start, end > window.To ? window.To : end));
        }

        return spans;
    }

    private static IReadOnlyList<(DateTime From, DateTime To)> MonthBuckets(bool planned, DateTime now)
    {
        var thisMonth = new DateTime(now.Year, now.Month, 1);

        // Same direction rule as Window: planned treatments are future-dated, so their twelve
        // months run FORWARD from this one. Looking back would show twelve empty bars.
        var first = planned ? thisMonth : thisMonth.AddMonths(-(MonthWindow - 1));

        var spans = new List<(DateTime, DateTime)>(MonthWindow);
        for (var i = 0; i < MonthWindow; i++)
        {
            var start = first.AddMonths(i);
            spans.Add((start, start.AddMonths(1).AddDays(-1)));
        }

        return spans;
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

        return ApplyFilters(definition, query).ToList();
    }

    /// <summary>
    /// The filters, without the timeframe.
    ///
    /// Split out for the series, which slices ONE filtered set into buckets rather than filtering
    /// once per bucket. Twelve buckets across eight tiles would otherwise be ninety-six passes over
    /// the rows where one does.
    /// </summary>
    private static IEnumerable<KpiRow> ApplyFilters(KpiDefinition definition, IEnumerable<KpiRow> rows)
    {
        var query = rows;

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

        return query;
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
        var days = KpiTimeframes.Days(timeframe);

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

    /// <summary>
    /// The bare number a measure produces over a set of rows - no formatting, no state, no unit.
    ///
    /// Null means "not a number here": a Top-1 ranking yields a label, and an average over nothing
    /// is undefined rather than zero. A count or a sum over nothing IS zero, and that difference
    /// matters: a gap in a series is not a dip to the floor, and "0 open bandages" deserves a green
    /// light where "no doses to average" deserves none.
    ///
    /// Extracted because the same switch was written twice - once to build the result, once to
    /// compute the previous period - and the series in KpiSeries would have been the third copy.
    /// </summary>
    private static double? MeasureOf(KpiMeasure measure, IReadOnlyList<KpiRow> rows) => measure switch
    {
        KpiMeasure.Count => rows.Count,
        KpiMeasure.CountDistinctCows => DistinctCows(rows),
        KpiMeasure.SumDosage => rows.Where(r => r.Dosage.HasValue).Sum(r => r.Dosage!.Value),
        // Average over a nullable sequence returns null when it is empty - exactly the wanted
        // "undefined", without a NaN sentinel to remember to check for.
        KpiMeasure.AvgDosage => rows.Where(r => r.Dosage.HasValue)
            .Select(r => (double?)r.Dosage!.Value)
            .Average(),
        _ => null
    };

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
        var groups = Group(definition.GroupBy, rows)
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

    private static IEnumerable<Ranked> Group(KpiGroupBy groupBy, List<KpiRow> rows)
    {
        // Ranking cows groups by the ANIMAL and only displays its collar number, which is what the
        // old query did with "GROUP BY ct.Ear_Tag_Number" while selecting Collar_Number. Grouping by
        // the collar instead would silently merge two animals whenever a number is re-issued - and
        // re-issuing is expected, since CowService.IsCollarInUse only reserves numbers of cows that
        // have not left the herd.
        if (groupBy == KpiGroupBy.Cow)
        {
            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.CowId))
                .GroupBy(r => r.CowId, StringComparer.Ordinal)
                .Select(g => new Ranked(g.First().CowLabel, g.Count()));
        }

        var key = KpiTagKeys.ForGroupBy(groupBy)!;

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
        var previousValue = MeasureOf(definition.Measure, previousRows);

        // A previous value of zero has no percentage: the change from 0 to anything is not "infinite
        // growth", it is simply not a ratio. Showing no trend beats showing a meaningless one.
        if (previousValue is not double previous || previous == 0)
        {
            return result with { Previous = previousValue };
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
