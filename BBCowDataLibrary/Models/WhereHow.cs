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

    public WhereHow() : this(0, "", true)
    {
    }

    public WhereHow(int whereHowId, string whereHowName, bool showDialog)
    {
        WhereHowId = whereHowId;
        WhereHowName = whereHowName;
        ShowDialog = showDialog;
    }
}