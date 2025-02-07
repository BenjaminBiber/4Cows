using System.ComponentModel.DataAnnotations;

namespace BB_Cow.Class;

public class KPI
{
    [Required, Key]
    public int KPIId { get; set; }
    
    [Required]
    public string Title { get; set; }
    
    [Required]
    public string Url { get; set; }
    
    [Required]
    public string Script { get; set; }
    
    [Required]
    public int SortOrder { get; set; }

    public KPI() : this(int.MinValue, string.Empty, string.Empty, string.Empty, int.MinValue)
    {
    }
    
    public KPI(int kpiId, string title, string url, string script, int sortOrder)
    {
        KPIId = kpiId;
        Title = title;
        Url = url;
        Script = script;
        SortOrder = sortOrder;
    }
}