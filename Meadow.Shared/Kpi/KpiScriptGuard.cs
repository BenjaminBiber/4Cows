using System.Text.RegularExpressions;

namespace BB_Cow.Kpi;

/// <summary>
/// Rejects a hand-written KPI script before it is executed.
///
/// Context for why this exists at all: the script goes verbatim into
/// Database.SqlQueryRaw, the application has no authentication of any kind, and the documented
/// Docker setup connects as root. Until now "DELETE FROM Cow_Treatment" saved as a KPI simply ran,
/// and the tile then showed "--".
///
/// The guard is deliberately NOT a keyword blacklist. A blacklist of INSERT/UPDATE/DELETE/... is
/// both too strict and pointless here:
///
///   * Too strict, because those words appear legitimately inside a SELECT - an alias like
///     "AS 'letztes Update'" or a string literal would be refused for no reason.
///   * Pointless, because MySQL does not allow a DELETE or a DROP inside a statement that starts
///     with SELECT. "One statement, starting with SELECT" already excludes every DDL and DML verb
///     structurally, which is a stronger guarantee than pattern matching on words.
///
/// What genuinely remains dangerous inside a single SELECT is writing to the file system, so that
/// is checked explicitly.
/// </summary>
public static class KpiScriptGuard
{
    private static readonly Regex BlockComments = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex LineComments = new(@"(--|#)[^\r\n]*", RegexOptions.Compiled);
    private static readonly Regex FileWrite = new(@"\bINTO\s+(OUTFILE|DUMPFILE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Null when the script may run, otherwise the reason it may not - phrased for the dialog.
    /// </summary>
    public static string? Reject(string? script)
    {
        var sql = Strip(script);

        if (sql.Length == 0)
        {
            return "Das Skript ist leer.";
        }

        // A trailing semicolon is normal and harmless; any further one means a second statement,
        // which is the whole attack surface ("SELECT 1; DROP TABLE Cow").
        sql = sql.TrimEnd(';', ' ', '\t', '\r', '\n');
        if (sql.Contains(';'))
        {
            return "Nur eine einzelne Anweisung ist erlaubt - das Skript enthält mehrere.";
        }

        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && !sql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return "Das Skript muss mit SELECT (oder WITH) beginnen - es darf nur lesen.";
        }

        if (FileWrite.IsMatch(sql))
        {
            return "SELECT ... INTO OUTFILE/DUMPFILE schreibt Dateien und ist nicht erlaubt.";
        }

        return null;
    }

    /// <summary>
    /// Comments removed so they can neither hide a second statement nor disguise the leading verb.
    ///
    /// Known limitation: a string literal containing "--" or "/*" is mangled by this, which can
    /// cause a false rejection. Only the CHECK sees the stripped text - the original script is what
    /// runs - so the worst case is a clear error message on an unusual but legitimate script,
    /// which beats a parser of our own.
    /// </summary>
    private static string Strip(string? script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return string.Empty;
        }

        var sql = BlockComments.Replace(script, " ");
        sql = LineComments.Replace(sql, " ");
        return sql.Trim();
    }
}
