using System.ComponentModel.DataAnnotations;

namespace BB_Cow.Class
{
    public class PlannedCowTreatment
    {
        [Key]
        public int PlannedCowTreatmentId { get; set; }

        [Required]
        public string EarTagNumber { get; set; }

        [Required]
        public int MedicineId { get; set; }

        [Required]
        public DateTime AdministrationDate { get; set; }

        [Required]
        public float MedicineDosage { get; set; }

        [Required]
        [StringLength(256)]
        public string WhereHow { get; set; }

        [Required]
        public bool IsFound { get; set; }

        [Required]
        public bool IsTreatet { get; set; }

        public PlannedCowTreatment() : this(0, string.Empty, 0, DateTime.MinValue, 0.0f, string.Empty, false, false) { }

        public PlannedCowTreatment(int plannedCowTreatmentId, string earTagNumber, int medicineId, DateTime administrationDate, float medicineDosage, string whereHow, bool isFound, bool isTreatet)
        {
            PlannedCowTreatmentId = plannedCowTreatmentId;
            EarTagNumber = earTagNumber;
            MedicineId = medicineId;
            AdministrationDate = administrationDate;
            MedicineDosage = medicineDosage;
            WhereHow = whereHow;
            IsFound = isFound;
            IsTreatet = isTreatet;
        }
    }
}
