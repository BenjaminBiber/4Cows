using System.Text;
using BB_Cow.Class;

namespace BB_Cow.Kpi;

/// <summary>
/// The generated script plus everything about the definition it could NOT express.
/// </summary>
public sealed record KpiSqlScript(string Sql, IReadOnlyList<string> Notes)
{
    /// <summary>Script with the notes prepended as SQL comments - what goes into the editor.</summary>
    public string Annotated
    {
        get
        {
            if (Notes.Count == 0)
            {
                return Sql;
            }

            var header = new StringBuilder();
            foreach (var note in Notes)
            {
                header.Append("-- ").AppendLine(note);
            }

            return header.Append(Sql).ToString();
        }
    }
}

/// <summary>
/// Translates a declarative definition into the MySQL a KPI script has to be: ONE row, ONE column
/// named "value", as text.
///
/// The point is the expert mode's running start - build most of it by clicking, then refine the
/// generated query by hand. That only works if the translation is faithful, so this generator aims
/// at reproducing the evaluator's result EXACTLY, including its German number formatting, rather
/// than at looking tidy. Where a definition cannot be expressed as a single scalar query at all
/// (the previous-period comparison), that is stated as a note instead of quietly dropped.
///
/// The SQL knowledge lives here in one place rather than as a second metadata model on
/// KpiSourceRegistry: the registry exists so the UI needs no per-source branching, and a single
/// switch inside the one component that generates SQL is not the problem that solved.
/// </summary>
public static class KpiSqlBuilder
{
    /// <summary>
    /// The evaluator formats with CultureInfo.CurrentCulture, and this application runs German
    /// throughout - so FORMAT gets that locale, and pasting the script into the editor yields the
    /// same text the tile shows ("1.240", not "1240"). Change both together or they drift.
    /// </summary>
    private const string Locale = "de_DE";

    private const string Alias = "t";

    public static KpiSqlScript Build(KpiDefinition definition, KpiSourceInfo source)
    {
        var table = Table(source.Id);
        var notes = new List<string>();

        if (definition.CompareToPrevious && definition.AllowsComparison && source.SupportsCompare)
        {
            notes.Add("Der Vergleich zum Vorzeitraum fehlt: eine KPI-Abfrage liefert genau einen");
            notes.Add("Wert, das Skript zeigt deshalb nur den aktuellen Zeitraum.");
        }

        var joins = new List<string>();
        var conditions = new List<string>();

        // Timeframe first, so the generated WHERE reads in the same order as the dialog.
        if (definition.Timeframe != KpiTimeframe.All && table.DateColumn is not null)
        {
            conditions.Add(TimeframeCondition(table.DateColumn, definition.Timeframe, source.IsPlanned));
        }

        foreach (var (key, values) in definition.Filters)
        {
            if (values is not { Count: > 0 })
            {
                continue;
            }

            var condition = FilterCondition(source.Id, key, values, joins);
            if (condition is not null)
            {
                conditions.Add(condition);
            }
        }

        var sql = definition.Measure == KpiMeasure.TopValue
            ? Ranking(definition, source, table, joins, conditions)
            : Aggregate(definition, table, joins, conditions);

        return new KpiSqlScript(sql, notes);
    }

    // ---- Aggregates ----------------------------------------------------

    private static string Aggregate(
        KpiDefinition definition, SourceTable table, List<string> joins, List<string> conditions)
    {
        var value = definition.Measure switch
        {
            KpiMeasure.Count => Formatted("COUNT(*)", 0, definition.Unit),
            KpiMeasure.CountDistinctCows => Formatted($"COUNT(DISTINCT {table.CowIdColumn})", 0, definition.Unit),
            // COALESCE because SUM over nothing is NULL in SQL while the evaluator reports 0.
            KpiMeasure.SumDosage => Formatted(
                $"COALESCE(SUM({table.DosageColumn}), 0)", definition.Decimals, definition.Unit),
            // An average over nothing is undefined, and the evaluator prints an en dash for it
            // rather than a zero - so the script does the same instead of returning NULL.
            KpiMeasure.AvgDosage => $"COALESCE({Formatted($"AVG({table.DosageColumn})", definition.Decimals, definition.Unit)}, '–')",
            _ => "'?'"
        };

        var sql = new StringBuilder();
        sql.Append("SELECT ").Append(value).AppendLine(" AS value");
        sql.Append("  FROM ").Append(table.Name).Append(' ').Append(Alias);
        AppendJoins(sql, joins);
        AppendWhere(sql, conditions);
        sql.Append(';');
        return sql.ToString();
    }

    /// <summary>
    /// FORMAT with the German locale reproduces ToString("N&lt;d&gt;") including the thousands
    /// separator; the unit is appended the same way the tile appends it.
    /// </summary>
    private static string Formatted(string expression, int decimals, string? unit)
    {
        var formatted = $"FORMAT({expression}, {Math.Clamp(decimals, 0, 4)}, '{Locale}')";
        return string.IsNullOrWhiteSpace(unit)
            ? formatted
            : $"CONCAT({formatted}, ' ', {Literal(unit.Trim())})";
    }

    // ---- Ranking (Top-1) -----------------------------------------------

    private static string Ranking(
        KpiDefinition definition,
        KpiSourceInfo source,
        SourceTable table,
        List<string> joins,
        List<string> conditions)
    {
        // Claw findings are the one dimension a plain GROUP BY cannot express: they live in four
        // flat columns plus two synthetic states, and one treatment carrying the same finding on
        // two claws must count ONCE. UNION (not UNION ALL) over (id, label) pairs is exactly that
        // per-row de-duplication.
        if (definition.GroupBy == KpiGroupBy.ClawFinding)
        {
            return ClawFindingRanking(table, joins, conditions);
        }

        var label = GroupExpression(source.Id, definition.GroupBy, joins);
        var byCow = definition.GroupBy == KpiGroupBy.Cow;

        // The label expression is named ONCE, in a derived table, and referenced by name afterwards.
        // Spelling it out in SELECT, WHERE, GROUP BY and ORDER BY instead is equivalent but unreadable
        // - the udder label alone is a five-line CASE, and a script nobody can read is useless as the
        // starting point this is meant to be.
        var select = new StringBuilder();
        select.Append("SELECT ").Append(label).Append(" AS label");
        if (byCow)
        {
            // Group by the ANIMAL and only display its collar number - a re-issued collar would
            // otherwise merge two cows.
            select.Append(", ").Append(table.CowIdColumn).Append(" AS cow");
        }

        var derived = new StringBuilder();
        derived.Append(select).Append(Environment.NewLine);
        derived.Append("            FROM ").Append(table.Name).Append(' ').Append(Alias);
        AppendJoins(derived, joins);
        AppendWhere(derived, conditions);

        var inner = new StringBuilder();
        inner.Append("SELECT label").Append(Environment.NewLine);
        inner.Append("    FROM (").Append(Environment.NewLine);
        inner.Append("          ").Append(derived).Append(Environment.NewLine);
        inner.Append("         ) x").Append(Environment.NewLine);
        inner.Append("   WHERE label IS NOT NULL AND label <> ''").Append(Environment.NewLine);
        inner.Append("   GROUP BY ").Append(byCow ? "cow, label" : "label").Append(Environment.NewLine);
        // The tiebreaker the hand-written scripts never had: BINARY makes it ordinal, matching the
        // evaluator, so a tie no longer resolves differently between two renders.
        inner.Append("   ORDER BY COUNT(*) DESC, BINARY label ASC").Append(Environment.NewLine);
        inner.Append("   LIMIT 1");

        return Ranked(inner.ToString());
    }

    /// <summary>
    /// Wraps a ranking so it ALWAYS yields exactly one row.
    ///
    /// A GROUP BY over nothing returns no rows at all, and a script that returns nothing ends up as
    /// the old catch-all "--" - indistinguishable from a broken query. The evaluator prints an en
    /// dash for an empty ranking, so the script says the same.
    /// </summary>
    private static string Ranked(string inner)
        => "SELECT COALESCE((" + Environment.NewLine
           + inner + Environment.NewLine
           + "       ), '–') AS value;";

    private static string ClawFindingRanking(
        SourceTable table, List<string> joins, List<string> conditions)
    {
        var branches = new List<string>();

        foreach (var position in HoofPositions.All)
        {
            var finding = $"TRIM({Alias}.Claw_Finding_{position})";
            branches.Add(Branch(finding, new List<string>(conditions) { $"{finding} <> ''" }));
        }

        branches.Add(Branch(
            Literal(KpiFlags.Bandage),
            new List<string>(conditions) { BandageCondition() }));

        branches.Add(Branch(
            Literal(KpiFlags.Block),
            new List<string>(conditions) { BlockCondition() }));

        var separator = $"{Environment.NewLine}          UNION{Environment.NewLine}          ";

        var inner = new StringBuilder();
        inner.Append("SELECT label").Append(Environment.NewLine);
        inner.Append("    FROM (").Append(Environment.NewLine);
        inner.Append("          ").Append(string.Join(separator, branches)).Append(Environment.NewLine);
        inner.Append("         ) x").Append(Environment.NewLine);
        inner.Append("   GROUP BY label").Append(Environment.NewLine);
        inner.Append("   ORDER BY COUNT(*) DESC, BINARY label ASC").Append(Environment.NewLine);
        inner.Append("   LIMIT 1");

        return Ranked(inner.ToString());

        string Branch(string label, List<string> branchConditions)
        {
            var branch = new StringBuilder();
            branch.Append("SELECT DISTINCT ").Append(table.IdColumn).Append(" AS id, ")
                .Append(label).Append(" AS label FROM ").Append(table.Name).Append(' ').Append(Alias);
            AppendJoins(branch, joins);
            AppendWhere(branch, branchConditions);
            return branch.ToString();
        }
    }

    // ---- Filters -------------------------------------------------------

    private static string? FilterCondition(
        KpiSourceId sourceId, string key, IReadOnlyList<string> values, List<string> joins)
    {
        switch (key)
        {
            case KpiTagKeys.ClawFinding:
                return ClawFindingCondition(values);

            case KpiTagKeys.ClawPosition:
            {
                var positions = HoofPositions.All
                    .Where(p => values.Contains(p.ToString(), StringComparer.OrdinalIgnoreCase))
                    .Select(p => $"{Alias}.Claw_Finding_{p}")
                    .ToList();

                return positions.Count == 0 ? "1 = 0" : $"({string.Join(" OR ", positions)})";
            }

            default:
            {
                var expression = TagExpression(sourceId, key, joins);
                return expression is null ? null : InList(expression, values);
            }
        }
    }

    /// <summary>
    /// Findings and the two synthetic states in ONE condition, OR-ed - the same single group the
    /// claw table offers, so a tile and its drill-down cannot disagree.
    /// </summary>
    private static string ClawFindingCondition(IReadOnlyList<string> values)
    {
        var parts = new List<string>();

        var findings = values
            .Where(v => !string.Equals(v, KpiFlags.Bandage, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(v, KpiFlags.Block, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (findings.Count > 0)
        {
            parts.AddRange(HoofPositions.All.Select(p => InList($"TRIM({Alias}.Claw_Finding_{p})", findings)));
        }

        if (values.Contains(KpiFlags.Bandage, StringComparer.OrdinalIgnoreCase))
        {
            parts.Add(BandageCondition());
        }

        if (values.Contains(KpiFlags.Block, StringComparer.OrdinalIgnoreCase))
        {
            parts.Add(BlockCondition());
        }

        return parts.Count == 0 ? "1 = 0" : $"({string.Join(" OR ", parts)})";
    }

    /// <summary>Exactly Claw_Table.MatchesFinding: a removed bandage is not a bandage.</summary>
    private static string BandageCondition()
        => $"(NOT {Alias}.IsBandageRemoved AND ("
           + string.Join(" OR ", HoofPositions.All.Select(p => $"{Alias}.Bandage_{p}")) + "))";

    private static string BlockCondition()
        => "(" + string.Join(" OR ", HoofPositions.All.Select(p => $"{Alias}.Block_{p}")) + ")";

    // ---- Expressions per tag -------------------------------------------

    private static string? TagExpression(KpiSourceId sourceId, string key, List<string> joins)
    {
        switch (key)
        {
            case KpiTagKeys.Medicine:
                Join(joins, $"LEFT JOIN Medicine med ON {Alias}.Medicine_ID = med.Medicine_ID");
                return "med.Medicine_Name";

            case KpiTagKeys.WhereHow:
                Join(joins, $"LEFT JOIN WhereHow wh ON {Alias}.WhereHow_ID = wh.WhereHow_ID");
                return "wh.WhereHow_Name";

            case KpiTagKeys.UdderQuarter:
                Join(joins, $"LEFT JOIN Udder u ON {Alias}.{UdderColumn(sourceId)} = u.UDDER_ID");
                return UdderLabelExpression();

            case KpiTagKeys.Cow:
                Join(joins, $"LEFT JOIN Cow c ON {Alias}.Ear_Tag_Number = c.Cow_ID");
                return "CAST(c.Collar_Number AS CHAR)";

            case KpiTagKeys.Calf:
                return $"CASE WHEN {Alias}.Is_Calv THEN {Literal(KpiFlags.Calf)} ELSE {Literal(KpiFlags.NotCalf)} END";

            case KpiTagKeys.Herd:
                return $"CASE WHEN {Alias}.IsGone THEN {Literal(KpiFlags.Gone)} ELSE {Literal(KpiFlags.InHerd)} END";

            case KpiTagKeys.Found:
                return $"CASE WHEN {Alias}.IsFound THEN {Literal(KpiFlags.Found)} ELSE {Literal(KpiFlags.NotFound)} END";

            case KpiTagKeys.Treated:
                return $"CASE WHEN {Alias}.IsTreatet THEN {Literal(KpiFlags.Treated)} ELSE {Literal(KpiFlags.NotTreated)} END";

            default:
                return null;
        }
    }

    private static string GroupExpression(KpiSourceId sourceId, KpiGroupBy groupBy, List<string> joins)
    {
        var key = KpiTagKeys.ForGroupBy(groupBy);
        return key is null ? "NULL" : TagExpression(sourceId, key, joins) ?? "NULL";
    }

    /// <summary>
    /// Reproduces WhereHowService.GetUdderString - including "Alle 4" and, crucially, the EMPTY
    /// string for the sentinel row with no quarter set. That empty label is what makes the ranking
    /// skip it, so the hardcoded "UDDER_ID != 16" is not needed here either.
    ///
    /// Order of the quarters is LV, LH, RV, RH, matching that method rather than the 2x2 drawing
    /// order - otherwise the label would read differently than everywhere else in the app.
    /// </summary>
    private static string UdderLabelExpression()
        => "CASE WHEN u.Quarter_LV AND u.Quarter_LH AND u.Quarter_RV AND u.Quarter_RH THEN 'Alle 4' "
           + "ELSE CONCAT_WS('/ ', "
           + "CASE WHEN u.Quarter_LV THEN 'LV' END, "
           + "CASE WHEN u.Quarter_LH THEN 'LH' END, "
           + "CASE WHEN u.Quarter_RV THEN 'RV' END, "
           + "CASE WHEN u.Quarter_RH THEN 'RH' END) END";

    // ---- Table facts ---------------------------------------------------

    private sealed record SourceTable(
        string Name, string IdColumn, string CowIdColumn, string? DateColumn, string? DosageColumn);

    private static SourceTable Table(KpiSourceId id) => id switch
    {
        KpiSourceId.CowTreatment => new SourceTable(
            "Cow_Treatment", $"{Alias}.Cow_Treatment_ID", $"{Alias}.Ear_Tag_Number",
            $"{Alias}.Administration_Date", $"{Alias}.Medicine_Dosage"),

        KpiSourceId.ClawTreatment => new SourceTable(
            "Claw_Treatment", $"{Alias}.Claw_Treatment_ID", $"{Alias}.Ear_Tag_Number",
            $"{Alias}.Treatment_Date", null),

        KpiSourceId.PlannedCowTreatment => new SourceTable(
            "Planned_Cow_Treatment", $"{Alias}.Planned_Cow_Treatment_ID", $"{Alias}.Ear_Tag_Number",
            $"{Alias}.Administration_Date", $"{Alias}.Medicine_Dosage"),

        KpiSourceId.PlannedClawTreatment => new SourceTable(
            "Planned_Claw_Treatment", $"{Alias}.Planned_Claw_Treatment_ID", $"{Alias}.Ear_Tag_Number",
            $"{Alias}.Treatment_Date", null),

        // Cow has no date column at all, and its own key is the cow id.
        _ => new SourceTable("Cow", $"{Alias}.Cow_ID", $"{Alias}.Cow_ID", null, null)
    };

    /// <summary>
    /// The udder foreign key is spelled differently in the two tables that have one:
    /// COW_QUARTER_ID in Cow_Treatment, Udder_ID in Planned_Cow_Treatment.
    /// </summary>
    private static string UdderColumn(KpiSourceId id)
        => id == KpiSourceId.PlannedCowTreatment ? "Udder_ID" : "COW_QUARTER_ID";

    // ---- Assembly helpers ----------------------------------------------

    private static string TimeframeCondition(string dateColumn, KpiTimeframe timeframe, bool planned)
    {
        var days = KpiDrillDownUrl.Days(timeframe);

        // Same direction rule as the evaluator: recorded treatments look back and exclude the
        // future, planned ones look forward.
        return planned
            ? $"DATE({dateColumn}) >= CURDATE() AND DATE({dateColumn}) <= DATE_ADD(CURDATE(), INTERVAL {days} DAY)"
            : $"DATE({dateColumn}) >= DATE_SUB(CURDATE(), INTERVAL {days} DAY) AND DATE({dateColumn}) <= CURDATE()";
    }

    private static void Join(List<string> joins, string join)
    {
        if (!joins.Contains(join))
        {
            joins.Add(join);
        }
    }

    private static void AppendJoins(StringBuilder sql, IReadOnlyList<string> joins)
    {
        foreach (var join in joins)
        {
            sql.AppendLine().Append("       ").Append(join);
        }
    }

    private static void AppendWhere(StringBuilder sql, IReadOnlyList<string> conditions)
    {
        if (conditions.Count == 0)
        {
            return;
        }

        sql.AppendLine().Append(" WHERE ")
            .Append(string.Join($"{Environment.NewLine}   AND ", conditions));
    }

    private static string InList(string expression, IReadOnlyList<string> values)
        => values.Count == 1
            ? $"{expression} = {Literal(values[0])}"
            : $"{expression} IN ({string.Join(", ", values.Select(Literal))})";

    /// <summary>
    /// A SQL string literal. Backslashes are escaped as well as quotes: MySQL treats a backslash as
    /// an escape character by default, so a medicine name containing one would otherwise change the
    /// meaning of the query rather than just its text.
    /// </summary>
    private static string Literal(string value)
        => "'" + value.Replace("\\", "\\\\").Replace("'", "''") + "'";
}
