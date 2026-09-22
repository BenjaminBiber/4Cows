using Meadow.Shared.Models;
using Meadow.Shared.Notices;

namespace Meadow.Data.Tests.Notices;

/// <summary>
/// Die reine Zusammenfuehrung der Hinweisquellen. Das ist die Abnahme-Einheit:
/// ein Dummy-Provider liefert einen Hinweis, die Aggregation nimmt ihn auf und
/// haelt ihre beiden Zusicherungen - Dedup nach Id und deterministische
/// Sortierung. Ohne Client, ohne DI, ohne bUnit, wie die Lookups-Tests daneben.
/// </summary>
public class NoticeAggregationTests
{
    private static MeadowNotice Notice(string id, MeadowNoticeSeverity severity = MeadowNoticeSeverity.Info, string source = "test")
        => new() { Id = id, Text = $"Hinweis {id}", Severity = severity, Source = source };

    [Fact]
    public void A_single_source_notice_is_carried_through()
    {
        // Der Abnahmefall: eine Quelle, ein Hinweis - er kommt aggregiert heraus.
        var result = NoticeAggregation.Aggregate(new[] { new[] { Notice("a") } });

        var only = Assert.Single(result);
        Assert.Equal("a", only.Id);
    }

    [Fact]
    public void Null_sources_argument_throws_ArgumentNullException()
    {
        // Schutz des oeffentlichen Eintrittspunkts: ein null-Argument ist kein
        // gueltiger Aufruf und muss klar abgelehnt werden, bevor die foreach
        // eine NRE wirft.
        Assert.Throws<ArgumentNullException>(() => NoticeAggregation.Aggregate(null!));
    }

    [Fact]
    public void No_sources_read_empty()
    {
        Assert.Empty(NoticeAggregation.Aggregate(Array.Empty<IEnumerable<MeadowNotice>?>()));
    }

    [Fact]
    public void A_null_source_is_skipped_instead_of_throwing()
    {
        // Eine Quelle ohne Hinweise liefert null - der Aufrufer soll nicht vorab
        // filtern muessen.
        var result = NoticeAggregation.Aggregate(new IEnumerable<MeadowNotice>?[] { null, new[] { Notice("a") } });

        Assert.Single(result);
    }

    [Fact]
    public void The_same_id_from_two_sources_survives_only_once()
    {
        // Zwei Quellen melden denselben Sachverhalt - er darf nur einmal stehen.
        var result = NoticeAggregation.Aggregate(new[]
        {
            new[] { Notice("dup", source: "erste") },
            new[] { Notice("dup", source: "zweite") }
        });

        var only = Assert.Single(result);
        // Der zuerst gesehene ueberlebt - die zweite Quelle ueberschreibt nicht.
        Assert.Equal("erste", only.Source);
    }

    [Fact]
    public void A_source_that_repeats_an_id_is_deduplicated()
    {
        var result = NoticeAggregation.Aggregate(new[] { new[] { Notice("x"), Notice("x") } });

        Assert.Single(result);
    }

    [Fact]
    public void More_severe_notices_sort_to_the_top()
    {
        var result = NoticeAggregation.Aggregate(new[]
        {
            new[]
            {
                Notice("b", MeadowNoticeSeverity.Info),
                Notice("a", MeadowNoticeSeverity.Critical),
                Notice("c", MeadowNoticeSeverity.Warning)
            }
        });

        Assert.Equal(new[] { MeadowNoticeSeverity.Critical, MeadowNoticeSeverity.Warning, MeadowNoticeSeverity.Info },
            result.Select(n => n.Severity));
    }

    [Fact]
    public void Equal_severity_sorts_by_id_regardless_of_source_order()
    {
        // Der Determinismus: dieselbe Menge in umgekehrter Provider-Reihenfolge
        // muss dieselbe Ausgabe liefern, sonst flackert die Anzeige.
        var forward = NoticeAggregation.Aggregate(new[]
        {
            new[] { Notice("m"), Notice("a"), Notice("z") }
        });

        var reversed = NoticeAggregation.Aggregate(new[]
        {
            new[] { Notice("z"), Notice("m"), Notice("a") }
        });

        Assert.Equal(new[] { "a", "m", "z" }, forward.Select(n => n.Id));
        Assert.Equal(forward.Select(n => n.Id), reversed.Select(n => n.Id));
    }
}
