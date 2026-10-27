using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Meadow.Shared.Models;

/// <summary>
/// Manueller Abbruch eines Behandlungsplan-Durchlaufs. Durchlaeufe selbst
/// werden nicht gespeichert (siehe <see cref="TreatmentPlanRuns"/>); der Abbruch
/// ist das Einzige, was sich nicht aus den Behandlungen ableiten laesst.
///
/// Der Durchlauf wird ueber die ClientId seiner ERSTEN Gabe identifiziert -
/// die ist stabil, schon bevor die Zeile den Server je gesehen hat.
/// </summary>
[Table("Treatment_Plan_Cancellation")]
public class TreatmentPlanCancellation
{
    public const int ReasonMaxLength = 190;

    [Key]
    [Column("Treatment_Plan_Cancellation_ID")]
    public int TreatmentPlanCancellationId { get; set; }

    [Required]
    [Column("Cow_ID")]
    public string CowId { get; set; } = string.Empty;

    [Required]
    [Column("Medicine_ID")]
    public int MedicineId { get; set; }

    [Required]
    [Column("Start_Treatment_Client_Id")]
    public Guid StartTreatmentClientId { get; set; }

    [Required]
    [Column("Cancelled_On")]
    public DateTime CancelledOn { get; set; }

    [StringLength(ReasonMaxLength)]
    [Column("Reason")]
    public string? Reason { get; set; }
}
