using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBCowDataLibrary.Migrations
{
    /// <inheritdoc />
    public partial class ConvertCountKpisToBuilder : Migration
    {
        // Turns the four shipped COUNT KPIs into declarative ones in databases that ALREADY EXIST.
        // A fresh installation gets them from DataSeeder; this is for everyone who upgraded, whose
        // KPI table is skipped by that seeder's AnyAsync guard and would otherwise keep running
        // hand-written SQL forever.
        //
        // Only these four. The three Top-1 KPIs are deliberately left alone: matching their
        // multi-line GROUP BY scripts in a migration would be guesswork, and one of them was
        // rewritten in place by AddCowIdAndIsCalv, so its text differs depending on which
        // migrations a database has seen. They keep working as Kind = 0 and can be converted in the
        // dialog in a few clicks.
        //
        // No guarded stored procedure here, unlike AddKpiDefinition: there is no DDL to half-apply,
        // and "AND Kind = 0" already makes every statement a no-op on a second run.
        //
        // Matching is EXACT on a normalised script rather than a LIKE pattern. Two reasons: LIKE
        // would treat the underscores in the table names as single-character wildcards, and an
        // exact comparison cannot accidentally catch a customer's own script that merely starts the
        // same way. Carriage returns and line feeds are stripped and a trailing semicolon ignored,
        // so a reformatted copy of the shipped script still matches.
        //
        // Script is NOT cleared. That keeps this migration losslessly reversible, and the leftover
        // text is inert because a KPI only counts as declarative when Kind says so AND a definition
        // is present (see KPI.IsBuilder). As a bonus, switching such a KPI to the expert mode still
        // shows the query it used to run.

        private const string Normalised =
            "TRIM(TRAILING ';' FROM TRIM(REPLACE(REPLACE(`Script`, CHAR(13), ''), CHAR(10), '')))";

        /// <summary>The four scripts, without their trailing semicolon.</summary>
        private static readonly (string Table, string Json)[] Conversions =
        {
            ("Planned_Cow_Treatment",
                "{\"source\":\"PlannedCowTreatment\",\"measure\":\"Count\",\"groupBy\":\"None\","
                + "\"timeframe\":\"All\",\"compareToPrevious\":false,\"decimals\":0,\"filters\":{}}"),
            ("Cow_Treatment",
                "{\"source\":\"CowTreatment\",\"measure\":\"Count\",\"groupBy\":\"None\","
                + "\"timeframe\":\"All\",\"compareToPrevious\":false,\"decimals\":0,\"filters\":{}}"),
            ("Claw_Treatment",
                "{\"source\":\"ClawTreatment\",\"measure\":\"Count\",\"groupBy\":\"None\","
                + "\"timeframe\":\"All\",\"compareToPrevious\":false,\"decimals\":0,\"filters\":{}}"),
            ("Planned_Claw_Treatment",
                "{\"source\":\"PlannedClawTreatment\",\"measure\":\"Count\",\"groupBy\":\"None\","
                + "\"timeframe\":\"All\",\"compareToPrevious\":false,\"decimals\":0,\"filters\":{}}")
        };

        private static string ScriptFor(string table)
            => $"SELECT CAST(COUNT(*) AS CHAR) AS value FROM {table}";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, json) in Conversions)
            {
                migrationBuilder.Sql(
                    "UPDATE `KPI` SET `Kind` = 1, `Definition` = '" + json + "' "
                    + "WHERE `Kind` = 0 AND " + Normalised + " = '" + ScriptFor(table) + "';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverts only what this migration converted: the definition must be exactly the one it
            // wrote, and the original script must still be there to fall back on.
            foreach (var (table, json) in Conversions)
            {
                migrationBuilder.Sql(
                    "UPDATE `KPI` SET `Kind` = 0, `Definition` = NULL "
                    + "WHERE `Kind` = 1 AND `Definition` = '" + json + "' "
                    + "AND " + Normalised + " = '" + ScriptFor(table) + "';");
            }
        }
    }
}
