using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class;

[Table("Cow")]
public class Cow
{
    [Key]
    [Required]
    [StringLength(64)]
    [Column("Ear_Tag_Number")]
    public string EarTagNumber { get; set; }

    [Required]
    [Column("Collar_Number")]
    public int CollarNumber { get; set; }

    [Required]
    public bool IsGone { get; set; } = false;

    public Cow(string earTagNumber, int collarNumber, bool isGone)
    {
        EarTagNumber = earTagNumber;
        CollarNumber = collarNumber;
        IsGone = isGone;
    }
    
    public Cow() : this("", 0, false)
    {
    }
}
