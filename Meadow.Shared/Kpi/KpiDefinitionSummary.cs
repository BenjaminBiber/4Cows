using BB_Cow.Class;

namespace BB_Cow.Kpi;

/// <summary>
/// A declarative definition as one readable line, e.g.
/// "Kuh Behandlungen · Anzahl · Medikament: Penicillin · letzte 30 Tage".
///
/// Exists because the KPI list in the settings shows Shorten(Script), which is empty for a builder
/// KPI - the list would look broken exactly for the KPIs that are no longer broken.
/// </summary>
public static class KpiDefinitionSummary
{
    private const string Separator = " · ";

    /// <summary>Works for both kinds, so the settings list needs no branching of its own.</summary>
    public static string Describe(KPI kpi)
    {
        if (!kpi.IsBuilder)
        {
            return Shorten(kpi.Script);
        }

        var definition = KpiDefinition.Deserialize(kpi.Definition);
        return definition is null ? "Definition unlesbar" : Describe(definition);
    }

    public static string Describe(KpiDefinition definition)
    {
        var source = KpiSourceRegistry.Find(definition.Source);
        var parts = new List<string>
        {
            source?.Label ?? definition.Source.ToString(),
            Measure(definition)
        };

        foreach (var (key, values) in definition.Filters)
        {
            if (values is null || values.Count == 0)
            {
                continue;
            }

            var label = source?.Tags.FirstOrDefault(t => t.Key == key)?.Label ?? key;
            parts.Add($"{label}: {string.Join(", ", values)}");
        }

        var timeframe = Timeframe(definition.Timeframe, source?.IsPlanned ?? false);
        if (timeframe.Length > 0)
        {
            parts.Add(timeframe);
        }

        if (definition.CompareToPrevious && definition.AllowsComparison)
        {
            parts.Add("mit Vergleich");
        }

        return string.Join(Separator, parts);
    }

    private static string Measure(KpiDefinition definition)
        => definition.Measure == KpiMeasure.TopValue
            ? $"{MeasureLabel(KpiMeasure.TopValue)} ({GroupBy(definition.GroupBy)})"
            : MeasureLabel(definition.Measure);

    /// <summary>Also the option list of the dialog, so summary and form cannot disagree.</summary>
    public static string MeasureLabel(KpiMeasure measure) => measure switch
    {
        KpiMeasure.Count => "Anzahl",
        KpiMeasure.CountDistinctCows => "Anzahl verschiedener Kühe",
        KpiMeasure.SumDosage => "Summe Menge",
        KpiMeasure.AvgDosage => "Ø Menge",
        KpiMeasure.TopValue => "Häufigster Wert",
        _ => measure.ToString()
    };

    public static string GroupBy(KpiGroupBy groupBy) => groupBy switch
    {
        KpiGroupBy.Cow => "Kuh",
        KpiGroupBy.UdderQuarter => "Euterviertel",
        KpiGroupBy.Medicine => "Medikament",
        KpiGroupBy.ClawFinding => "Befund",
        _ => "ohne Gruppierung"
    };

    /// <summary>
    /// Direction-aware, because it differs per source: planned treatments are future-dated, so
    /// their "30 Tage" are the NEXT thirty days. Writing "letzte 30 Tage" on a planned KPI would
    /// describe the opposite of what it counts.
    /// </summary>
    public static string Timeframe(KpiTimeframe timeframe, bool planned)
    {
        if (timeframe == KpiTimeframe.All)
        {
            return string.Empty;
        }

        var days = timeframe == KpiTimeframe.Days7 ? 7 : 30;
        return planned ? $"nächste {days} Tage" : $"letzte {days} Tage";
    }

    /// <summary>
    /// Like <see cref="Timeframe"/> but never empty, for the dialog's option list where "all time"
    /// needs a visible label of its own.
    /// </summary>
    public static string TimeframeOption(KpiTimeframe timeframe, bool planned)
        => timeframe == KpiTimeframe.All ? "Alle" : Timeframe(timeframe, planned);

    /// <summary>Same collapsing and cap the settings list applied to a raw script.</summary>
    private static string Shorten(string? script)
    {
        var text = (script ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 90 ? text : text[..90] + "…";
    }
}
