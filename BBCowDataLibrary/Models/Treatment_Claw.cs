using System.ComponentModel.DataAnnotations;

namespace BB_Cow.Class
{
  public class ClawTreatment
    {
        [Key]
        public int ClawTreatmentId { get; set; }

        [Required]
        public string EarTagNumber { get; set; }

        [Required]
        public DateTime TreatmentDate { get; set; }

        [Required]
        [StringLength(32)]
        public string ClawFindingLV { get; set; }

        [Required]
        public bool BandageLV { get; set; }

        [Required]
        public bool BlockLV { get; set; }

        [Required]
        [StringLength(32)]
        public string ClawFindingLH { get; set; }

        [Required]
        public bool BandageLH { get; set; }

        [Required]
        public bool BlockLH { get; set; }

        [Required]
        [StringLength(32)]
        public string ClawFindingRV { get; set; }

        [Required]
        public bool BandageRV { get; set; }

        [Required]
        public bool BlockRV { get; set; }

        [Required]
        [StringLength(32)]
        public string ClawFindingRH { get; set; }

        [Required]
        public bool BandageRH { get; set; }

        [Required]
        public bool BlockRH { get; set; }

        [Required]
        public bool IsBandageRemoved { get; set; }

        public ClawTreatment() : this(0, string.Empty, DateTime.MinValue, string.Empty, false, false, string.Empty, false, false, string.Empty, false, false, string.Empty, false, false, false) { }

        public ClawTreatment(int clawTreatmentId, string earTagNumber, DateTime treatmentDate, string clawFindingLV, bool bandageLV, bool blockLV, string clawFindingLH, bool bandageLH, bool blockLH, string clawFindingRV, bool bandageRV, bool blockRV, string clawFindingRH, bool bandageRH, bool blockRH, bool isBandageRemoved)
        {
            ClawTreatmentId = clawTreatmentId;
            EarTagNumber = earTagNumber;
            TreatmentDate = treatmentDate;
            ClawFindingLV = clawFindingLV;
            BandageLV = bandageLV;
            BlockLV = blockLV;
            ClawFindingLH = clawFindingLH;
            BandageLH = bandageLH;
            BlockLH = blockLH;
            ClawFindingRV = clawFindingRV;
            BandageRV = bandageRV;
            BlockRV = blockRV;
            ClawFindingRH = clawFindingRH;
            BandageRH = bandageRH;
            BlockRH = blockRH;
            IsBandageRemoved = isBandageRemoved;
        }
    }
}
