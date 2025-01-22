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
        public int WhereHowId { get; set; }

        public CowTreatment() : this(0, string.Empty, 0, DateTime.MinValue, 0.0f, int.MinValue) { }

        public CowTreatment(int cowTreatmentId, string earTagNumber, int medicineId, DateTime administrationDate, float medicineDosage, int whereHowId)
        {
            CowTreatmentId = cowTreatmentId;
            EarTagNumber = earTagNumber;
            MedicineId = medicineId;
            AdministrationDate = administrationDate;
            MedicineDosage = medicineDosage;
            WhereHowId = whereHowId;
        }
    }
}
