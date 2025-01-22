using System.ComponentModel.DataAnnotations;

namespace BB_Cow.Class;

public class Udder
{
    [Key] public int UdderId { get; set; }
    
    [Required]
    public bool QuarterLV { get; set; }
    
    [Required]
    public bool QuarterLH { get; set; }
    
    [Required]
    public bool QuarterRV { get; set; }
    
    [Required]
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