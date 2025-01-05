using System.ComponentModel.DataAnnotations;

namespace BB_Cow.Class;

public class Cow
{
    [Key]
    [Required]
    [StringLength(64)]
    public string EarTagNumber { get; set; }

    [Required]
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