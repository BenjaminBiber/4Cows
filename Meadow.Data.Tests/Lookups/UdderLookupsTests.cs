using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Die Fehltrefferregel des Euterviertel-Cache.
///
/// Sie zaehlt, weil die Aufrufstellen die vier Viertel direkt lesen, ohne
/// vorher auf null zu pruefen. Ein null an dieser Stelle waere kein falscher
/// Text, sondern ein abgerissener Circuit mitten im Behandlungsdialog -
/// deshalb kommt bei einer unbekannten ID ein leeres Viertel zurueck, dessen
/// ID int.MinValue ist und damit von der echten ID 0 unterscheidbar bleibt.
/// </summary>
public class UdderLookupsTests
{
    private static Dictionary<int, Udder> Udders(params Udder[] udders)
        => udders.ToDictionary(u => u.UdderId);

    [Fact]
    public void An_unknown_udder_id_yields_an_empty_record_and_never_null()
    {
        var udder = UdderLookups.GetById(Udders(), 7);

        Assert.NotNull(udder);
        Assert.False(udder.QuarterLV || udder.QuarterLH || udder.QuarterRV || udder.QuarterRH);
    }

    [Fact]
    public void The_empty_record_carries_int_MinValue_as_its_id_and_not_zero()
    {
        // Die 0 ist eine vergebbare Viertel-ID. Waere sie der Fehltreffer,
        // liese sich "nicht gefunden" nicht mehr von der ersten Zeile der
        // Tabelle unterscheiden.
        Assert.Equal(int.MinValue, UdderLookups.GetById(Udders(), 7).UdderId);
    }

    [Fact]
    public void The_sentinel_for_nothing_chosen_yields_the_empty_record_too()
    {
        // int.MinValue steht im Dialog fuer "noch nichts gewaehlt" und ist als
        // Schluessel nie vergeben.
        Assert.Equal(int.MinValue, UdderLookups.GetById(Udders(), int.MinValue).UdderId);
    }

    [Fact]
    public void A_known_udder_comes_back_unchanged()
    {
        var stored = new Udder(3, quarterLv: true, quarterLh: false, quarterRv: false, quarterRh: false);

        Assert.Same(stored, UdderLookups.GetById(Udders(stored), 3));
    }

    [Fact]
    public void An_unknown_id_counts_as_no_quarter_at_all()
    {
        Assert.False(UdderLookups.HasAnyQuarter(Udders(), 7));
    }

    [Fact]
    public void A_single_quarter_is_enough()
    {
        var stored = new Udder(3, quarterLv: false, quarterLh: false, quarterRv: false, quarterRh: true);

        Assert.True(UdderLookups.HasAnyQuarter(Udders(stored), 3));
    }

    [Fact]
    public void The_row_for_no_particular_quarter_counts_as_none()
    {
        // Die Tabelle fuehrt eine Zeile ohne gesetztes Viertel. Sie ist ein
        // gueltiger Eintrag und trotzdem keine Viertel-Angabe.
        var noQuarter = new Udder(1, quarterLv: false, quarterLh: false, quarterRv: false, quarterRh: false);

        Assert.True(Udders(noQuarter).ContainsKey(1));
        Assert.False(UdderLookups.HasAnyQuarter(Udders(noQuarter), 1));
    }
}
