namespace Meadow.Shared.Kpi;

/// <summary>
/// How wide one point of a series is. Derived from the timeframe, never stored - a field for it
/// would be a field that can contradict the timeframe it belongs to.
/// </summary>
public enum KpiBucket
{
    None,
    Day,
    Week,
    Month
}

/// <summary>One point of a series.</summary>
/// <param name="Start">
/// First day of the section. For <see cref="KpiBucket.Week"/> this is the window start plus 7k days
/// - NOT a calendar week. The buckets have to tile the evaluator's window exactly, and that window
/// starts wherever "90 days ago" happens to fall.
/// </param>
/// <param name="Value">
/// Null means "no value defined here", which only happens for an average: a count or a sum over an
/// empty section is a real zero. The distinction is the difference between an honest sparkline and
/// a misleading one - a gap is not a dip to the floor.
/// </param>
/// <param name="MatchedRows">Rows that fell into this section, so "0" can be told apart from "none".</param>
public sealed record KpiSeriesPoint(DateTime Start, double? Value, int MatchedRows);

/// <summary>
/// A KPI over time.
///
/// Computed, never stored: there is no history table anywhere in this schema, and there does not
/// need to be - the treatment rows carry their own dates, so every past period is recomputable from
/// what is already in memory. What does NOT exist is a way to ask "what did this tile say last
/// month", because a deleted treatment is deleted from the past too. That is a property of the
/// schema, not a gap in this file.
/// </summary>
public sealed record KpiSeries
{
    public required KpiBucket Bucket { get; init; }

    /// <summary>Oldest first, matching CowProfile.Months.</summary>
    public required IReadOnlyList<KpiSeriesPoint> Points { get; init; }

    /// <summary>
    /// Whether the points together cover everything the tile counts.
    ///
    /// False for <see cref="Models.KpiTimeframe.All"/>: the tile counts every row ever recorded,
    /// the series shows the last twelve months. A reader comparing the last bar to the big number
    /// would otherwise conclude the chart was broken.
    /// </summary>
    public required bool CoversTimeframe { get; init; }

    /// <summary>Why there is no series, when there is none.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// Mean over the sections that HAVE a value, already formatted with the definition's unit and
    /// decimals. Null when there is nothing to average.
    ///
    /// The second number on the tile, and the reason the first one can be read at all: "120" says
    /// little, "120, on average 10 a month" says something. Formatted here rather than on the tile
    /// so it cannot end up spelled differently from the value above it.
    ///
    /// Sections without a value are skipped, not counted as zero - an average over an undefined
    /// section is undefined, not nought (see <see cref="KpiSeriesPoint.Value"/>).
    /// </summary>
    public string? AverageDisplay { get; init; }

    /// <summary>At least one section actually has rows - the test for "worth drawing".</summary>
    public bool HasAny => Points.Any(p => p.MatchedRows > 0);

    public double Max => Points
        .Where(p => p.Value.HasValue)
        .Select(p => p.Value!.Value)
        .DefaultIfEmpty(0)
        .Max();

    /// <summary>No series, and the reason in plain German.</summary>
    public static KpiSeries Unavailable(string message) => new()
    {
        Bucket = KpiBucket.None,
        Points = Array.Empty<KpiSeriesPoint>(),
        CoversTimeframe = false,
        Message = message
    };
}
