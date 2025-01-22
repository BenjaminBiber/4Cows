using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace BB_Cow.Class;

public class WhereHow
{
    [Key]
    public int WhereHowId { get; set; }
    
    [Required]
    public string WhereHowName { get; set; }
    
    [Required]
    public bool ShowDialog { get; set; }

    public bool QuarterLV { get; set; }

    public bool QuarterLH { get; set; }
    
    public bool QuarterRV { get; set; }

    public bool QuarterRH { get; set; }

    public WhereHow() : this(0, "", true, false, false, false, false)
    {
    }

    public WhereHow(int whereHowId, string whereHowName, bool showDialog, bool quarterLv, bool quarterLh, bool quarterRv, bool quarterRh)
    {
        WhereHowId = whereHowId;
        WhereHowName = whereHowName;
        ShowDialog = showDialog;
        QuarterLV = quarterLv;
        QuarterLH = quarterLh;
        QuarterRV = quarterRv;
        QuarterRH = quarterRh;
    }
}