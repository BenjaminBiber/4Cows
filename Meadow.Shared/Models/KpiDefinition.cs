using System.Text.Json;
using System.Text.Json.Serialization;

namespace BB_Cow.Class;

/// <summary>
/// How a KPI computes its value. Persisted as the KPI.Kind column.
///
/// Sql is 0 ON PURPOSE: it makes the C# default and the migration's column default say the same
/// thing - "behave exactly as before". Every KPI row that already exists in a customer database
/// gets Kind = 0, Definition = NULL and an untouched Script, so nothing about it changes. A new
/// KPI created in the dialog sets Kind = Builder explicitly.
/// </summary>
public enum KpiKind
{
    Sql = 0,
    Builder = 1
}

/// <summary>The table a KPI counts over. Per-source metadata and capabilities live in KpiSourceRegistry.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KpiSourceId
{
    CowTreatment,
    ClawTreatment,
    PlannedCowTreatment,
    PlannedClawTreatment,
    Cow
}

/// <summary>
/// What is computed over the filtered rows.
///
/// TopValue is not an aggregate but a ranking: group by <see cref="KpiDefinition.GroupBy"/>, take
/// the most frequent group, show its label. It exists because three of the seven shipped KPIs are
/// "GROUP BY ... ORDER BY COUNT(*) DESC LIMIT 1" and return TEXT, not a number.
///
/// SumDosage/AvgDosage target Medicine_Dosage, the only meaningful numeric field in the schema
/// (there is no milk yield, weight or lactation data anywhere), and it exists on Cow_Treatment and
/// Planned_Cow_Treatment only. Summing Collar_Number would be nonsense, hence the per-source
/// capability gate rather than one uniform measure list.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KpiMeasure
{
    Count,
    CountDistinctCows,
    SumDosage,
    AvgDosage,
    TopValue
}

/// <summary>
/// Ranking dimension for <see cref="KpiMeasure.TopValue"/>.
///
/// UdderQuarter means the COMBINATION ("LV/RH"), not the individual quarter. The seeded SQL groups
/// by COW_QUARTER_ID, so a multi-quarter treatment is its own group; exploding into single quarters
/// would change the value the tile has always shown.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KpiGroupBy
{
    None,
    Cow,
    UdderQuarter,
    Medicine,
    ClawFinding
}

/// <summary>
/// Deliberately congruent with the frontend's DateRange (All/Days7/Days30).
///
/// A tile drills down into the matching table page, and that page can only express these three
/// ranges. A KPI with "90 days" would make the table show a different set than the tile it was
/// opened from. Widening this means widening DateRange and every table page with it.
///
/// Direction is a property of the source, not of the timeframe: DateRanges.Matches deliberately
/// excludes the future, but planned treatments are deliberately future-dated, so for the two
/// planned sources "30 days" means the NEXT 30 days. See KpiSourceRegistry.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KpiTimeframe
{
    All,
    Days7,
    Days30
}

/// <summary>
/// A KPI defined by clicking instead of by writing SQL. Persisted as JSON in KPI.Definition.
///
/// JSON rather than normalised columns: the project has no fluent configuration and no declared
/// relationships at all (DatabaseContext is a list of Set-of-T properties), so a filter table would
/// be its first one and would have no cascade delete - KPIService.DeleteDataAsync would have to
/// delete children by hand and its ImmutableDictionary cache would have to hydrate them. That is a
/// lot of machinery for data nobody ever queries; the whole table is loaded into memory anyway.
/// Script is the precedent: already an opaque longtext that is never joined or filtered.
///
/// The accepted cost is no database-level validation. A malformed row must therefore surface as
/// KpiResultState.Error with a message, never as the old catch-all "--".
/// </summary>
public sealed class KpiDefinition
{
    public KpiSourceId Source { get; set; }

    public KpiMeasure Measure { get; set; }

    /// <summary>Only meaningful for <see cref="KpiMeasure.TopValue"/>; None otherwise.</summary>
    public KpiGroupBy GroupBy { get; set; }

    public KpiTimeframe Timeframe { get; set; }

    /// <summary>
    /// Show the delta against the previous equally long period. See <see cref="AllowsComparison"/>:
    /// impossible for <see cref="KpiTimeframe.All"/> (there is no previous period) and meaningless
    /// for TopValue, which yields a label rather than a number.
    /// </summary>
    public bool CompareToPrevious { get; set; }

    /// <summary>Suffix on the tile, e.g. "ml". Null or empty renders nothing.</summary>
    public string? Unit { get; set; }

    /// <summary>Decimal places for Sum/Avg. Ignored by Count and TopValue.</summary>
    public int Decimals { get; set; }

    /// <summary>
    /// Filter key (see KpiFilterKeys) to selected values.
    ///
    /// Mirrors MeadowMultiSelect exactly, so that component is reusable unchanged: an empty or
    /// missing list means "all" (no filter), values inside one group are OR-ed, separate groups are
    /// AND-ed. Booleans are expressed as synthetic values in the flag group rather than as their own
    /// operator kind - the same trick ClawTableFilter already uses for "Verband"/"Klotz". That is why
    /// no operator model and no numeric range editor is needed here.
    ///
    /// CRITICAL: values are DISPLAY NAMES, never lookup ids. DemoResetBackgroundService clears
    /// Medicine and WhereHow nightly with ALTER TABLE ... AUTO_INCREMENT = 1 but deliberately does
    /// NOT clear KPI, so those ids are re-assigned every night while these definitions survive - an
    /// id stored here would silently start counting a different medicine. Names are compared
    /// OrdinalIgnoreCase, matching CowTableFilter.Medicines. The trade-off is that renaming a
    /// medicine breaks the filter, which the evaluator reports as a hint instead of a silent zero.
    /// </summary>
    public Dictionary<string, List<string>> Filters { get; set; } = new();

    /// <summary>A previous period only exists for a bounded timeframe, and only numbers can be compared.</summary>
    [JsonIgnore]
    public bool AllowsComparison
        => Timeframe != KpiTimeframe.All && Measure != KpiMeasure.TopValue;

    /// <summary>TopValue needs a ranking dimension; every other measure must not carry one.</summary>
    [JsonIgnore]
    public bool RequiresGroupBy => Measure == KpiMeasure.TopValue;

    /// <summary>Sum and average need a numeric field, which only the two cow-treatment sources have.</summary>
    [JsonIgnore]
    public bool RequiresDosage => Measure is KpiMeasure.SumDosage or KpiMeasure.AvgDosage;

    public KpiDefinition Clone() => Deserialize(Serialize(this)) ?? new KpiDefinition();

    // Enums are written as strings so reordering a member later cannot silently reinterpret every
    // stored definition. Unknown members are ignored rather than rejected (the System.Text.Json
    // default), so a row written by a newer build does not crash an older one.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(KpiDefinition definition)
        => JsonSerializer.Serialize(definition, Options);

    /// <summary>Returns null for null, empty or malformed JSON - the caller turns that into an Error state.</summary>
    public static KpiDefinition? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KpiDefinition>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
