using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meadow.Data.Migrations
{
    /// <summary>
    /// Der zweite Schluessel der vier offline schreibbaren Tabellen.
    ///
    /// Die Vorlage, die "dotnet ef migrations add" erzeugt hat, laesst sich auf
    /// Bestandsdaten NICHT anwenden: sie haengt die Spalte mit
    /// defaultValue Guid.Empty an und legt direkt danach den eindeutigen Index
    /// an. Alle vorhandenen Zeilen tragen dann denselben Wert. Gegen die
    /// Dev-Datenbank ausprobiert, 121 Zeilen:
    ///
    ///   ERROR 1062 (23000): Duplicate entry
    ///   '00000000-0000-0000-0000-000000000000' for key
    ///   'IX_Cow_Treatment_Client_Id'
    ///
    /// Deshalb vier Schritte je Tabelle: Spalte NULLABLE anhaengen, mit UUID()
    /// je Zeile befuellen, auf NOT NULL ziehen, dann erst den Index.
    ///
    /// UUID() ist MariaDB-eigen. Das passt zu diesem Migrationsordner, in dem
    /// schon SET FOREIGN_KEY_CHECKS und ALTER TABLE ... AUTO_INCREMENT stehen.
    /// Bei binlog_format = MIXED (Vorgabe hier) wird die Anweisung
    /// zeilenbasiert protokolliert und ist damit replikationssicher; auf einem
    /// Replikat mit STATEMENT-Protokoll waere sie es nicht.
    ///
    /// Neue Zeilen brauchen den Nachtrag nicht: ClientId ist am Modell ein
    /// Property-Initialisierer und laeuft bei jedem Konstruktor, auch beim
    /// positionalen des Seeders.
    /// </summary>
    public partial class AddClientId : Migration
    {
        private static readonly string[] Tables =
        {
            "Cow_Treatment",
            "Claw_Treatment",
            "Planned_Cow_Treatment",
            "Planned_Claw_Treatment"
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "Client_Id",
                    table: table,
                    type: "char(36)",
                    nullable: true,
                    collation: "ascii_general_ci")
                    .Annotation("MySql:CharSet", "ascii");

                // Je Zeile ein eigener Wert. UUID() wird pro Zeile ausgewertet -
                // gegengeprueft mit
                //   SELECT COUNT(*), COUNT(DISTINCT Client_Id) FROM <t>;
                migrationBuilder.Sql(
                    $"UPDATE `{table}` SET `Client_Id` = UUID() WHERE `Client_Id` IS NULL;");

                migrationBuilder.AlterColumn<Guid>(
                    name: "Client_Id",
                    table: table,
                    type: "char(36)",
                    nullable: false,
                    collation: "ascii_general_ci",
                    oldClrType: typeof(Guid),
                    oldType: "char(36)",
                    oldNullable: true)
                    .Annotation("MySql:CharSet", "ascii");

                migrationBuilder.CreateIndex(
                    name: $"IX_{table}_Client_Id",
                    table: table,
                    column: "Client_Id",
                    unique: true);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.DropIndex(name: $"IX_{table}_Client_Id", table: table);
                migrationBuilder.DropColumn(name: "Client_Id", table: table);
            }
        }
    }
}
