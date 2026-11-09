using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meadow.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyEarTagForeignKeys : Migration
    {
        // Datenbanken aus dem alten Skript docs/sql/4Cows-DB-V3.sql tragen Fremdschluessel
        // Cow_Treatment/Claw_Treatment/Planned_Cow_Treatment/Planned_Claw_Treatment
        // (Ear_Tag_Number) -> Cow(Ear_Tag_Number). Das EF-Modell kennt sie nicht, eine frisch
        // migrierte Datenbank hat sie nicht. Seit AddCowIdAndIsCalv steht in der Spalte
        // "Ear_Tag_Number" der Behandlungstabellen aber die Cow_ID - die Schluessel zeigen also
        // auf die falsche Spalte:
        //
        //  * jede Aenderung der Ohrmarke einer Kuh mit Behandlungen scheitert ("Cannot delete or
        //    update a parent row"), allen voran der Horizon-Abgleich, fuer den Horizon bei der
        //    Ohrmarke fuehrend ist;
        //  * eine Behandlung fuer ein Kalb (Cow_ID = GUID, keine Ohrmarke) verletzt sie ebenfalls.
        //
        // Deshalb fallen sie weg; danach verhaelt sich die Altdatenbank wie eine frische.
        // Gesucht wird nach dem Ziel (Cow.Ear_Tag_Number), nicht nach Namen wie
        // "Cow_Treatment_ibfk_1" - die vergibt der Server und sie koennen je Datenbank anders
        // heissen. Idempotent: ohne passende Schluessel tut die Prozedur nichts.
        // EXECUTE IMMEDIATE mit lokaler Variable statt PREPARE mit @-Variable, damit es
        // unabhaengig von AllowUserVariables laeuft (wie in AddCowIdAndIsCalv).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__ef_drop_legacy_eartag_fks`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `__ef_drop_legacy_eartag_fks`()
BEGIN
    DECLARE v_done  INT DEFAULT 0;
    DECLARE v_table VARCHAR(64);
    DECLARE v_name  VARCHAR(64);
    DECLARE v_sql   VARCHAR(512);
    DECLARE fk_cursor CURSOR FOR
        SELECT DISTINCT TABLE_NAME, CONSTRAINT_NAME
        FROM information_schema.KEY_COLUMN_USAGE
        WHERE TABLE_SCHEMA = DATABASE()
          AND REFERENCED_TABLE_SCHEMA = DATABASE()
          AND REFERENCED_TABLE_NAME = 'Cow'
          AND REFERENCED_COLUMN_NAME = 'Ear_Tag_Number';
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1;

    OPEN fk_cursor;
    drop_loop: LOOP
        FETCH fk_cursor INTO v_table, v_name;
        IF v_done = 1 THEN
            LEAVE drop_loop;
        END IF;
        SET v_sql = CONCAT('ALTER TABLE `', v_table, '` DROP FOREIGN KEY `', v_name, '`');
        EXECUTE IMMEDIATE v_sql;
    END LOOP;
    CLOSE fk_cursor;
END;");
            migrationBuilder.Sql("CALL `__ef_drop_legacy_eartag_fks`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `__ef_drop_legacy_eartag_fks`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Absichtlich leer: die Schluessel zeigen auf die falsche Spalte und wuerden auf
            // einer Datenbank mit Kaelbern oder geaenderten Ohrmarken nicht mehr anlegbar sein.
        }
    }
}
