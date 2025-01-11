using System.ComponentModel.DataAnnotations;

namespace BB_Cow.Class;

public class Medicine
{
    [Key]
    public int MedicineId { get; set; }

    [Required]
    [StringLength(64)]
    public string MedicineName { get; set; }

    public Medicine() : this(0, string.Empty) { }

    public Medicine(int medicineId, string medicineName)
    {
        MedicineId = medicineId;
        MedicineName = medicineName;
    }
}