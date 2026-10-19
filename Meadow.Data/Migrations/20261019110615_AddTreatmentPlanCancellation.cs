using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meadow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTreatmentPlanCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Treatment_Plan_Cancellation",
                columns: table => new
                {
                    Treatment_Plan_Cancellation_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Cow_ID = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Medicine_ID = table.Column<int>(type: "int", nullable: false),
                    Start_Treatment_Client_Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                        .Annotation("MySql:CharSet", "ascii"),
                    Cancelled_On = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(190)", maxLength: 190, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treatment_Plan_Cancellation", x => x.Treatment_Plan_Cancellation_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Treatment_Plan_Cancellation_Start_Treatment_Client_Id",
                table: "Treatment_Plan_Cancellation",
                column: "Start_Treatment_Client_Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Treatment_Plan_Cancellation");
        }
    }
}
