using System.ComponentModel.DataAnnotations;

namespace BB_Cow.Class;

public class KPI
{
    [Required]
    public int KPIId { get; set; }
    
    [Required]
    public string MethodName { get; set; }
    
    [Required]
    public int SortOrder { get; set; }

    public KPI() : this(int.MinValue, string.Empty, int.MinValue)
    { }
    
    public KPI(int kpiId, string methodName, int sortOrder)
    {
        KPIId = kpiId;
        MethodName = methodName;
        SortOrder = sortOrder;
    }
}