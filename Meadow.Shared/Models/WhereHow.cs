using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Meadow.Shared.Models;

[Table("WhereHow")]
public class WhereHow
{
    [Key]
    [Column("WhereHow_ID")]
    public int WhereHowId { get; set; }
    
    [Required]
    [Column("WhereHow_Name")]
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
