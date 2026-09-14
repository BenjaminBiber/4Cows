using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBCowDataLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddTreatmentReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Treatment_Reason_ID",
                table: "Planned_Cow_Treatment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Treatment_Reason_ID",
                table: "Cow_Treatment",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Treatment_Reason",
                columns: table => new
                {
                    Treatment_Reason_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Treatment_Reason_Name = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treatment_Reason", x => x.Treatment_Reason_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Treatment_Reason");

            migrationBuilder.DropColumn(
                name: "Treatment_Reason_ID",
                table: "Planned_Cow_Treatment");

            migrationBuilder.DropColumn(
                name: "Treatment_Reason_ID",
                table: "Cow_Treatment");
        }
    }
}
