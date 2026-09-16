namespace Meadow.Shared.Kpi;

/// <summary>
/// Why a KPI shows what it shows.
///
/// This distinction is the whole point. Today GetKPIValue returns the literal "--" both when a
/// script yields no rows and when it throws, so a broken KPI is indistinguishable from an empty one
/// on the dashboard - a typo can sit there for months looking like "no data".
/// </summary>
public enum KpiResultState
{
    /// <summary>A value was computed.</summary>
    Ok,

    /// <summary>Nothing matched. Legitimate: an empty period, a filter nothing satisfies.</summary>
    Empty,

    /// <summary>The KPI could not be evaluated at all. Always carries a <see cref="KpiResult.Message"/>.</summary>
    Error
}

/// <summary>
/// The traffic light, once a KPI carries a target.
///
/// None is not a fourth colour, it means "not rated" - no target set, nothing rateable (a Top-1
/// label), or the value itself is under suspicion. That last case is the one worth naming: mixed
/// dosage units and a filter value that occurs nowhere both mean "this number is doubtful", and a
/// green dot on a doubtful number is worse than no dot at all. So the light stays silent for as
/// long as the tile has something to complain about.
/// </summary>
public enum KpiStatus
{
    None,
    Good,
    Warning,
    Bad
}

/// <summary>Direction of the previous-period delta, for the tile to render.</summary>
public enum KpiTrend
{
    None,
    Up,
    Down,
    Flat
}

/// <summary>
/// The outcome of evaluating one KPI: the formatted string for the tile plus everything needed to
/// explain or drill into it.
/// </summary>
public sealed record KpiResult
{
    public required KpiResultState State { get; init; }

    /// <summary>Ready for the tile. "0", "1.240", "42,50 ml", "LV/ RH", or "!" on an error.</summary>
    public required string Display { get; init; }

    /// <summary>The numeric value, or null for a Top-1 ranking and on an error.</summary>
    public double? Number { get; init; }

    /// <summary>The winning label of a Top-1 ranking, null otherwise.</summary>
    public string? Label { get; init; }

    public double? Previous { get; init; }

    public double? DeltaPercent { get; init; }

    /// <summary>Formatted delta, e.g. "+12 %". Null when no comparison was possible.</summary>
    public string? DeltaDisplay { get; init; }

    public KpiTrend Trend { get; init; }

    /// <summary>
    /// The traffic light. <see cref="KpiStatus.None"/> unless the definition carries a target the
    /// evaluator could actually apply - which is also what every result computed before targets
    /// existed says, so nothing that does not opt in changes colour.
    /// </summary>
    public KpiStatus Status { get; init; }

    /// <summary>How many rows the filters and timeframe left. Lets "0" be told apart from "broken".</summary>
    public int MatchedRows { get; init; }

    /// <summary>
    /// The error text on <see cref="KpiResultState.Error"/>, and otherwise an optional warning that
    /// does not invalidate the value - e.g. a filter value that occurs in no row at all, which is
    /// what a renamed medicine looks like. Without that hint such a KPI just quietly reads 0.
    /// </summary>
    public string? Message { get; init; }

    public static KpiResult Failed(string message) => new()
    {
        State = KpiResultState.Error,
        Display = "!",
        Message = message
    };
}
