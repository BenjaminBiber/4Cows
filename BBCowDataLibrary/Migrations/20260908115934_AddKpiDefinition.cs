using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBCowDataLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddKpiDefinition : Migration
    {
        // Written to be IDEMPOTENT, for the same reason AddCowIdAndIsCalv is (see its header):
        // MySQL/MariaDB DDL is non-transactional and auto-commits every statement, so a migration
        // that fails half-way leaves its earlier steps permanently applied while EF does NOT record
        // the migration as done. The next deploy then re-runs it from the top and dies on
        // "Duplicate column 'Kind'". Guarding each step against the current schema makes a re-run
        // self-healing.
        //
        // AddAppSetting was allowed to use the plain migrationBuilder API because CreateTable on a
        // brand-new table cannot collide. This migration alters KPI, a table that also exists in
        // databases created from the legacy 4Cows-DB-V3.sql script.
        //
        // No FOREIGN_KEY_CHECKS juggling is needed here: nothing references KPI.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__ef_migrate_kpi_definition_up`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__ef_migrate_kpi_definition_up`()
BEGIN
    DECLARE v_has_kind INT DEFAULT 0;
    DECLARE v_has_def  INT DEFAULT 0;

    -- 0 = KpiKind.Sql. Every KPI that already exists keeps running its hand-written Script, so an
    -- upgraded database is behaviourally identical to before. KpiKind orders Sql first precisely so
    -- that this column default and the C# default agree.
    SELECT COUNT(*) INTO v_has_kind FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'KPI' AND COLUMN_NAME = 'Kind';
    IF v_has_kind = 0 THEN
        ALTER TABLE `KPI` ADD COLUMN `Kind` int NOT NULL DEFAULT 0;
    END IF;

    -- The declarative definition as JSON; NULL for SQL KPIs.
    SELECT COUNT(*) INTO v_has_def FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'KPI' AND COLUMN_NAME = 'Definition';
    IF v_has_def = 0 THEN
        ALTER TABLE `KPI` ADD COLUMN `Definition` longtext CHARACTER SET utf8mb4 NULL;
    END IF;
END;");

            migrationBuilder.Sql("CALL `__ef_migrate_kpi_definition_up`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__ef_migrate_kpi_definition_up`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__ef_migrate_kpi_definition_down`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__ef_migrate_kpi_definition_down`()
BEGIN
    DECLARE v_has_kind INT DEFAULT 0;
    DECLARE v_has_def  INT DEFAULT 0;

    -- Builder KPIs cannot survive without their definition, and their Script is "" - dropping the
    -- columns would leave tiles that execute an empty statement. Turning them back into visible,
    -- editable SQL placeholders is the only honest downgrade.
    UPDATE `KPI` SET `Script` = 'SELECT CAST(0 AS CHAR) AS value', `Url` = '/Settings'
        WHERE `Kind` = 1 AND (`Script` IS NULL OR `Script` = '');

    SELECT COUNT(*) INTO v_has_def FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'KPI' AND COLUMN_NAME = 'Definition';
    IF v_has_def > 0 THEN
        ALTER TABLE `KPI` DROP COLUMN `Definition`;
    END IF;

    SELECT COUNT(*) INTO v_has_kind FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'KPI' AND COLUMN_NAME = 'Kind';
    IF v_has_kind > 0 THEN
        ALTER TABLE `KPI` DROP COLUMN `Kind`;
    END IF;
END;");

            migrationBuilder.Sql("CALL `__ef_migrate_kpi_definition_down`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__ef_migrate_kpi_definition_down`;");
        }
    }
}
