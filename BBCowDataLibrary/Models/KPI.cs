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

    // Legacy for Kind = Sql, where it is the hand-typed click target. Builder KPIs leave it to
    // KpiDrillDown, which derives it from the definition so a typo cannot send the tile to a 404.
    // Kept NOT NULL: nothing gains from making an existing column nullable.
    [Required]
    public string Url { get; set; }

    // Only executed for Kind = Sql. Builder KPIs store "" here, and a script a customer wrote is
    // NEVER cleared when a KPI is switched to Builder - that is what makes the switch reversible.
    [Required]
    public string Script { get; set; }

    [Required]
    [Column("Sort_Order")]
    public int SortOrder { get; set; }

    /// <summary>
    /// Sql or Builder. Stored as int; see <see cref="KpiKind"/> for why Sql is 0.
    /// </summary>
    [Required]
    public int Kind { get; set; }

    /// <summary>
    /// The declarative definition as JSON, null for SQL KPIs. See <see cref="KpiDefinition"/>.
    /// </summary>
    public string? Definition { get; set; }

    /// <summary>
    /// Belt and braces: a KPI only counts as declarative if it says so AND carries a definition.
    /// If Kind says Builder but the JSON is missing or unreadable, the evaluator must report an
    /// error rather than silently falling back to Script - a silent fallback would run a script the
    /// author believed to be inactive.
    /// </summary>
    [NotMapped]
    public bool IsBuilder => Kind == (int)KpiKind.Builder && !string.IsNullOrWhiteSpace(Definition);

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
