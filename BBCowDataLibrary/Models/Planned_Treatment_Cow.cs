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

        public PlannedCowTreatment() : this(0, string.Empty, 0, DateTime.MinValue, 0.0f, int.MinValue, false, false, int.MinValue) { }

        public PlannedCowTreatment(int plannedCowTreatmentId, string earTagNumber, int medicineId, DateTime administrationDate, float medicineDosage, int whereHowId, bool isFound, bool isTreatet, int udderId)
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
        }
    }
}
