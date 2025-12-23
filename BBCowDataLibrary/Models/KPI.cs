using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class;

[Table("KPI")]
public class KPI
{
    [Required, Key]
    [Column("KPI_ID")]
    public int KPIId { get; set; }
    
    [Required]
    public string Title { get; set; }
    
    [Required]
    public string Url { get; set; }
    
    [Required]
    public string Script { get; set; }
    
    [Required]
    [Column("Sort_Order")]
    public int SortOrder { get; set; }

    public KPI()
    {
        Title = string.Empty;
        Url = string.Empty;
        Script = string.Empty;
    }

    public KPI(int kpiId, string title, string url, string script, int sortOrder) : this()
    {
        KPIId = kpiId;
        Title = title;
        Url = url;
        Script = script;
        SortOrder = sortOrder;
    }
}
