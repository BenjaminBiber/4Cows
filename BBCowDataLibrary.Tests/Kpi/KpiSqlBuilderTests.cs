using BB_Cow.Class;
using BB_Cow.Kpi;

namespace BBCowDataLibrary.Tests.Kpi;

/// <summary>
/// The generated script is what an author refines by hand in the expert mode, so it has to be
/// faithful rather than merely plausible. These tests pin the parts where "close enough" would
/// silently change the number.
/// </summary>
public class KpiSqlBuilderTests
{
    private static KpiSqlScript Build(KpiDefinition definition)
        => KpiSqlBuilder.Build(definition, KpiSourceRegistry.Find(definition.Source)!);

    private static string Sql(KpiDefinition definition) => Build(definition).Sql;

    // ---- Aggregates ----------------------------------------------------

    [Fact]
    public void Counts_rows_of_the_right_table()
    {
        var sql = Sql(new KpiDefinition { Source = KpiSourceId.ClawTreatment });

        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("FROM Claw_Treatment t", sql);
        Assert.Contains("AS value", sql);
        Assert.DoesNotContain("WHERE", sql);
    }

    [Fact]
    public void Formats_numbers_the_way_the_tile_does()
    {
        // The evaluator uses ToString("N0") under a German culture, so 1240 reads "1.240". A plain
        // CAST would print "1240" and the script would disagree with the preview above it.
        Assert.Contains("FORMAT(COUNT(*), 0, 'de_DE')", Sql(new KpiDefinition()));
    }

    [Fact]
    public void Counts_distinct_cows_on_the_column_that_actually_holds_the_cow_id()
    {
        var sql = Sql(new KpiDefinition { Measure = KpiMeasure.CountDistinctCows });

        Assert.Contains("COUNT(DISTINCT t.Ear_Tag_Number)", sql);
    }

    [Fact]
    public void Sums_dosage_with_the_unit_and_decimals_from_the_definition()
    {
        var sql = Sql(new KpiDefinition
        {
            Measure = KpiMeasure.SumDosage,
            Decimals = 2,
            Unit = "ml"
        });

        Assert.Contains("SUM(t.Medicine_Dosage)", sql);
        Assert.Contains(", 2, 'de_DE')", sql);
        Assert.Contains("'ml'", sql);
        // SUM over nothing is NULL in SQL but 0 in the evaluator.
        Assert.Contains("COALESCE(SUM", sql);
    }

    [Fact]
    public void An_average_over_nothing_prints_the_same_dash_as_the_tile()
    {
        var sql = Sql(new KpiDefinition { Measure = KpiMeasure.AvgDosage, Decimals = 1 });

        Assert.Contains("AVG(t.Medicine_Dosage)", sql);
        Assert.Contains("'–'", sql);
    }

    // ---- Timeframe -----------------------------------------------------

    [Fact]
    public void A_recorded_source_looks_back_and_excludes_the_future()
    {
        var sql = Sql(new KpiDefinition { Timeframe = KpiTimeframe.Days7 });

        Assert.Contains("DATE_SUB(CURDATE(), INTERVAL 7 DAY)", sql);
        Assert.Contains("<= CURDATE()", sql);
        Assert.DoesNotContain("DATE_ADD", sql);
    }

    [Fact]
    public void A_planned_source_looks_forward()
    {
        // Planned treatments are deliberately future-dated; looking back would return nothing.
        var sql = Sql(new KpiDefinition
        {
            Source = KpiSourceId.PlannedCowTreatment,
            Timeframe = KpiTimeframe.Days30
        });

        Assert.Contains(">= CURDATE()", sql);
        Assert.Contains("DATE_ADD(CURDATE(), INTERVAL 30 DAY)", sql);
        Assert.DoesNotContain("DATE_SUB", sql);
    }

    [Fact]
    public void A_source_without_a_date_gets_no_timeframe_condition()
    {
        var sql = Sql(new KpiDefinition { Source = KpiSourceId.Cow, Timeframe = KpiTimeframe.Days7 });

        Assert.DoesNotContain("CURDATE", sql);
    }

    // ---- Filters -------------------------------------------------------

    [Fact]
    public void A_single_filter_value_compares_instead_of_using_in()
    {
        var definition = new KpiDefinition
        {
            Filters = { [KpiTagKeys.Medicine] = new List<string> { "Penicillin" } }
        };

        var sql = Sql(definition);

        Assert.Contains("LEFT JOIN Medicine med ON t.Medicine_ID = med.Medicine_ID", sql);
        Assert.Contains("med.Medicine_Name = 'Penicillin'", sql);
    }

    [Fact]
    public void Several_values_of_one_group_become_an_in_list()
    {
        var definition = new KpiDefinition
        {
            Filters = { [KpiTagKeys.Medicine] = new List<string> { "Penicillin", "Cefa" } }
        };

        Assert.Contains("med.Medicine_Name IN ('Penicillin', 'Cefa')", Sql(definition));
    }

    [Fact]
    public void Separate_groups_are_and_ed_and_each_join_appears_once()
    {
        var definition = new KpiDefinition
        {
            Filters =
            {
                [KpiTagKeys.Medicine] = new List<string> { "Zink" },
                [KpiTagKeys.WhereHow] = new List<string> { "Euter" },
                [KpiTagKeys.Cow] = new List<string> { "104" }
            }
        };

        var sql = Sql(definition);

        Assert.Contains("AND", sql);
        Assert.Equal(1, Occurrences(sql, "LEFT JOIN Medicine"));
        Assert.Equal(1, Occurrences(sql, "LEFT JOIN WhereHow"));
        Assert.Contains("LEFT JOIN Cow c ON t.Ear_Tag_Number = c.Cow_ID", sql);
        Assert.Contains("CAST(c.Collar_Number AS CHAR) = '104'", sql);
    }

    [Fact]
    public void Boolean_groups_compare_against_the_same_labels_the_dialog_offers()
    {
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.Cow,
            Filters = { [KpiTagKeys.Herd] = new List<string> { KpiFlags.InHerd } }
        };

        var sql = Sql(definition);

        Assert.Contains("CASE WHEN t.IsGone THEN 'Abgang' ELSE 'Im Bestand' END = 'Im Bestand'", sql);
    }

    [Fact]
    public void Claw_findings_and_the_two_states_land_in_one_or_ed_condition()
    {
        // One group, OR-ed - exactly what the claw table's single multi-select does. Splitting them
        // would AND them and the script would count fewer rows than the tile.
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.ClawTreatment,
            Filters =
            {
                [KpiTagKeys.ClawFinding] = new List<string> { "Mortellaro", KpiFlags.Bandage }
            }
        };

        var sql = Sql(definition);

        Assert.Contains("TRIM(t.Claw_Finding_LV) = 'Mortellaro'", sql);
        Assert.Contains("TRIM(t.Claw_Finding_RH) = 'Mortellaro'", sql);
        // A removed bandage is not a bandage - same rule as Claw_Table.MatchesFinding.
        Assert.Contains("NOT t.IsBandageRemoved", sql);
        Assert.Contains(" OR ", sql);
    }

    [Fact]
    public void A_block_counts_regardless_of_whether_a_bandage_was_removed()
    {
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.ClawTreatment,
            Filters = { [KpiTagKeys.ClawFinding] = new List<string> { KpiFlags.Block } }
        };

        var sql = Sql(definition);

        Assert.Contains("t.Block_LV OR", sql);
        Assert.DoesNotContain("IsBandageRemoved", sql);
    }

    [Fact]
    public void Planned_claw_positions_filter_on_the_four_booleans()
    {
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.PlannedClawTreatment,
            Filters = { [KpiTagKeys.ClawPosition] = new List<string> { "LV", "RH" } }
        };

        var sql = Sql(definition);

        Assert.Contains("t.Claw_Finding_LV OR t.Claw_Finding_RH", sql);
        Assert.DoesNotContain("Claw_Finding_LH", sql);
    }

    [Fact]
    public void The_udder_foreign_key_is_spelled_per_table()
    {
        // Cow_Treatment calls it COW_QUARTER_ID, Planned_Cow_Treatment calls it Udder_ID.
        var recorded = new KpiDefinition
        {
            Filters = { [KpiTagKeys.UdderQuarter] = new List<string> { "LV" } }
        };
        var planned = new KpiDefinition
        {
            Source = KpiSourceId.PlannedCowTreatment,
            Filters = { [KpiTagKeys.UdderQuarter] = new List<string> { "LV" } }
        };

        Assert.Contains("t.COW_QUARTER_ID = u.UDDER_ID", Sql(recorded));
        Assert.Contains("t.Udder_ID = u.UDDER_ID", Sql(planned));
    }

    // ---- Escaping ------------------------------------------------------

    [Theory]
    [InlineData("Sohlengeschwür", "'Sohlengeschwür'")]
    [InlineData("O'Brien", "'O''Brien'")]
    [InlineData("a\\b", "'a\\\\b'")]
    public void String_literals_are_escaped(string value, string expected)
    {
        // The backslash matters: MySQL treats it as an escape character by default, so an unescaped
        // one would change what the query MEANS, not just how it reads.
        var definition = new KpiDefinition
        {
            Filters = { [KpiTagKeys.Medicine] = new List<string> { value } }
        };

        Assert.Contains(expected, Sql(definition));
    }

    // ---- Ranking -------------------------------------------------------

    [Fact]
    public void Ranking_by_cow_groups_by_the_animal_and_shows_its_collar()
    {
        var definition = new KpiDefinition
        {
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.Cow
        };

        var sql = Sql(definition);

        // The animal is the group, the collar is only the label - a re-issued collar number would
        // otherwise merge two cows into one row.
        Assert.Contains("CAST(c.Collar_Number AS CHAR) AS label", sql);
        Assert.Contains("t.Ear_Tag_Number AS cow", sql);
        Assert.Contains("GROUP BY cow, label", sql);
        Assert.Contains("LIMIT 1", sql);
    }

    [Fact]
    public void A_ranking_names_its_label_expression_once()
    {
        // The udder label alone is a long CASE. Repeating it in SELECT, WHERE, GROUP BY and ORDER BY
        // is equivalent but unreadable, and this script exists to be refined by hand.
        var sql = Sql(new KpiDefinition
        {
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.UdderQuarter
        });

        Assert.Equal(1, Occurrences(sql, "Quarter_LV AND u.Quarter_LH"));
        Assert.Contains("AS label", sql);
        Assert.Contains("GROUP BY label", sql);
        Assert.Contains("BINARY label ASC", sql);
    }

    [Theory]
    [InlineData(KpiSourceId.CowTreatment, KpiGroupBy.Medicine)]
    [InlineData(KpiSourceId.CowTreatment, KpiGroupBy.UdderQuarter)]
    [InlineData(KpiSourceId.CowTreatment, KpiGroupBy.Cow)]
    [InlineData(KpiSourceId.ClawTreatment, KpiGroupBy.ClawFinding)]
    public void A_ranking_over_nothing_still_returns_one_row(KpiSourceId source, KpiGroupBy groupBy)
    {
        // A GROUP BY that matches nothing returns NO rows, and a KPI script returning nothing ends
        // up as the old catch-all "--" - indistinguishable from a broken query. The evaluator prints
        // an en dash for an empty ranking, so the script has to as well.
        var sql = Sql(new KpiDefinition
        {
            Source = source,
            Measure = KpiMeasure.TopValue,
            GroupBy = groupBy
        });

        Assert.StartsWith("SELECT COALESCE((", sql);
        Assert.Contains("), '–') AS value;", sql);
    }

    [Fact]
    public void Ranking_always_has_a_tiebreaker()
    {
        // The shipped scripts used "ORDER BY COUNT(*) DESC LIMIT 1" with no tiebreaker, so a tie
        // resolved arbitrarily. BINARY makes it ordinal, matching the evaluator.
        var definition = new KpiDefinition
        {
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.Medicine
        };

        Assert.Contains("BINARY", Sql(definition));
    }

    [Fact]
    public void Ranking_by_udder_quarter_needs_no_sentinel_id()
    {
        // Replaces "WHERE c.UDDER_ID != 16": the sentinel row yields an empty label and is excluded
        // by that, so no id is hardcoded anywhere.
        var definition = new KpiDefinition
        {
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.UdderQuarter
        };

        var sql = Sql(definition);

        Assert.DoesNotContain("16", sql);
        Assert.Contains("<> ''", sql);
        Assert.Contains("IS NOT NULL", sql);
        Assert.Contains("'Alle 4'", sql);
    }

    [Fact]
    public void Ranking_by_claw_finding_counts_one_treatment_once_per_finding()
    {
        // UNION rather than UNION ALL: a treatment carrying the same finding on two claws must
        // count once, which is what de-duplicating (id, label) pairs achieves.
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.ClawTreatment,
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.ClawFinding
        };

        var sql = Sql(definition);

        Assert.Contains("UNION", sql);
        Assert.DoesNotContain("UNION ALL", sql);
        Assert.Contains("SELECT DISTINCT t.Claw_Treatment_ID AS id", sql);
        // The two synthetic states rank alongside the real findings, as they do in the evaluator.
        Assert.Contains("'Verband'", sql);
        Assert.Contains("'Klotz'", sql);
    }

    [Fact]
    public void Ranking_by_claw_finding_repeats_the_filters_in_every_branch()
    {
        var definition = new KpiDefinition
        {
            Source = KpiSourceId.ClawTreatment,
            Measure = KpiMeasure.TopValue,
            GroupBy = KpiGroupBy.ClawFinding,
            Timeframe = KpiTimeframe.Days30
        };

        var sql = Sql(definition);

        // Four findings plus bandage plus block.
        Assert.Equal(6, Occurrences(sql, "DATE_SUB(CURDATE(), INTERVAL 30 DAY)"));
    }

    // ---- Notes ---------------------------------------------------------

    [Fact]
    public void The_previous_period_comparison_is_reported_as_missing_not_dropped()
    {
        // A KPI script yields exactly one value, so the comparison genuinely cannot be expressed.
        // Saying so beats emitting SQL that quietly means something narrower.
        var script = Build(new KpiDefinition
        {
            Timeframe = KpiTimeframe.Days30,
            CompareToPrevious = true
        });

        Assert.NotEmpty(script.Notes);
        Assert.Contains("Vorzeitraum", string.Join(" ", script.Notes));
        Assert.StartsWith("--", script.Annotated);
        Assert.Contains(script.Sql, script.Annotated);
    }

    [Fact]
    public void A_definition_that_cannot_compare_produces_no_note()
    {
        var script = Build(new KpiDefinition { CompareToPrevious = true });

        Assert.Empty(script.Notes);
        Assert.Equal(script.Sql, script.Annotated);
    }

    // ---- Every seed ----------------------------------------------------

    [Theory]
    [MemberData(nameof(KpiSeedsTests.SeedTitles), MemberType = typeof(KpiSeedsTests))]
    public void Every_shipped_seed_translates_to_a_read_only_single_statement(string title)
    {
        // Whatever comes out must still satisfy the guard the expert mode applies on save -
        // otherwise the generated script could not be saved at all.
        var seed = KpiSeeds.Default.Single(s => s.Title == title);
        var script = KpiSqlBuilder.Build(seed.Definition, KpiSourceRegistry.Find(seed.Definition.Source)!);

        Assert.Null(KpiScriptGuard.Reject(script.Annotated));
        Assert.Contains("AS value", script.Sql);
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
