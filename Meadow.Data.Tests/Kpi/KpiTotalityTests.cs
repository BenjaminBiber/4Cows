using Meadow.Client.Components.Ui;
using Meadow.Shared.Kpi;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Kpi;

/// <summary>
/// Guards against the one failure mode these enums keep producing: a member is ADDED, everything
/// still compiles, and some switch quietly treats it as something else.
///
/// The KPI code is full of mappings keyed by enum - days per timeframe, tag key per grouping, SQL
/// expression per grouping, German label per everything. Each was written when the enum was
/// complete, and none of them fails loudly when it stops being complete. These tests walk
/// Enum.GetValues so that forgetting a mapping is a red test rather than a wrong number on a tile.
/// </summary>
public class KpiTotalityTests
{
    [Fact]
    public void Every_timeframe_has_a_day_count()
    {
        // Was three separate "timeframe == Days7 ? 7 : 30" expressions: a binary test over a
        // growing enum, which meant a fourth timeframe would have silently meant thirty days in
        // the evaluator, the drill-down link AND the label.
        foreach (var timeframe in KpiTimeframes.All)
        {
            var days = KpiTimeframes.Days(timeframe);

            if (timeframe == KpiTimeframe.All)
            {
                Assert.Equal(0, days);
            }
            else
            {
                Assert.True(days > 0, $"{timeframe} hat keinen Tageswert.");
            }
        }
    }

    [Fact]
    public void Every_timeframe_has_a_table_range_and_back()
    {
        // KpiDefinition documents the congruence to DateRange as a deliberate constraint: a tile
        // drills into a table page, and that page can only express these ranges. Until now it was
        // only a comment.
        foreach (var timeframe in KpiTimeframes.All)
        {
            var range = DateRanges.FromTimeframe(timeframe);
            Assert.Equal(timeframe, DateRanges.ToTimeframe(range));
        }
    }

    [Fact]
    public void Every_table_range_has_a_timeframe_and_back()
    {
        foreach (var range in Enum.GetValues<DateRange>())
        {
            var timeframe = DateRanges.ToTimeframe(range);
            Assert.Equal(range, DateRanges.FromTimeframe(timeframe));
        }
    }

    [Fact]
    public void Every_table_range_label_parses_back_to_its_range()
    {
        foreach (var range in Enum.GetValues<DateRange>())
        {
            Assert.Equal(range, DateRanges.Parse(DateRanges.ToLabel(range)));
        }
    }

    [Theory]
    [InlineData("", false)]                 // Root IST das Dashboard
    [InlineData("app", false)]
    [InlineData("Kuh_Daten", true)]
    [InlineData("Klauen_Daten", true)]
    [InlineData("Settings", true)]
    [InlineData("Settings?tab=kpis", true)] // Query darf die Entscheidung nicht kippen
    [InlineData("Kuh/DE 08 1523 1042", true)]
    [InlineData("kennzahl/7", true)]
    [InlineData("nicht-gefunden", true)]
    public void The_header_title_leads_home_from_everywhere_but_home(string route, bool expectLink)
    {
        // Der Titel im Kopf fuehrt aufs Dashboard - ausser man ist schon dort, denn ein Link auf
        // die eigene Seite verspricht etwas, das nicht passiert. Auch von einer Detailseite und
        // von der 404-Seite aus, weil "egal auf welcher Seite" genau das heisst.
        var home = MeadowRoutes.HomeFor(route);

        Assert.Equal(expectLink, home is not null);

        if (expectLink)
        {
            Assert.Equal(MeadowRoutes.Dashboard, home);
        }
    }

    [Fact]
    public void Every_grouping_but_none_reads_a_tag()
    {
        // Stronger than KpiEvaluatorTests.Every_declared_grouping_is_evaluable_on_its_source, which
        // only checks the groupings a source happens to advertise. A member nobody advertises yet
        // is exactly the one that gets added without its mapping.
        foreach (var groupBy in Enum.GetValues<KpiGroupBy>())
        {
            if (groupBy == KpiGroupBy.None)
            {
                Assert.Null(KpiTagKeys.ForGroupBy(groupBy));
                continue;
            }

            Assert.False(
                string.IsNullOrEmpty(KpiTagKeys.ForGroupBy(groupBy)),
                $"{groupBy} bildet auf keinen Tag-Schluessel ab.");
        }
    }

    [Fact]
    public void Every_declared_grouping_produces_real_sql()
    {
        // KpiSqlBuilder.GroupExpression falls back to the literal "NULL" for an unmapped grouping.
        // That does not throw and does not look wrong: the script runs, ranks nothing and prints an
        // en dash. So assert on the generated text instead.
        foreach (var source in KpiSourceRegistry.All)
        {
            foreach (var grouping in source.Groupings)
            {
                var script = KpiSqlBuilder.Build(
                    new KpiDefinition
                    {
                        Source = source.Id,
                        Measure = KpiMeasure.TopValue,
                        GroupBy = grouping
                    },
                    source);

                Assert.DoesNotContain("NULL AS label", script.Sql);
            }
        }
    }

    [Fact]
    public void Every_generated_script_survives_the_script_guard()
    {
        // AdoptGeneratedSql writes the ANNOTATED script into KPI.Script, and saving runs it through
        // KpiScriptGuard. A note containing a semicolon would make the guard see two statements and
        // reject a KPI the user built in the form. Every combination, not just the seven shipped.
        foreach (var source in KpiSourceRegistry.All)
        {
            foreach (var measure in source.Measures)
            {
                var groupings = measure == KpiMeasure.TopValue
                    ? source.Groupings
                    : new[] { KpiGroupBy.None };

                foreach (var grouping in groupings)
                {
                    foreach (var timeframe in KpiTimeframes.All)
                    {
                        var definition = new KpiDefinition
                        {
                            Source = source.Id,
                            Measure = measure,
                            GroupBy = grouping,
                            Timeframe = timeframe,
                            CompareToPrevious = true,
                            Unit = "ml",
                            Decimals = 2
                        };

                        var annotated = KpiSqlBuilder.Build(definition, source).Annotated;
                        Assert.Null(KpiScriptGuard.Reject(annotated));
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(LabelSets))]
    public void Dialog_labels_are_unique(string what, string[] labels)
    {
        // The builder resolves a changed dropdown by comparing LABELS with FirstOrDefault. Two
        // options sharing a label would not throw: the second becomes unreachable and the selection
        // falls back to the zero value of the enum - Count, or All.
        var duplicates = labels
            .GroupBy(l => l, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            $"{what}: doppelte Beschriftung {string.Join(", ", duplicates)}");
    }

    public static TheoryData<string, string[]> LabelSets()
    {
        var data = new TheoryData<string, string[]>
        {
            { "Datenquellen", KpiSourceRegistry.All.Select(s => s.Label).ToArray() },
            {
                "Berechnungen",
                Enum.GetValues<KpiMeasure>().Select(KpiDefinitionSummary.MeasureLabel).ToArray()
            },
            {
                "Gruppierungen",
                Enum.GetValues<KpiGroupBy>()
                    .Where(g => g != KpiGroupBy.None)
                    .Select(KpiDefinitionSummary.GroupBy)
                    .ToArray()
            }
        };

        // Direction matters: a planned source says "naechste 30 Tage" where a recorded one says
        // "letzte 30 Tage", so both lists have to be checked on their own.
        foreach (var planned in new[] { false, true })
        {
            data.Add(
                planned ? "Zeitraeume (geplant)" : "Zeitraeume",
                KpiTimeframes.All.Select(t => KpiDefinitionSummary.TimeframeOption(t, planned)).ToArray());
        }

        return data;
    }

    [Fact]
    public void Every_tag_a_source_offers_is_named_and_distinct()
    {
        // A filter group without a key would render an always-empty multi-select, and two groups
        // sharing a key would overwrite each other in KpiDefinition.Filters.
        foreach (var source in KpiSourceRegistry.All)
        {
            foreach (var tag in source.Tags)
            {
                Assert.False(string.IsNullOrWhiteSpace(tag.Key), $"{source.Label}: Tag ohne Schluessel.");
                Assert.False(
                    string.IsNullOrWhiteSpace(tag.Label),
                    $"{source.Label}/{tag.Key}: Tag ohne Beschriftung.");
            }

            Assert.Equal(
                source.Tags.Select(t => t.Key).Distinct(StringComparer.Ordinal).Count(),
                source.Tags.Count);
        }
    }
}
