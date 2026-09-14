using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Meadow.Shared.Models
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

        /// <summary>
        /// Befund dieser Klaue, aufgeloest ueber ClawFindingService.
        ///
        /// <c>null</c> heisst "an dieser Klaue wurde nichts erfasst" - bis zur
        /// Migration AddClawFinding stand dafuer der Leerstring in einer
        /// varchar(32)-Spalte. Bewusst ohne Fremdschluessel: das Schema aus
        /// 20251223183944_InitialCreate legt nirgends welche an, der Service
        /// prueft beim Loeschen selbst nach.
        /// </summary>
        [Column("Claw_Finding_LV_ID")]
        public int? ClawFindingLVId { get; set; }

        [Required]
        [Column("Bandage_LV")]
        public bool BandageLV { get; set; }

        [Required]
        [Column("Block_LV")]
        public bool BlockLV { get; set; }

        [Column("Claw_Finding_LH_ID")]
        public int? ClawFindingLHId { get; set; }

        [Required]
        [Column("Bandage_LH")]
        public bool BandageLH { get; set; }

        [Required]
        [Column("Block_LH")]
        public bool BlockLH { get; set; }

        [Column("Claw_Finding_RV_ID")]
        public int? ClawFindingRVId { get; set; }

        [Required]
        [Column("Bandage_RV")]
        public bool BandageRV { get; set; }

        [Required]
        [Column("Block_RV")]
        public bool BlockRV { get; set; }

        [Column("Claw_Finding_RH_ID")]
        public int? ClawFindingRHId { get; set; }

        [Required]
        [Column("Bandage_RH")]
        public bool BandageRH { get; set; }

        [Required]
        [Column("Block_RH")]
        public bool BlockRH { get; set; }

        [Required]
        [Column("IsBandageRemoved")]
        public bool IsBandageRemoved { get; set; }

        public ClawTreatment() : this(0, string.Empty, DateTime.MinValue, null, false, false, null, false, false, null, false, false, null, false, false, false) { }

        public ClawTreatment(int clawTreatmentId, string earTagNumber, DateTime treatmentDate, int? clawFindingLV, bool bandageLV, bool blockLV, int? clawFindingLH, bool bandageLH, bool blockLH, int? clawFindingRV, bool bandageRV, bool blockRV, int? clawFindingRH, bool bandageRH, bool blockRH, bool isBandageRemoved)
        {
            ClawTreatmentId = clawTreatmentId;
            EarTagNumber = earTagNumber;
            TreatmentDate = treatmentDate;
            ClawFindingLVId = clawFindingLV;
            BandageLV = bandageLV;
            BlockLV = blockLV;
            ClawFindingLHId = clawFindingLH;
            BandageLH = bandageLH;
            BlockLH = blockLH;
            ClawFindingRVId = clawFindingRV;
            BandageRV = bandageRV;
            BlockRV = blockRV;
            ClawFindingRHId = clawFindingRH;
            BandageRH = bandageRH;
            BlockRH = blockRH;
            IsBandageRemoved = isBandageRemoved;
        }
    }
}
