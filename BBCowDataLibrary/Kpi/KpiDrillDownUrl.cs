using BB_Cow.Class;

namespace BB_Cow.Kpi;

/// <summary>
/// The link behind a KPI tile, as pure string handling.
///
/// Lives in the library rather than next to the Blazor pages so the URL contract can be tested
/// without Blazor: building and parsing are exact inverses, and that is the property that keeps a
/// tile and the table it opens showing the same set of rows.
///
/// The query keys ARE the tag keys from <see cref="KpiTagKeys"/>. That is the point rather than a
/// coincidence: the URL is the definition's filter set plus the timeframe, so there is no
/// translation table that could drift out of step with the evaluator.
/// </summary>
public static class KpiDrillDownUrl
{
    /// <summary>
    /// Free-text search. Only ever READ, never emitted: a Top-1-by-cow tile filters on the "cow"
    /// group instead, because the search matches substrings and "104" also hits ear tag "...1042".
    /// Kept so a hand-written or bookmarked link can still pre-fill the search box.
    /// </summary>
    public const string SearchKey = "q";

    public const string RangeKey = "range";

    /// <summary>
    /// Route plus query for a tile. Falls back to the stored Url for SQL KPIs and for the synthetic
    /// "+" tile, which keep their hand-typed target.
    /// </summary>
    public static string Build(KpiTileModel tile)
    {
        if (tile.IsAddTile || !tile.Kpi.IsBuilder)
        {
            return tile.Kpi.Url;
        }

        var definition = KpiDefinition.Deserialize(tile.Kpi.Definition);
        var source = definition is null ? null : KpiSourceRegistry.Find(definition.Source);
        if (definition is null || source is null)
        {
            return tile.Kpi.Url;
        }

        // A source with no page to show its rows gets no link at all. Emitting a query against
        // some other page would be worse than nothing: the reader would see a filtered list of
        // something else and believe it was the number they clicked.
        if (!source.HasDrillDown)
        {
            return string.Empty;
        }

        var parts = new List<string>();

        foreach (var (key, values) in definition.Filters)
        {
            if (values is { Count: > 0 })
            {
                parts.Add($"{key}={string.Join(",", values.Select(Uri.EscapeDataString))}");
            }
        }

        if (definition.Timeframe != KpiTimeframe.All)
        {
            parts.Add($"{RangeKey}={Days(definition.Timeframe)}");
        }

        // A Top-1-by-cow tile is about ONE animal, and the winner is only known from the result -
        // hence a tile model rather than a definition alone.
        //
        // Emitted as the ordinary "cow" filter group, which both treatment sources already declare
        // (KpiTagKeys.Cow), so it needs no special handling on the far side. Using the search box
        // instead was tried and is too blunt: it matches substrings, so "104" also finds ear tag
        // "...1042" and the table showed unrelated animals - 16 rows for a tile that said 8.
        if (definition.Measure == KpiMeasure.TopValue
            && definition.GroupBy == KpiGroupBy.Cow
            && !string.IsNullOrWhiteSpace(tile.Result.Label)
            && !definition.Filters.ContainsKey(KpiTagKeys.Cow))
        {
            parts.Add($"{KpiTagKeys.Cow}={Uri.EscapeDataString(tile.Result.Label)}");
        }

        return parts.Count == 0 ? source.Route : $"{source.Route}?{string.Join("&", parts)}";
    }

    public static int Days(KpiTimeframe timeframe) => timeframe == KpiTimeframe.Days7 ? 7 : 30;

    /// <summary>
    /// Key to values. Accepts a bare query string with or without the leading "?".
    ///
    /// Values are comma-joined because one filter group carries several, and each value was escaped
    /// individually when built - findings and medicine names contain umlauts, and a name may
    /// contain a comma, which then arrives as %2C and survives the split.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Parse(string? query)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separator]);
            var values = pair[(separator + 1)..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .ToArray();

            if (values.Length > 0)
            {
                result[key] = values;
            }
        }

        return result;
    }

    /// <summary>Values under one key, or null when the key is absent or empty.</summary>
    public static IReadOnlyList<string>? Values(IReadOnlyDictionary<string, string[]> query, string key)
        => query.TryGetValue(key, out var values) && values.Length > 0 ? values : null;

    /// <summary>The requested day count, or 0 when no usable range was given.</summary>
    public static int Range(IReadOnlyDictionary<string, string[]> query)
        => Values(query, RangeKey) is { Count: > 0 } values && int.TryParse(values[0], out var days)
            ? days
            : 0;
}
