using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class;

[Table("Medicine")]
public class Medicine
{
    [Key]
    [Column("Medicine_ID")]
    public int MedicineId { get; set; }

    [Required]
    [StringLength(64)]
    [Column("Medicine_Name")]
    public string MedicineName { get; set; }

    public Medicine() : this(0, string.Empty) { }

    public Medicine(int medicineId, string medicineName)
    {
        MedicineId = medicineId;
        MedicineName = medicineName;
    }
}
