using Microsoft.AspNetCore.Components;

namespace _4Cows_FE.Components.Meadow;

public enum MeadowCellStyle
{
    Normal,
    Bold,
    Mono
}

public enum MeadowAlign
{
    Left,
    Right
}

/// <summary>
/// Eine Spalte von <see cref="MeadowTable{TItem}"/>.
///
/// Typisierte Deskriptoren statt Zell-RenderFragments, und zwar vor allem
/// wegen SortBy: bisher stand in drei Tabellen
/// <c>SortBy="new Func&lt;CowTreatment, object&gt;(...)"</c> auf einer Tabelle
/// mit anderem Item-Typ, wodurch die Sortierung still gar nichts tat. Mit
/// <c>Func&lt;TItem, IComparable?&gt;</c> ist derselbe Fehler ein Compile-Fehler.
/// </summary>
public sealed class MeadowColumn<TItem>
{
    public string Title { get; init; } = "";

    /// <summary>Reine Textzelle.</summary>
    public Func<TItem, string>? Text { get; init; }

    /// <summary>Reichere Zelle (Pill, Chip, Aktionen). Schlaegt Text.</summary>
    public RenderFragment<TItem>? Template { get; init; }

    /// <summary>
    /// Sortierschluessel. Immer nach dem, was ANGEZEIGT wird - nie nach der
    /// ID dahinter (Medikament-Spalten sortierten nach MedicineId, die fuenf
    /// KPI-Spalten alle nach KPIId).
    /// Null bedeutet: Kopf ist nicht klickbar.
    /// </summary>
    public Func<TItem, IComparable?>? SortBy { get; init; }

    public MeadowCellStyle Style { get; init; } = MeadowCellStyle.Normal;
    public MeadowAlign Align { get; init; } = MeadowAlign.Left;

    /// <summary>Leerer Kopf, rechtsbuendig, nie sortierbar.</summary>
    public bool IsActions { get; init; }

    public bool NoWrap { get; init; }
    public string? Width { get; init; }
    public string? MaxWidth { get; init; }

    internal string CellClass
    {
        get
        {
            var classes = new List<string>();
            if (Style == MeadowCellStyle.Bold) classes.Add("mw-td--bold");
            if (Style == MeadowCellStyle.Mono) classes.Add("mw-td--mono");
            if (IsActions) classes.Add("mw-td--actions");
            else if (Align == MeadowAlign.Right) classes.Add("mw-td--right");
            if (NoWrap) classes.Add("mw-td--nowrap");
            return string.Join(' ', classes);
        }
    }

    internal string? CellStyle => Width is null && MaxWidth is null
        ? null
        : string.Concat(
            Width is null ? "" : $"width:{Width};",
            MaxWidth is null ? "" : $"max-width:{MaxWidth};overflow:hidden;text-overflow:ellipsis;");
}
