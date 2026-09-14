using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class;

/// <summary>
/// Befund einer Klaue, z.B. "Mortellaro" oder "Sohlengeschwuer".
///
/// Aufgebaut wie <see cref="TreatmentReason"/>: eine reine Nachschlagetabelle,
/// deren Eintraege ueberwiegend nebenbei im Klauenbehandlungs-Dialog entstehen
/// und auf der Basisdaten-Seite gepflegt werden.
///
/// varchar(64) statt der frueheren varchar(32)-Spalten auf Claw_Treatment:
/// laengere Formulierungen ("Weisse-Linie-Defekt, tiefgehend") kippten dort den
/// Insert. Ohne StringLength macht Pomelo daraus longtext, was WhereHow_Name
/// bis heute mitschleppt.
/// </summary>
[Table("Claw_Finding")]
public class ClawFinding
{
    /// <summary>
    /// Laengengrenze von <see cref="ClawFindingName"/>. Steht als Konstante
    /// hier, weil der Merge den ueberlebenden Namen darauf kuerzt - wie
    /// <see cref="Medicine.NameMaxLength"/>.
    /// </summary>
    public const int NameMaxLength = 64;

    [Key]
    [Column("Claw_Finding_ID")]
    public int ClawFindingId { get; set; }

    [Required]
    [StringLength(NameMaxLength)]
    [Column("Claw_Finding_Name")]
    public string ClawFindingName { get; set; }

    public ClawFinding() : this(0, string.Empty) { }

    public ClawFinding(int clawFindingId, string clawFindingName)
    {
        ClawFindingId = clawFindingId;
        ClawFindingName = clawFindingName;
    }
}
