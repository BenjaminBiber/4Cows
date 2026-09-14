using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Meadow.Shared.Models;

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

    /// <summary>
    /// Ergebnis von ClawFindingService.GetIdByNameAsync, wenn der Befund nicht
    /// angelegt werden konnte. <c>null</c> ist dort KEIN Fehler, sondern der
    /// regulaere Fall "keine Eingabe" - deshalb braucht das Scheitern einen
    /// eigenen Wert, wie in TreatmentReasonService.
    ///
    /// Steht am MODELL und nicht am Dienst, obwohl sie sein Ergebnis
    /// beschreibt: sobald die Komponenten den Dienst als IClawFindingService
    /// einspritzen, bindet der einfache Name ClawFindingService im
    /// Klauenbehandlungs-Dialog an das FELD, nicht mehr an den Typ (heute
    /// traegt die "Color Color"-Regel das, weil Feld und Typ gleich heissen).
    /// Eine Konstante ist kein Schnittstellen-Member, der Zugriff waere dann
    /// CS1061 - und zwar erst im naechsten Commit, wo niemand mehr danach
    /// sucht. Am Modell steht sie allen Implementierungen der Naht
    /// gleichermassen zur Verfuegung.
    /// </summary>
    public const int FailedId = int.MinValue;

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
