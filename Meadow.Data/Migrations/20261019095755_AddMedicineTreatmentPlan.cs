using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meadow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicineTreatmentPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Plan_Days",
                table: "Medicine",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Plan_Dose_Count",
                table: "Medicine",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Plan_Mode",
                table: "Medicine",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Plan_Valid_From",
                table: "Medicine",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Plan_Days",
                table: "Medicine");

            migrationBuilder.DropColumn(
                name: "Plan_Dose_Count",
                table: "Medicine");

            migrationBuilder.DropColumn(
                name: "Plan_Mode",
                table: "Medicine");

            migrationBuilder.DropColumn(
                name: "Plan_Valid_From",
                table: "Medicine");
        }
    }
}
