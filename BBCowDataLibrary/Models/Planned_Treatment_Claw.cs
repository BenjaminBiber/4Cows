using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class
{
    [Table("Planned_Claw_Treatment")]
    public class PlannedClawTreatment
    {
        [Key]
        [Column("Planned_Claw_Treatment_ID")]
        public int PlannedClawTreatmentId { get; set; }

        [Required]
        [Column("Ear_Tag_Number")]
        public string EarTagNumber { get; set; }

        [Required]
        [Column("Treatment_Date")]
        public DateTime TreatmentDate { get; set; }

        [Column("Desciption")]
        public string? Desciption { get; set; }

        [Required]
        [Column("Claw_Finding_LV")]
        public bool ClawFindingLV { get; set; }

        [Required]
        [Column("Claw_Finding_LH")]
        public bool ClawFindingLH { get; set; }

        [Required]
        [Column("Claw_Finding_RV")]
        public bool ClawFindingRV { get; set; }

        [Required]
        [Column("Claw_Finding_RH")]
        public bool ClawFindingRH { get; set; }

        public PlannedClawTreatment() : this(0, string.Empty, DateTime.MinValue, string.Empty, false, false, false, false) { }

        public PlannedClawTreatment(int plannedClawTreatmentId, string earTagNumber, DateTime treatmentDate, string? desciption, bool clawFindingLV, bool clawFindingLH, bool clawFindingRV, bool clawFindingRH)
        {
            PlannedClawTreatmentId = plannedClawTreatmentId;
            EarTagNumber = earTagNumber;
            TreatmentDate = treatmentDate;
            Desciption = desciption;
            ClawFindingLV = clawFindingLV;
            ClawFindingLH = clawFindingLH;
            ClawFindingRV = clawFindingRV;
            ClawFindingRH = clawFindingRH;
        }
    }
}
