using BB_Cow.Class;

namespace BB_Cow.Kpi;

/// <summary>One filterable tag group of a source, as the dialog needs to render it.</summary>
public sealed record KpiTagInfo(string Key, string Label);

/// <summary>
/// What a KPI source can do and how its rows are built. Metadata as DATA, so the dialog and the
/// evaluator read the same declaration instead of each carrying their own switch over source ids.
/// </summary>
public sealed class KpiSourceInfo
{
    public required KpiSourceId Id { get; init; }

    public required string Label { get; init; }

    /// <summary>
    /// Page that lists this source's rows, for the drill-down. EMPTY when no such page exists - the
    /// tile then renders without a link rather than sending the reader somewhere that shows a
    /// different set of rows.
    /// </summary>
    public required string Route { get; init; }

    public bool HasDrillDown => !string.IsNullOrWhiteSpace(Route);

    /// <summary>
    /// Planned treatments are deliberately future-dated, so for them a timeframe looks FORWARD:
    /// "30 days" means the next 30 days. DateRanges.Matches in the frontend deliberately excludes
    /// the future for the two recorded-treatment tables, and the evaluator has to honour the same
    /// direction or the tile and the table it drills into disagree.
    /// </summary>
    public required bool IsPlanned { get; init; }

    /// <summary>False only for Cow, which has no date column at all.</summary>
    public required bool HasDate { get; init; }

    /// <summary>True only where Medicine_Dosage exists: the two cow-treatment tables.</summary>
    public required bool HasDosage { get; init; }

    public required IReadOnlyList<KpiMeasure> Measures { get; init; }

    public required IReadOnlyList<KpiGroupBy> Groupings { get; init; }

    public required IReadOnlyList<KpiTagInfo> Tags { get; init; }

    public required Func<IKpiLookups, IReadOnlyList<KpiRow>> BuildRows { get; init; }

    public bool SupportsTimeframe => HasDate;

    /// <summary>
    /// No comparison for planned sources. "The previous equally long period" before a forward-looking
    /// window lands in the past, i.e. it would compare treatments still to come against treatments
    /// already scheduled and gone - a number nobody can interpret. Better to withhold it than to
    /// show something meaningless.
    /// </summary>
    public bool SupportsCompare => HasDate && !IsPlanned;

    public bool Supports(KpiMeasure measure) => Measures.Contains(measure);

    public bool Supports(KpiGroupBy groupBy) => Groupings.Contains(groupBy);

    /// <summary>Options for one filter group, from the values that ACTUALLY occur in the rows.</summary>
    public IReadOnlyList<string> OptionsFor(string tagKey, IReadOnlyList<KpiRow> rows)
        => rows.SelectMany(r => r.TagValues(tagKey))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.CurrentCulture)
            .ToList();
}

/// <summary>
/// The five KPI sources. Everything a source-specific decision could hide behind is declared here,
/// once.
/// </summary>
public static class KpiSourceRegistry
{
    public static IReadOnlyList<KpiSourceInfo> All { get; } = new[]
    {
        CowTreatments(),
        ClawTreatments(),
        PlannedCowTreatments(),
        PlannedClawTreatments(),
        Cows()
    };

    public static KpiSourceInfo? Find(KpiSourceId id) => All.FirstOrDefault(s => s.Id == id);

    // ---- Sources -------------------------------------------------------

    private static KpiSourceInfo CowTreatments() => new()
    {
        Id = KpiSourceId.CowTreatment,
        Label = "Kuh Behandlungen",
        Route = "Kuh_Daten",
        IsPlanned = false,
        HasDate = true,
        HasDosage = true,
        Measures = new[]
        {
            KpiMeasure.Count, KpiMeasure.CountDistinctCows,
            KpiMeasure.SumDosage, KpiMeasure.AvgDosage, KpiMeasure.TopValue
        },
        Groupings = new[] { KpiGroupBy.Cow, KpiGroupBy.UdderQuarter, KpiGroupBy.Medicine },
        Tags = new[]
        {
            new KpiTagInfo(KpiTagKeys.Medicine, "Medikament"),
            new KpiTagInfo(KpiTagKeys.WhereHow, "Wie / Wo"),
            new KpiTagInfo(KpiTagKeys.UdderQuarter, "Euterviertel"),
            new KpiTagInfo(KpiTagKeys.Cow, "Kuh (Halsband)")
        },
        BuildRows = l => l.CowTreatments.Select(t =>
        {
            // THE TRAP: Ear_Tag_Number stores Cow.Cow_ID, not the ear tag. The column name is
            // historically wrong (see the AddCowIdAndIsCalv migration). Resolved here and nowhere
            // else - joining on the ear tag instead would silently drop every calf.
            var collar = l.CollarLabel(t.EarTagNumber);
            return new KpiRow
            {
                CowId = t.EarTagNumber,
                CowLabel = collar,
                Date = t.AdministrationDate,
                Dosage = t.MedicineDosage,
                Tags = Tags(
                    (KpiTagKeys.Medicine, One(l.MedicineName(t.MedicineId))),
                    (KpiTagKeys.WhereHow, One(l.WhereHowName(t.WhereHowId))),
                    (KpiTagKeys.UdderQuarter, One(l.UdderLabel(t.UdderId))),
                    (KpiTagKeys.Cow, One(collar)))
            };
        }).ToList()
    };

    private static KpiSourceInfo ClawTreatments() => new()
    {
        Id = KpiSourceId.ClawTreatment,
        Label = "Klauen Behandlungen",
        Route = "Klauen_Daten",
        IsPlanned = false,
        HasDate = true,
        HasDosage = false,
        Measures = new[] { KpiMeasure.Count, KpiMeasure.CountDistinctCows, KpiMeasure.TopValue },
        Groupings = new[] { KpiGroupBy.Cow, KpiGroupBy.ClawFinding },
        Tags = new[]
        {
            new KpiTagInfo(KpiTagKeys.ClawFinding, "Befund"),
            new KpiTagInfo(KpiTagKeys.Cow, "Kuh (Halsband)")
        },
        BuildRows = l => l.ClawTreatments.Select(t =>
        {
            var collar = l.CollarLabel(t.EarTagNumber);
            return new KpiRow
            {
                CowId = t.EarTagNumber,
                CowLabel = collar,
                Date = t.TreatmentDate,
                Tags = Tags(
                    (KpiTagKeys.ClawFinding, ClawFindingValues(t)),
                    (KpiTagKeys.Cow, One(collar)))
            };
        }).ToList()
    };

    private static KpiSourceInfo PlannedCowTreatments() => new()
    {
        Id = KpiSourceId.PlannedCowTreatment,
        Label = "Geplante Kuh Behandlungen",
        Route = "geplante_Kuh_Daten",
        IsPlanned = true,
        HasDate = true,
        HasDosage = true,
        Measures = new[]
        {
            KpiMeasure.Count, KpiMeasure.CountDistinctCows,
            KpiMeasure.SumDosage, KpiMeasure.AvgDosage, KpiMeasure.TopValue
        },
        Groupings = new[] { KpiGroupBy.Cow, KpiGroupBy.UdderQuarter, KpiGroupBy.Medicine },
        Tags = new[]
        {
            new KpiTagInfo(KpiTagKeys.Medicine, "Medikament"),
            new KpiTagInfo(KpiTagKeys.WhereHow, "Wie / Wo"),
            new KpiTagInfo(KpiTagKeys.UdderQuarter, "Euterviertel"),
            new KpiTagInfo(KpiTagKeys.Found, "Gefunden"),
            new KpiTagInfo(KpiTagKeys.Treated, "Behandelt"),
            new KpiTagInfo(KpiTagKeys.Cow, "Kuh (Halsband)")
        },
        BuildRows = l => l.PlannedCowTreatments.Select(t =>
        {
            var collar = l.CollarLabel(t.EarTagNumber);
            return new KpiRow
            {
                CowId = t.EarTagNumber,
                CowLabel = collar,
                Date = t.AdministrationDate,
                Dosage = t.MedicineDosage,
                Tags = Tags(
                    (KpiTagKeys.Medicine, One(l.MedicineName(t.MedicineId))),
                    (KpiTagKeys.WhereHow, One(l.WhereHowName(t.WhereHowId))),
                    // Planned_Cow_Treatment names this column Udder_ID, not COW_QUARTER_ID.
                    (KpiTagKeys.UdderQuarter, One(l.UdderLabel(t.UdderId))),
                    (KpiTagKeys.Found, One(t.IsFound ? KpiFlags.Found : KpiFlags.NotFound)),
                    (KpiTagKeys.Treated, One(t.IsTreatet ? KpiFlags.Treated : KpiFlags.NotTreated)),
                    (KpiTagKeys.Cow, One(collar)))
            };
        }).ToList()
    };

    private static KpiSourceInfo PlannedClawTreatments() => new()
    {
        Id = KpiSourceId.PlannedClawTreatment,
        Label = "Geplante Klauen Behandlungen",
        Route = "geplante_Klauen_Daten",
        IsPlanned = true,
        HasDate = true,
        HasDosage = false,
        Measures = new[] { KpiMeasure.Count, KpiMeasure.CountDistinctCows, KpiMeasure.TopValue },
        Groupings = new[] { KpiGroupBy.Cow },
        Tags = new[]
        {
            new KpiTagInfo(KpiTagKeys.ClawPosition, "Klaue"),
            new KpiTagInfo(KpiTagKeys.Cow, "Kuh (Halsband)")
        },
        BuildRows = l => l.PlannedClawTreatments.Select(t =>
        {
            var collar = l.CollarLabel(t.EarTagNumber);
            return new KpiRow
            {
                CowId = t.EarTagNumber,
                CowLabel = collar,
                Date = t.TreatmentDate,
                Tags = Tags(
                    // Planned findings are four booleans, not names - so the tag carries the claw
                    // positions that are ticked, which is genuinely all this table records.
                    (KpiTagKeys.ClawPosition,
                        HoofPositions.All.Where(t.GetFlag).Select(p => p.ToString()).ToList()),
                    (KpiTagKeys.Cow, One(collar)))
            };
        }).ToList()
    };

    private static KpiSourceInfo Cows() => new()
    {
        Id = KpiSourceId.Cow,
        Label = "Kühe",
        // No route ON PURPOSE. The application has no page that lists cows - "Kuh_Daten" is the cow
        // TREATMENTS table, and animals are maintained in the Basisdaten tab of the settings, which
        // has no address of its own. Pointing there would open a table of 120 treatments behind a
        // tile that counted 37 animals, so this tile is deliberately not a link.
        Route = string.Empty,
        IsPlanned = false,
        // Cow has no date column whatsoever (Cow_ID, Ear_Tag_Number, Collar_Number, Is_Calv,
        // IsGone), so no timeframe and no trend are computable here.
        HasDate = false,
        HasDosage = false,
        // Count only. CountDistinctCows would equal Count - there is one row per cow - and summing
        // Collar_Number is nonsense. Ranking cows by cow is meaningless too.
        Measures = new[] { KpiMeasure.Count },
        Groupings = Array.Empty<KpiGroupBy>(),
        Tags = new[]
        {
            new KpiTagInfo(KpiTagKeys.Calf, "Kalb"),
            new KpiTagInfo(KpiTagKeys.Herd, "Bestand")
        },
        BuildRows = l => l.Cows.Select(c => new KpiRow
        {
            CowId = c.CowId,
            CowLabel = c.CollarNumber.ToString(),
            Date = null,
            // No "cow" tag here: this source can neither be grouped nor filtered by cow (one row
            // IS one cow), and an undeclared tag would only be dead weight in every row.
            Tags = Tags(
                (KpiTagKeys.Calf, One(c.IsCalv ? KpiFlags.Calf : KpiFlags.NotCalf)),
                (KpiTagKeys.Herd, One(c.IsGone ? KpiFlags.Gone : KpiFlags.InHerd)))
        }).ToList()
    };

    // ---- Projection helpers --------------------------------------------

    /// <summary>
    /// Findings plus the two synthetic states, with EXACTLY the semantics of
    /// Claw_Table.MatchesFinding - equality on the trimmed finding, a bandage only when it has not
    /// been removed, a block regardless. Any divergence here shows up as a tile and its drill-down
    /// disagreeing.
    /// </summary>
    private static IReadOnlyList<string> ClawFindingValues(ClawTreatment t)
    {
        var values = HoofPositions.All
            .Select(p => t.GetFinding(p)?.Trim() ?? "")
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!t.IsBandageRemoved && HoofPositions.All.Any(t.GetBandage))
        {
            values.Add(KpiFlags.Bandage);
        }

        if (HoofPositions.All.Any(t.GetBlock))
        {
            values.Add(KpiFlags.Block);
        }

        return values;
    }

    /// <summary>
    /// A single value, or nothing at all when it is missing - never a list holding "".
    ///
    /// Also drops the literal "--", which the lookup services return for an unknown id. That is a
    /// display placeholder, not a value, and as a tag it would appear in the dialog as a selectable
    /// medicine. Filtered here rather than in KpiRowProvider so the guarantee holds for EVERY
    /// IKpiLookups implementation instead of just the production one.
    /// </summary>
    private static IReadOnlyList<string> One(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var trimmed = value.Trim();
        return trimmed == UnknownPlaceholder ? Array.Empty<string>() : new[] { trimmed };
    }

    /// <summary>What CowService, MedicineService and WhereHowService return for an unknown id.</summary>
    private const string UnknownPlaceholder = "--";

    /// <summary>
    /// Drops empty groups, so a row with no udder quarter simply has no value under that key. That
    /// absence is what makes Top-1 skip the sentinel row without knowing any id.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Tags(
        params (string Key, IReadOnlyList<string> Values)[] entries)
    {
        var tags = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, values) in entries)
        {
            if (values.Count > 0)
            {
                tags[key] = values;
            }
        }

        return tags;
    }
}
