using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBCowDataLibrary.Migrations
{
    /// <summary>
    /// Lagert die Klauenbefunde aus vier varchar(32)-Freitextspalten auf
    /// Claw_Treatment in die Nachschlagetabelle Claw_Finding aus.
    ///
    /// Die Reihenfolge in Up ist tragend: Tabelle und ID-Spalten zuerst, dann
    /// der Bestand, erst danach fallen die Textspalten. Wer die DropColumn
    /// nach oben zieht, verliert alle Befunde.
    ///
    /// Bewusst ohne Fremdschluessel - das Schema aus InitialCreate legt
    /// nirgends welche an, ClawFindingService prueft beim Loeschen selbst nach.
    /// </summary>
    public partial class AddClawFinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Claw_Finding",
                columns: table => new
                {
                    Claw_Finding_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Claw_Finding_Name = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claw_Finding", x => x.Claw_Finding_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            foreach (var position in new[] { "LV", "LH", "RV", "RH" })
            {
                migrationBuilder.AddColumn<int>(
                    name: $"Claw_Finding_{position}_ID",
                    table: "Claw_Treatment",
                    type: "int",
                    nullable: true);
            }

            // Jeden vorhandenen Befund einmal anlegen. Gruppiert wird ohne
            // Ruecksicht auf Gross- und Kleinschreibung - genau so hat die
            // Anwendung Befunde immer schon verglichen (ClawFindingSummary,
            // CowProfileBuilder). Es gewinnt die haeufigste Schreibweise, bei
            // Gleichstand die binaer kleinste, damit der Lauf reproduzierbar
            // ist.
            //
            // Leere Zellen fallen hier heraus und bleiben unten NULL: leer
            // heisst "an dieser Klaue wurde nichts erfasst" und ist kein
            // Befund. Eine Zeile dafuer stuende sonst als haeufigster
            // Klauenbefund auf jeder Kuh-Kachel.
            migrationBuilder.Sql(@"
INSERT INTO Claw_Finding (Claw_Finding_Name)
SELECT name
  FROM (SELECT name,
               ROW_NUMBER() OVER (PARTITION BY LOWER(name) ORDER BY n DESC, BINARY name ASC) AS rn
          FROM (SELECT name, COUNT(*) AS n
                  FROM (          SELECT TRIM(Claw_Finding_LV) AS name FROM Claw_Treatment
                        UNION ALL SELECT TRIM(Claw_Finding_LH)         FROM Claw_Treatment
                        UNION ALL SELECT TRIM(Claw_Finding_RV)         FROM Claw_Treatment
                        UNION ALL SELECT TRIM(Claw_Finding_RH)         FROM Claw_Treatment) cells
                 WHERE name <> ''
                 GROUP BY BINARY name) spellings) ranked
 WHERE rn = 1;");

            // Der Vergleich laeuft ueber die Standard-Kollation und ist damit
            // case-insensitiv: "mortellaro" findet die Zeile "Mortellaro". Das
            // IST das Buendeln der Schreibweisen.
            foreach (var position in new[] { "LV", "LH", "RV", "RH" })
            {
                migrationBuilder.Sql($@"
UPDATE Claw_Treatment ct
  JOIN Claw_Finding f ON f.Claw_Finding_Name = TRIM(ct.Claw_Finding_{position})
   SET ct.Claw_Finding_{position}_ID = f.Claw_Finding_ID
 WHERE TRIM(ct.Claw_Finding_{position}) <> '';");
            }

            foreach (var position in new[] { "LV", "LH", "RV", "RH" })
            {
                migrationBuilder.DropColumn(
                    name: $"Claw_Finding_{position}",
                    table: "Claw_Treatment");
            }
        }

        /// <summary>
        /// Zurueck auf Freitext. Befundnamen ueber 32 Zeichen werden dabei
        /// gekuerzt - die alten Spalten sind varchar(32), die neue Tabelle
        /// erlaubt 64. Ein Rueckbau nach laengeren Eingaben ist also nicht
        /// verlustfrei.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var position in new[] { "LV", "LH", "RV", "RH" })
            {
                migrationBuilder.AddColumn<string>(
                    name: $"Claw_Finding_{position}",
                    table: "Claw_Treatment",
                    type: "varchar(32)",
                    maxLength: 32,
                    nullable: false,
                    defaultValue: "")
                    .Annotation("MySql:CharSet", "utf8mb4");
            }

            foreach (var position in new[] { "LV", "LH", "RV", "RH" })
            {
                migrationBuilder.Sql($@"
UPDATE Claw_Treatment ct
  JOIN Claw_Finding f ON f.Claw_Finding_ID = ct.Claw_Finding_{position}_ID
   SET ct.Claw_Finding_{position} = LEFT(f.Claw_Finding_Name, 32);");
            }

            foreach (var position in new[] { "LV", "LH", "RV", "RH" })
            {
                migrationBuilder.DropColumn(
                    name: $"Claw_Finding_{position}_ID",
                    table: "Claw_Treatment");
            }

            migrationBuilder.DropTable(
                name: "Claw_Finding");
        }
    }
}
