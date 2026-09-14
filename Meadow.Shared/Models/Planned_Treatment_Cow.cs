using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class
{
    [Table("Planned_Cow_Treatment")]
    public class PlannedCowTreatment
    {
        [Key]
        [Column("Planned_Cow_Treatment_ID")]
        public int PlannedCowTreatmentId { get; set; }

        [Required]
        [Column("Ear_Tag_Number")]
        public string EarTagNumber { get; set; }

        [Required]
        [Column("Medicine_ID")]
        public int MedicineId { get; set; }

        [Required]
        [Column("Administration_Date")]
        public DateTime AdministrationDate { get; set; }

        [Required]
        [Column("Medicine_Dosage")]
        public float MedicineDosage { get; set; }

        [Required]
        [Column("WhereHow_ID")]
        public int WhereHowId { get; set; }

        [Required]
        [Column("IsFound")]
        public bool IsFound { get; set; }

        [Required]
        [Column("IsTreatet")]
        public bool IsTreatet { get; set; }
        
        [Required]
        [Column("Udder_ID")]
        public int UdderId { get; set; }

        /// <summary>
        /// Behandlungsgrund, optional. NULL heisst "kein Grund angegeben" -
        /// siehe den Kommentar an CowTreatment.TreatmentReasonId.
        /// </summary>
        [Column("Treatment_Reason_ID")]
        public int? TreatmentReasonId { get; set; }

        public PlannedCowTreatment() : this(0, string.Empty, 0, DateTime.MinValue, 0.0f, int.MinValue, false, false, int.MinValue) { }

        // treatmentReasonId ist optional, damit die vorhandenen positionellen
        // Aufrufe - allen voran DemoDataSeeder - unveraendert weiterlaufen.
        public PlannedCowTreatment(int plannedCowTreatmentId, string earTagNumber, int medicineId, DateTime administrationDate, float medicineDosage, int whereHowId, bool isFound, bool isTreatet, int udderId, int? treatmentReasonId = null)
        {
            PlannedCowTreatmentId = plannedCowTreatmentId;
            EarTagNumber = earTagNumber;
            MedicineId = medicineId;
            AdministrationDate = administrationDate;
            MedicineDosage = medicineDosage;
            WhereHowId = whereHowId;
            IsFound = isFound;
            IsTreatet = isTreatet;
            UdderId = udderId;
            TreatmentReasonId = treatmentReasonId;
        }
    }
}
