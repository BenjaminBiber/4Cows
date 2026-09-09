using BB_Cow.Class;

namespace BB_Cow.Kpi;

/// <summary>
/// One row of any KPI source, projected into a single uniform shape.
///
/// This projection is the load-bearing idea of the whole feature. The five sources have five
/// unrelated entity types; projecting them once means the evaluator is ONE non-generic function
/// over a list, with no knowledge of any source - no generics leaking into the UI, no switch over
/// source ids scattered across the dialog.
///
/// Three roles are typed fields because they are structurally special: <see cref="Date"/> drives
/// the timeframe, <see cref="Dosage"/> is the only summable number in the schema, and
/// <see cref="CowId"/> is the key for "how many different cows". Everything filterable or groupable
/// lives in <see cref="Tags"/> as data, so a filter key from a stored definition indexes straight
/// into it and the evaluator needs no per-field branching at all.
/// </summary>
public sealed record KpiRow
{
    /// <summary>
    /// The stable Cow_ID, already resolved. Note that Cow_Treatment.Ear_Tag_Number and its three
    /// siblings actually STORE Cow_ID despite the column name (see the AddCowIdAndIsCalv migration);
    /// KpiSourceRegistry is the single place that knows this, so nothing downstream can get the
    /// join wrong and silently drop every calf.
    /// </summary>
    public required string CowId { get; init; }

    /// <summary>Collar number as text - what the Top-1 tiles have always displayed.</summary>
    public required string CowLabel { get; init; }

    /// <summary>Null only for the Cow source, which has no date column at all.</summary>
    public DateTime? Date { get; init; }

    /// <summary>Medicine_Dosage, or null where the source has no numeric field.</summary>
    public double? Dosage { get; init; }

    /// <summary>
    /// The unit <see cref="Dosage"/> is measured in, taken from the medicine. Empty when none is
    /// recorded, and null for sources without a dosage.
    ///
    /// Carried on the row rather than resolved later because a sum is only meaningful when every
    /// contributing row shares one unit. Before the medicine carried a unit every dosage was
    /// implicitly ml; now "3 tablets + 20 ml = 23" is a number that looks entirely plausible and is
    /// nonsense, so the evaluator has to be able to see the mix.
    /// </summary>
    public string? DosageUnit { get; init; }

    /// <summary>
    /// Filterable and groupable values by tag key (see <see cref="KpiTagKeys"/>).
    ///
    /// Multi-valued on purpose: a claw treatment carries up to four findings, and a cow treatment
    /// can be flagged several ways at once. A row matches a filter if ANY of its values under that
    /// key is selected - the same OR-within-a-group rule MeadowMultiSelect and Claw_Table already
    /// use. A missing or empty key simply never matches.
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Tags { get; init; }

    /// <summary>Values under one tag key, or empty. Never null, so callers need no guard.</summary>
    public IReadOnlyList<string> TagValues(string key)
        => Tags.TryGetValue(key, out var values) ? values : Array.Empty<string>();
}

/// <summary>
/// Tag keys. These are the identifiers persisted in KpiDefinition.Filters, so renaming one is a
/// data migration, not a rename.
/// </summary>
public static class KpiTagKeys
{
    public const string Medicine = "medicine";
    public const string WhereHow = "whereHow";
    public const string UdderQuarter = "udderQuarter";

    /// <summary>
    /// Claw findings AND the two synthetic states "Verband"/"Klotz" - deliberately one group.
    ///
    /// Claw_Table offers them in a single multi-select, so they are OR-ed there. Splitting the
    /// states into their own group would AND them ("Mortellaro AND bandaged") and the drill-down
    /// would then show a different, larger set than the tile it was opened from.
    /// </summary>
    public const string ClawFinding = "clawFinding";

    /// <summary>Which claw a planned treatment targets. Planned findings are booleans, not names.</summary>
    public const string ClawPosition = "clawPosition";

    public const string Cow = "cow";

    // Each boolean gets its OWN key holding a value PAIR, not one shared "flag" group. Values
    // inside a group are OR-ed, so a single group could never express "calves that are still in the
    // herd" - that needs two groups AND-ed. Every row emits exactly one value of each pair, so
    // selecting both members of a pair correctly means "no restriction".
    public const string Calf = "calf";
    public const string Herd = "herd";
    public const string Found = "found";
    public const string Treated = "treated";

    /// <summary>
    /// Which tag a ranking dimension reads. Keeping <see cref="KpiGroupBy"/> as its own enum rather
    /// than storing a raw key makes a stored definition self-describing and rejectable.
    /// </summary>
    public static string? ForGroupBy(KpiGroupBy groupBy) => groupBy switch
    {
        KpiGroupBy.Cow => Cow,
        KpiGroupBy.UdderQuarter => UdderQuarter,
        KpiGroupBy.Medicine => Medicine,
        KpiGroupBy.ClawFinding => ClawFinding,
        _ => null
    };
}

/// <summary>
/// Booleans expressed as synthetic filter values, so every filter stays a string multi-select and
/// MeadowMultiSelect is reusable unchanged. This is not a workaround: ClawTableFilter already offers
/// "Verband" and "Klotz" exactly this way alongside the real findings.
///
/// <see cref="Bandage"/> and <see cref="Block"/> are the SAME strings the claw table filters on.
/// ClawTableFilter should reference these constants rather than declare its own, otherwise a tile
/// and the table it drills into can drift apart.
///
/// The remaining values come in pairs because a row always emits exactly one of each pair - that is
/// what makes "not gone" expressible at all (see the key comments in <see cref="KpiTagKeys"/>).
/// </summary>
public static class KpiFlags
{
    public const string Bandage = "Verband";
    public const string Block = "Klotz";

    public const string Calf = "Kalb";
    public const string NotCalf = "Kein Kalb";

    public const string InHerd = "Im Bestand";
    public const string Gone = "Abgang";

    public const string Found = "Gefunden";
    public const string NotFound = "Nicht gefunden";

    public const string Treated = "Behandelt";
    public const string NotTreated = "Nicht behandelt";
}
