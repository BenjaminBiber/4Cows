using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class
{
    [Table("Cow_Treatment")]
    public class CowTreatment
    {
        [Key]
        [Column("Cow_Treatment_ID")]
        public int CowTreatmentId { get; set; }

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
        [Column("COW_QUARTER_ID")]
        public int UdderId { get; set; }

        /// <summary>
        /// Behandlungsgrund, optional. NULL heisst "kein Grund angegeben".
        ///
        /// Bewusst nullable und nicht der int.MinValue-Sentinel von UdderId:
        /// dort bedeutet der Wert "noch nicht gewaehlt" und Save besteht je
        /// nach Wie/Wo darauf, hier ist "kein Grund" ein regulaerer
        /// Dauerzustand - und alle Bestandszeilen bekommen ihn durch die
        /// Migration von selbst.
        /// </summary>
        [Column("Treatment_Reason_ID")]
        public int? TreatmentReasonId { get; set; }

        public CowTreatment() : this(0, string.Empty, 0, DateTime.MinValue, 0.0f, int.MinValue, int.MinValue) { }

        // treatmentReasonId ist optional, damit die vorhandenen positionellen
        // Aufrufe - allen voran DemoDataSeeder - unveraendert weiterlaufen.
        public CowTreatment(int cowTreatmentId, string earTagNumber, int medicineId, DateTime administrationDate, float medicineDosage, int whereHowId, int udderId, int? treatmentReasonId = null)
        {
            CowTreatmentId = cowTreatmentId;
            EarTagNumber = earTagNumber;
            MedicineId = medicineId;
            AdministrationDate = administrationDate;
            MedicineDosage = medicineDosage;
            WhereHowId = whereHowId;
            UdderId = udderId;
            TreatmentReasonId = treatmentReasonId;
        }
    }
}
