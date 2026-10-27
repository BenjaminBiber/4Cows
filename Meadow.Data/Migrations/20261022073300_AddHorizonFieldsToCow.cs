using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meadow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHorizonFieldsToCow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: MariaDB-DDL committet je Anweisung, ein Abbruch in der Mitte
            // liesse sonst einen Teilstand zurueck, an dem der naechste Start scheitert.
            migrationBuilder.Sql("ALTER TABLE `Cow` ADD COLUMN IF NOT EXISTS `Lely_Animal_Id` varchar(64) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `Cow` ADD COLUMN IF NOT EXISTS `Location` varchar(100) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `Cow` ADD COLUMN IF NOT EXISTS `Group_Name` varchar(100) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `Cow` ADD COLUMN IF NOT EXISTS `Description` varchar(500) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_Cow_Lely_Animal_Id` ON `Cow` (`Lely_Animal_Id`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_Cow_Lely_Animal_Id` ON `Cow`;");
            migrationBuilder.Sql("ALTER TABLE `Cow` DROP COLUMN IF EXISTS `Description`;");
            migrationBuilder.Sql("ALTER TABLE `Cow` DROP COLUMN IF EXISTS `Group_Name`;");
            migrationBuilder.Sql("ALTER TABLE `Cow` DROP COLUMN IF EXISTS `Location`;");
            migrationBuilder.Sql("ALTER TABLE `Cow` DROP COLUMN IF EXISTS `Lely_Animal_Id`;");
        }
    }
}
