using System.ComponentModel.DataAnnotations;
using MySqlConnector;
using System.Xml.Linq;

namespace BB_Cow.Class
{
    public class CowTreatment
    {
        [Key]
        public int CowTreatmentId { get; set; }

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

        public CowTreatment() : this(0, string.Empty, 0, DateTime.MinValue, 0.0f, string.Empty) { }

        public CowTreatment(int cowTreatmentId, string earTagNumber, int medicineId, DateTime administrationDate, float medicineDosage, string whereHow)
        {
            CowTreatmentId = cowTreatmentId;
            EarTagNumber = earTagNumber;
            MedicineId = medicineId;
            AdministrationDate = administrationDate;
            MedicineDosage = medicineDosage;
            WhereHow = whereHow;
        }
    }
}
