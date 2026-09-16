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
        KpiTimeframe.Days90 => 90,
        _ => throw new ArgumentOutOfRangeException(
            nameof(timeframe), timeframe, "Für diesen Zeitraum ist kein Tageswert hinterlegt.")
    };

    /// <summary>
    /// The timeframe a drill-down link's "range=N" means, or null when no timeframe spans that many
    /// days.
    ///
    /// Null rather than a fallback: the caller keeps whatever the page already had, which is what
    /// "the link says nothing about the timeframe" should do. The alternative - guessing - is how
    /// the drill-down adapters used to read "7 => Days7, 30 => Days30, _ => unchanged", where a 90
    /// arrived and silently changed nothing.
    /// </summary>
    public static KpiTimeframe? FromDays(int? days)
        => days is int value
            ? All.Cast<KpiTimeframe?>().FirstOrDefault(t => t != KpiTimeframe.All && Days(t!.Value) == value)
            : null;
}
