using System.ComponentModel.DataAnnotations;

namespace BB_Cow.Class
{
    public class PlannedClawTreatment
    {
        [Key]
        public int PlannedClawTreatmentId { get; set; }

        [Required]
        public string EarTagNumber { get; set; }

        [Required]
        public DateTime TreatmentDate { get; set; }

        public string Desciption { get; set; }

        [Required]
        public bool ClawFindingLV { get; set; }

        [Required]
        public bool ClawFindingLH { get; set; }

        [Required]
        public bool ClawFindingRV { get; set; }

        [Required]
        public bool ClawFindingRH { get; set; }

        public PlannedClawTreatment() : this(0, string.Empty, DateTime.MinValue, string.Empty, false, false, false, false) { }

        public PlannedClawTreatment(int plannedClawTreatmentId, string earTagNumber, DateTime treatmentDate, string desciption, bool clawFindingLV, bool clawFindingLH, bool clawFindingRV, bool clawFindingRH)
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
