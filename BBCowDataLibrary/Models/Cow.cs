using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Class;

[Table("Cow")]
[Index(nameof(EarTagNumber), IsUnique = true, Name = "IX_Cow_Ear_Tag_Number")]
public class Cow
{
    // Stable identity of the animal for its whole life. For identified cows this equals the
    // ear tag number; for calves (no ear tag yet) it is a GUID and never changes, even after
    // an ear tag is later assigned, so treatment history stays linked.
    [Key]
    [Required]
    [StringLength(64)]
    [Column("Cow_ID")]
    public string CowId { get; set; }

    // Nullable: calves are registered with only a collar number and no ear tag yet.
    // MySQL unique indexes allow multiple NULLs, so many tagless calves can coexist.
    [StringLength(64)]
    [Column("Ear_Tag_Number")]
    public string? EarTagNumber { get; set; }

    [Required]
    [Column("Collar_Number")]
    public int CollarNumber { get; set; }

    // A cow registered with a collar number but no ear tag. Set to false once an ear tag
    // is assigned (via the XLink scraper or manual Basisdatenpflege).
    [Required]
    [Column("Is_Calv")]
    public bool IsCalv { get; set; } = false;

    [Required]
    public bool IsGone { get; set; } = false;

    public Cow(string cowId, string? earTagNumber, int collarNumber, bool isCalv, bool isGone)
    {
        CowId = cowId;
        EarTagNumber = earTagNumber;
        CollarNumber = collarNumber;
        IsCalv = isCalv;
        IsGone = isGone;
    }

    // Convenience ctor for an identified (ear-tagged) cow: Cow_ID = ear tag, not a calf.
    public Cow(string earTagNumber, int collarNumber, bool isGone)
        : this(earTagNumber, earTagNumber, collarNumber, isCalv: false, isGone: isGone)
    {
    }

    public Cow() : this("", null, 0, false, false)
    {
    }

    // Factory for a calf: no ear tag, a GUID identity, IsCalv = true.
    public static Cow CreateCalf(int collarNumber)
        => new(Guid.NewGuid().ToString(), null, collarNumber, isCalv: true, isGone: false);
}
