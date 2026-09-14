using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBCowDataLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicineDosageUnitAndDefaultWhereHow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Medicine_Name",
                table: "Medicine",
                type: "varchar(190)",
                maxLength: 190,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Default_WhereHow_ID",
                table: "Medicine",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dosage_Unit",
                table: "Medicine",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Default_WhereHow_ID",
                table: "Medicine");

            migrationBuilder.DropColumn(
                name: "Dosage_Unit",
                table: "Medicine");

            migrationBuilder.AlterColumn<string>(
                name: "Medicine_Name",
                table: "Medicine",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(190)",
                oldMaxLength: 190)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
