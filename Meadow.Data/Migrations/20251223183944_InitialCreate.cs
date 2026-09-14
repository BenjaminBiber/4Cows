using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBCowDataLibrary.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Claw_Treatment",
                columns: table => new
                {
                    Claw_Treatment_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ear_Tag_Number = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Treatment_Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Claw_Finding_LV = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bandage_LV = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Block_LV = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Claw_Finding_LH = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bandage_LH = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Block_LH = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Claw_Finding_RV = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bandage_RV = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Block_RV = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Claw_Finding_RH = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bandage_RH = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Block_RH = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsBandageRemoved = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claw_Treatment", x => x.Claw_Treatment_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Cow",
                columns: table => new
                {
                    Ear_Tag_Number = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Collar_Number = table.Column<int>(type: "int", nullable: false),
                    IsGone = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cow", x => x.Ear_Tag_Number);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Cow_Treatment",
                columns: table => new
                {
                    Cow_Treatment_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ear_Tag_Number = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Medicine_ID = table.Column<int>(type: "int", nullable: false),
                    Administration_Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Medicine_Dosage = table.Column<float>(type: "float", nullable: false),
                    WhereHow_ID = table.Column<int>(type: "int", nullable: false),
                    COW_QUARTER_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cow_Treatment", x => x.Cow_Treatment_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "KPI",
                columns: table => new
                {
                    KPI_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Url = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Script = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sort_Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KPI", x => x.KPI_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Medicine",
                columns: table => new
                {
                    Medicine_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Medicine_Name = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicine", x => x.Medicine_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Planned_Claw_Treatment",
                columns: table => new
                {
                    Planned_Claw_Treatment_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ear_Tag_Number = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Treatment_Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Desciption = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Claw_Finding_LV = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Claw_Finding_LH = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Claw_Finding_RV = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Claw_Finding_RH = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planned_Claw_Treatment", x => x.Planned_Claw_Treatment_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Planned_Cow_Treatment",
                columns: table => new
                {
                    Planned_Cow_Treatment_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ear_Tag_Number = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Medicine_ID = table.Column<int>(type: "int", nullable: false),
                    Administration_Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Medicine_Dosage = table.Column<float>(type: "float", nullable: false),
                    WhereHow_ID = table.Column<int>(type: "int", nullable: false),
                    IsFound = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsTreatet = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Udder_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planned_Cow_Treatment", x => x.Planned_Cow_Treatment_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Udder",
                columns: table => new
                {
                    UDDER_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Quarter_LV = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Quarter_LH = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Quarter_RV = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Quarter_RH = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Udder", x => x.UDDER_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WhereHow",
                columns: table => new
                {
                    WhereHow_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WhereHow_Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShowDialog = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhereHow", x => x.WhereHow_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Claw_Treatment");

            migrationBuilder.DropTable(
                name: "Cow");

            migrationBuilder.DropTable(
                name: "Cow_Treatment");

            migrationBuilder.DropTable(
                name: "KPI");

            migrationBuilder.DropTable(
                name: "Medicine");

            migrationBuilder.DropTable(
                name: "Planned_Claw_Treatment");

            migrationBuilder.DropTable(
                name: "Planned_Cow_Treatment");

            migrationBuilder.DropTable(
                name: "Udder");

            migrationBuilder.DropTable(
                name: "WhereHow");
        }
    }
}
