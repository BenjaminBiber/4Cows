using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class;

[Table("Udder")]
public class Udder
{
    [Key]
    [Column("UDDER_ID")]
    public int UdderId { get; set; }
    
    [Required]
    [Column("Quarter_LV")]
    public bool QuarterLV { get; set; }
    
    [Required]
    [Column("Quarter_LH")]
    public bool QuarterLH { get; set; }
    
    [Required]
    [Column("Quarter_RV")]
    public bool QuarterRV { get; set; }
    
    [Required]
    [Column("Quarter_RH")]
    public bool QuarterRH { get; set; }

    public Udder(int udderId, bool quarterLv, bool quarterLh, bool quarterRv, bool quarterRh)
    {
        UdderId = udderId;
        QuarterLV = quarterLv;
        QuarterLH = quarterLh;
        QuarterRV = quarterRv;
        QuarterRH = quarterRh;
    }

    public Udder() :this (int.MinValue, false, false, false, false)
    {
    }
}
