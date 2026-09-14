using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBCowDataLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddCowIdAndIsCalv : Migration
    {
        // This migration is written to be IDEMPOTENT and to run entirely with FK checks
        // suspended. Two hard constraints of MySQL/MariaDB drove this design:
        //
        //  * DDL is non-transactional and auto-commits every statement, so a migration that
        //    fails half-way leaves its earlier steps permanently applied while EF does NOT
        //    record the migration as done. The next deploy then re-runs the whole migration
        //    from the top and collides with the partial state (e.g. "Duplicate column 'Cow_ID'").
        //    Guarding every step against the current schema state makes re-runs self-healing.
        //
        //  * Databases created from the legacy 4Cows-DB-V3.sql script carry foreign keys on
        //    Cow(Ear_Tag_Number) (Cow_Treatment, Claw_Treatment, Planned_Cow_Treatment,
        //    Planned_Claw_Treatment) that the EF model does not know about. Dropping the Cow
        //    primary key while an FK references it fails with errno 150, so the whole swap runs
        //    with FOREIGN_KEY_CHECKS = 0; the unique index on Ear_Tag_Number is recreated so
        //    those FKs stay valid afterwards.
        //
        // The guarded logic lives in a temporary stored procedure (same technique Pomelo uses
        // for its own migration helpers). Procedure-local DECLARE variables avoid session user
        // variables, so this works regardless of the connection's AllowUserVariables setting.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS = 0;");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__ef_migrate_cow_id_up`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__ef_migrate_cow_id_up`()
BEGIN
    DECLARE v_has_cowid   INT DEFAULT 0;
    DECLARE v_cowid_is_pk INT DEFAULT 0;
    DECLARE v_has_any_pk  INT DEFAULT 0;
    DECLARE v_has_iscalv  INT DEFAULT 0;
    DECLARE v_has_idx     INT DEFAULT 0;

    -- 1. Add the stable identity column as NULLABLE first (a NOT NULL string column without a
    --    default cannot be added to a table that already has rows).
    SELECT COUNT(*) INTO v_has_cowid FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cow' AND COLUMN_NAME = 'Cow_ID';
    IF v_has_cowid = 0 THEN
        ALTER TABLE `Cow` ADD COLUMN `Cow_ID` varchar(64) CHARACTER SET utf8mb4 NULL;
    END IF;

    -- 2. Backfill: every existing cow is identified, so its stable id is its ear tag.
    UPDATE `Cow` SET `Cow_ID` = `Ear_Tag_Number` WHERE `Cow_ID` IS NULL;

    -- 3-6. Move the primary key from Ear_Tag_Number to Cow_ID, only if not already done.
    SELECT COUNT(*) INTO v_cowid_is_pk FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cow'
          AND INDEX_NAME = 'PRIMARY' AND COLUMN_NAME = 'Cow_ID';
    IF v_cowid_is_pk = 0 THEN
        SELECT COUNT(*) INTO v_has_any_pk FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cow' AND INDEX_NAME = 'PRIMARY';
        IF v_has_any_pk > 0 THEN
            ALTER TABLE `Cow` DROP PRIMARY KEY;
        END IF;
        -- Ear_Tag_Number becomes nullable: calves are registered with only a collar number.
        ALTER TABLE `Cow` MODIFY `Ear_Tag_Number` varchar(64) CHARACTER SET utf8mb4 NULL;
        ALTER TABLE `Cow` MODIFY `Cow_ID` varchar(64) CHARACTER SET utf8mb4 NOT NULL;
        ALTER TABLE `Cow` ADD PRIMARY KEY (`Cow_ID`);
    END IF;

    -- 7. Is_Calv flag; existing cows are all identified -> default false.
    SELECT COUNT(*) INTO v_has_iscalv FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cow' AND COLUMN_NAME = 'Is_Calv';
    IF v_has_iscalv = 0 THEN
        ALTER TABLE `Cow` ADD COLUMN `Is_Calv` tinyint(1) NOT NULL DEFAULT 0;
    END IF;

    -- 8. Unique index on Ear_Tag_Number. MySQL allows multiple NULLs in a unique index, so many
    --    tagless calves coexist while real ear tags remain unique. Also gives the legacy FKs an
    --    index to reference once the primary key has moved to Cow_ID.
    SELECT COUNT(*) INTO v_has_idx FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cow' AND INDEX_NAME = 'IX_Cow_Ear_Tag_Number';
    IF v_has_idx = 0 THEN
        CREATE UNIQUE INDEX `IX_Cow_Ear_Tag_Number` ON `Cow` (`Ear_Tag_Number`);
    END IF;
END;");

            migrationBuilder.Sql("CALL `__ef_migrate_cow_id_up`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__ef_migrate_cow_id_up`;");

            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS = 1;");

            // 9. Refresh already-seeded KPI scripts so the "most treatments" queries join the
            //    treatment key on the stable Cow_ID (fresh installs get this from DataSeeder, but
            //    the seeder skips non-empty KPI tables). Idempotent: REPLACE is a no-op once done.
            migrationBuilder.Sql(
                "UPDATE `KPI` SET `Script` = REPLACE(`Script`, 'c.Ear_Tag_Number', 'c.Cow_ID') " +
                "WHERE `Script` LIKE '%LEFT JOIN Cow c%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE `KPI` SET `Script` = REPLACE(`Script`, 'c.Cow_ID', 'c.Ear_Tag_Number') " +
                "WHERE `Script` LIKE '%LEFT JOIN Cow c%';");

            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS = 0;");

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__ef_migrate_cow_id_down`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__ef_migrate_cow_id_down`()
BEGIN
    DECLARE v_has_idx      INT DEFAULT 0;
    DECLARE v_has_iscalv   INT DEFAULT 0;
    DECLARE v_eartag_is_pk INT DEFAULT 0;
    DECLARE v_has_any_pk   INT DEFAULT 0;
    DECLARE v_has_cowid    INT DEFAULT 0;

    SELECT COUNT(*) INTO v_has_idx FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cow' AND INDEX_NAME = 'IX_Cow_Ear_Tag_Number';
    IF v_has_idx > 0 THEN
        DROP INDEX `IX_Cow_Ear_Tag_Number` ON `Cow`;
    END IF;

    SELECT COUNT(*) INTO v_has_iscalv FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cow' AND COLUMN_NAME = 'Is_Calv';
    IF v_has_iscalv > 0 THEN
        ALTER TABLE `Cow` DROP COLUMN `Is_Calv`;
    END IF;

    -- Move the primary key back to Ear_Tag_Number, only if not already there.
    SELECT COUNT(*) INTO v_eartag_is_pk FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cow'
          AND INDEX_NAME = 'PRIMARY' AND COLUMN_NAME = 'Ear_Tag_Number';
    IF v_eartag_is_pk = 0 THEN
        SELECT COUNT(*) INTO v_has_any_pk FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cow' AND INDEX_NAME = 'PRIMARY';
        IF v_has_any_pk > 0 THEN
            ALTER TABLE `Cow` DROP PRIMARY KEY;
        END IF;
        -- Calves have no ear tag and cannot exist once Ear_Tag_Number is the PK again.
        DELETE FROM `Cow` WHERE `Ear_Tag_Number` IS NULL;
        ALTER TABLE `Cow` MODIFY `Ear_Tag_Number` varchar(64) CHARACTER SET utf8mb4 NOT NULL;
        ALTER TABLE `Cow` ADD PRIMARY KEY (`Ear_Tag_Number`);
    END IF;

    SELECT COUNT(*) INTO v_has_cowid FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cow' AND COLUMN_NAME = 'Cow_ID';
    IF v_has_cowid > 0 THEN
        ALTER TABLE `Cow` DROP COLUMN `Cow_ID`;
    END IF;
END;");

            migrationBuilder.Sql("CALL `__ef_migrate_cow_id_down`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__ef_migrate_cow_id_down`;");

            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS = 1;");
        }
    }
}
