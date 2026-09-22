using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meadow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPushReminderLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PushReminderLog",
                columns: table => new
                {
                    Claw_Treatment_ID = table.Column<int>(type: "int", nullable: false),
                    SentOn = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushReminderLog", x => x.Claw_Treatment_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushReminderLog");
        }
    }
}
