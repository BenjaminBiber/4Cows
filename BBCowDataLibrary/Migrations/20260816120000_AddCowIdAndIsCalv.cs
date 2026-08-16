using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBCowDataLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddCowIdAndIsCalv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the new stable identity column as NULLABLE first, so it can be added to a
            //    table that already has rows (a NOT NULL string column without a default would fail).
            migrationBuilder.AddColumn<string>(
                name: "Cow_ID",
                table: "Cow",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // 2. Backfill: every existing cow is identified, so its stable id is its ear tag.
            migrationBuilder.Sql("UPDATE `Cow` SET `Cow_ID` = `Ear_Tag_Number` WHERE `Cow_ID` IS NULL;");

            // 3. Drop the old primary key on Ear_Tag_Number (needed before it can become nullable).
            migrationBuilder.DropPrimaryKey(
                name: "PK_Cow",
                table: "Cow");

            // 4. Ear_Tag_Number becomes nullable: calves are registered with only a collar number.
            migrationBuilder.AlterColumn<string>(
                name: "Ear_Tag_Number",
                table: "Cow",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // 5. Cow_ID is fully backfilled -> make it NOT NULL.
            migrationBuilder.AlterColumn<string>(
                name: "Cow_ID",
                table: "Cow",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // 6. New primary key on the stable Cow_ID.
            migrationBuilder.AddPrimaryKey(
                name: "PK_Cow",
                table: "Cow",
                column: "Cow_ID");

            // 7. Is_Calv flag; existing cows are all identified -> default false.
            migrationBuilder.AddColumn<bool>(
                name: "Is_Calv",
                table: "Cow",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // 8. Unique index on Ear_Tag_Number. MySQL allows multiple NULLs in a unique index,
            //    so many tagless calves coexist while real ear tags remain unique.
            migrationBuilder.CreateIndex(
                name: "IX_Cow_Ear_Tag_Number",
                table: "Cow",
                column: "Ear_Tag_Number",
                unique: true);

            // 9. Refresh already-seeded KPI scripts so the "most treatments" queries join the
            //    treatment key on the stable Cow_ID (fresh installs get this from DataSeeder, but
            //    the seeder skips non-empty KPI tables). Only touches the Cow-alias side; the
            //    treatment column ct.Ear_Tag_Number is intentionally left unchanged.
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

            migrationBuilder.DropIndex(
                name: "IX_Cow_Ear_Tag_Number",
                table: "Cow");

            migrationBuilder.DropColumn(
                name: "Is_Calv",
                table: "Cow");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Cow",
                table: "Cow");

            // Calves have no ear tag and cannot exist once Ear_Tag_Number is the PK again.
            migrationBuilder.Sql("DELETE FROM `Cow` WHERE `Ear_Tag_Number` IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Ear_Tag_Number",
                table: "Cow",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Cow",
                table: "Cow",
                column: "Ear_Tag_Number");

            migrationBuilder.DropColumn(
                name: "Cow_ID",
                table: "Cow");
        }
    }
}
