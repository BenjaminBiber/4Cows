using Meadow.Shared.Models;

namespace Meadow.Shared.Kpi;

/// <summary>
/// The day count behind each <see cref="KpiTimeframe"/>, in ONE place.
///
/// Before this existed, three call sites each wrote "timeframe == Days7 ? 7 : 30" - the evaluator's
/// window, the drill-down link and the dialog's label. A binary test over a growing enum does not
/// fail when a member is added: it compiles everywhere and silently means 30. The bug is invisible
/// in the stored JSON too, because the enum round-trips as a string, so the definition is correct
/// and only every reader of it is wrong.
///
/// A switch that THROWS on an unhandled member is the point. It turns "someone added a timeframe and
/// forgot this file" from a wrong number on a tile into a test failure - see KpiTotalityTests.
/// </summary>
public static class KpiTimeframes
{
    /// <summary>Every member, for the dialog's option list and for the totality tests.</summary>
    public static readonly IReadOnlyList<KpiTimeframe> All =
        Enum.GetValues<KpiTimeframe>();

    /// <summary>
    /// Days spanned by the timeframe; 0 for <see cref="KpiTimeframe.All"/>, which has no window.
    ///
    /// Careful: the window is inclusive at BOTH ends, so "7" spans 8 calendar days. That is not an
    /// off-by-one, it is the rule DateRanges.Matches uses, and KpiEvaluator.Window compensates for
    /// it when shifting to the previous period. See the comment there.
    /// </summary>
    public static int Days(KpiTimeframe timeframe) => timeframe switch
    {
        KpiTimeframe.All => 0,
        KpiTimeframe.Days7 => 7,
        KpiTimeframe.Days30 => 30,
        _ => throw new ArgumentOutOfRangeException(
            nameof(timeframe), timeframe, "Für diesen Zeitraum ist kein Tageswert hinterlegt.")
    };
}
