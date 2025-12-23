using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class
{
  [Table("Claw_Treatment")]
  public class ClawTreatment
    {
        [Key]
        [Column("Claw_Treatment_ID")]
        public int ClawTreatmentId { get; set; }

        [Required]
        [Column("Ear_Tag_Number")]
        public string EarTagNumber { get; set; }

        [Required]
        [Column("Treatment_Date")]
        public DateTime TreatmentDate { get; set; }

        [Required]
        [StringLength(32)]
        [Column("Claw_Finding_LV")]
        public string ClawFindingLV { get; set; }

        [Required]
        [Column("Bandage_LV")]
        public bool BandageLV { get; set; }

        [Required]
        [Column("Block_LV")]
        public bool BlockLV { get; set; }

        [Required]
        [StringLength(32)]
        [Column("Claw_Finding_LH")]
        public string ClawFindingLH { get; set; }

        [Required]
        [Column("Bandage_LH")]
        public bool BandageLH { get; set; }

        [Required]
        [Column("Block_LH")]
        public bool BlockLH { get; set; }

        [Required]
        [StringLength(32)]
        [Column("Claw_Finding_RV")]
        public string ClawFindingRV { get; set; }

        [Required]
        [Column("Bandage_RV")]
        public bool BandageRV { get; set; }

        [Required]
        [Column("Block_RV")]
        public bool BlockRV { get; set; }

        [Required]
        [StringLength(32)]
        [Column("Claw_Finding_RH")]
        public string ClawFindingRH { get; set; }

        [Required]
        [Column("Bandage_RH")]
        public bool BandageRH { get; set; }

        [Required]
        [Column("Block_RH")]
        public bool BlockRH { get; set; }

        [Required]
        [Column("IsBandageRemoved")]
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
