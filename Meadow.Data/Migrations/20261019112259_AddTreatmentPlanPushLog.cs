using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meadow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTreatmentPlanPushLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Treatment_Plan_Push_Log",
                columns: table => new
                {
                    Treatment_Plan_Push_Log_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Sent_On = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treatment_Plan_Push_Log", x => x.Treatment_Plan_Push_Log_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Treatment_Plan_Push_Log_Sent_On",
                table: "Treatment_Plan_Push_Log",
                column: "Sent_On",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Treatment_Plan_Push_Log");
        }
    }
}
