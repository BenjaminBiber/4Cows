using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class;

/// <summary>
/// Behandlungsgrund einer Kuh- oder geplanten Kuhbehandlung.
///
/// Aufgebaut wie <see cref="Medicine"/>: eine reine Nachschlagetabelle, deren
/// Eintraege ueberwiegend nebenbei im Behandlungs-Dialog entstehen.
/// varchar(64) wie Medicine_Name - ohne StringLength macht Pomelo daraus
/// longtext, was WhereHow_Name bis heute mitschleppt.
/// </summary>
[Table("Treatment_Reason")]
public class TreatmentReason
{
    [Key]
    [Column("Treatment_Reason_ID")]
    public int TreatmentReasonId { get; set; }

    [Required]
    [StringLength(64)]
    [Column("Treatment_Reason_Name")]
    public string TreatmentReasonName { get; set; }

    public TreatmentReason() : this(0, string.Empty) { }

    public TreatmentReason(int treatmentReasonId, string treatmentReasonName)
    {
        TreatmentReasonId = treatmentReasonId;
        TreatmentReasonName = treatmentReasonName;
    }
}
