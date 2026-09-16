namespace Meadow.Shared.Kpi;

/// <summary>
/// What checking a script found out.
///
/// Three outcomes rather than two, because "the script is fine but nobody ran it" is not a failure
/// and must not be shown as one. Kpi:AllowScriptValidation is off by default and SHOULD be: the
/// application has no authentication and the documented container connects as root, so the
/// validation endpoint is the only place a request body reaches the database. Through the dialog an
/// attacker needs the screen; as an open endpoint it is one line of curl. The default is correct -
/// the UI simply never said that it was in effect.
/// </summary>
/// <param name="Rejection">
/// Why the script is not acceptable, from KpiScriptGuard. Checked locally and without a server, so
/// the most common mistakes - not a SELECT, more than one statement, writing to a file - are caught
/// offline and immediately.
/// </param>
/// <param name="Value">What the script returned, when it was actually run.</param>
/// <param name="NotRun">
/// The script passed the guard but was not executed, because script validation is switched off on
/// this server. A hint, not an error.
/// </param>
public sealed record KpiScriptCheck(string? Rejection, string? Value, bool NotRun)
{
    public static KpiScriptCheck Rejected(string reason) => new(reason, null, false);

    public static KpiScriptCheck Ran(string value) => new(null, value, false);

    public static KpiScriptCheck Skipped() => new(null, null, true);

    public bool Ok => Rejection is null;
}
