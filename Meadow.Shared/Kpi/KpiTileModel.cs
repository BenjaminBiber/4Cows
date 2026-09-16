using Meadow.Shared.Models;

namespace Meadow.Shared.Kpi;

/// <summary>
/// One dashboard tile: the KPI it came from plus the outcome of evaluating it.
///
/// Replaces the Dictionary&lt;KPI, string&gt; the dashboard used to receive. A bare string could only
/// ever say "--", which meant the tile could not tell an empty period from a broken definition, and
/// had nowhere to put a trend.
/// </summary>
public sealed record KpiTileModel
{
    public required KPI Kpi { get; init; }

    public required KpiResult Result { get; init; }

    /// <summary>
    /// The history, when the definition asks for one, and null otherwise.
    ///
    /// On the tile model rather than in KpiResult, because it is not part of the outcome - it is an
    /// extra the DASHBOARD asks for. Keeping it here also keeps the promise that a plain tile costs
    /// nothing: KpiDashboard only calls KpiEvaluator.Series for a definition whose SeriesDisplay
    /// says so, and never for the "+" tile or a hand-written script.
    /// </summary>
    public KpiSeries? Series { get; init; }

    /// <summary>The synthetic "add a KPI" tile, which is not a KPI and is never evaluated.</summary>
    public bool IsAddTile { get; init; }

    /// <summary>
    /// Marker for the synthetic tile. Kept as int.MinValue because Index.razor already recognises
    /// it that way - changing it would be churn for no gain.
    /// </summary>
    public const int AddTileId = int.MinValue;

    /// <summary>
    /// The "+" tile. It used to carry the fake script "select '+' as value", which meant a database
    /// round trip whose entire purpose was to return a plus sign. Now it never reaches the
    /// evaluator, the script guard or the database.
    /// </summary>
    public static KpiTileModel AddTile() => new()
    {
        Kpi = new KPI(AddTileId, "KPI hinzufügen", "/Settings", string.Empty, 8),
        Result = new KpiResult { State = KpiResultState.Ok, Display = "+" },
        IsAddTile = true
    };
}
