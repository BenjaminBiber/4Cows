using Meadow.Client.Services;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Notices;

/// <summary>
/// Das Registrierungs- und Ereignisverhalten des Sammelpunkts. Der Dienst haengt
/// an keiner Browserumgebung - er haelt eine Liste und ein Ereignis -, deshalb
/// ist er hier direkt instanziierbar (wie die HTTP-Dienste in den Fallen-Tests,
/// die diesen Client-Verweis schon nutzen). Das deckt den Teil der Abnahme, der
/// ueber die reine Aggregation hinausgeht: ein Provider meldet sich an, der
/// Dienst nimmt seinen Hinweis auf und meldet die Aenderung per Event.
/// </summary>
public class MeadowNoticeStateTests
{
    /// <summary>Ein Provider, dessen Hinweise und Changed der Test steuert.</summary>
    private sealed class DummyProvider : INoticeProvider
    {
        private List<MeadowNotice> _notices;

        public DummyProvider(string source, params MeadowNotice[] notices)
        {
            Source = source;
            _notices = notices.ToList();
        }

        public string Source { get; }

        public IEnumerable<MeadowNotice> GetNotices() => _notices;

        public event Action? Changed;

        /// <summary>Tauscht die Hinweise und feuert Changed - wie ein Cache-Refresh.</summary>
        public void Publish(params MeadowNotice[] notices)
        {
            _notices = notices.ToList();
            Changed?.Invoke();
        }

        /// <summary>
        /// Tauscht die Hinweise OHNE Changed - der Fall, um den es beim Start
        /// geht: der Cache des Providers fuellt sich (Daten werden geladen),
        /// aber niemand meldet das, weil der Provider nur auf eigene
        /// Schreibvorgaenge horcht.
        /// </summary>
        public void FillSilently(params MeadowNotice[] notices) => _notices = notices.ToList();
    }

    private static MeadowNotice Notice(string id, MeadowNoticeSeverity severity = MeadowNoticeSeverity.Info, string source = "test")
        => new() { Id = id, Text = $"Hinweis {id}", Severity = severity, Source = source };

    [Fact]
    public void Registering_a_provider_takes_its_existing_notices_and_raises_changed()
    {
        // Der Abnahmefall: Dummy-Provider liefert einen Hinweis, der Dienst
        // aggregiert ihn und meldet die Aenderung per Event.
        var state = new MeadowNoticeState();
        var raised = 0;
        state.Changed += () => raised++;

        state.AddProvider(new DummyProvider("verband", Notice("a")));

        Assert.Equal(1, raised);
        var only = Assert.Single(state.Notices);
        Assert.Equal("a", only.Id);
    }

    [Fact]
    public void A_provider_change_reaggregates_and_raises_changed_again()
    {
        var state = new MeadowNoticeState();
        var provider = new DummyProvider("verband", Notice("a"));
        state.AddProvider(provider);

        var raised = 0;
        state.Changed += () => raised++;

        provider.Publish(Notice("a"), Notice("b"));

        Assert.Equal(1, raised);
        Assert.Equal(new[] { "a", "b" }, state.Notices.Select(n => n.Id));
    }

    [Fact]
    public void Two_providers_are_aggregated_together()
    {
        var state = new MeadowNoticeState();
        state.AddProvider(new DummyProvider("erste", Notice("a", MeadowNoticeSeverity.Info)));
        state.AddProvider(new DummyProvider("zweite", Notice("b", MeadowNoticeSeverity.Critical)));

        // Zusammengefuehrt und sortiert: Critical zuerst.
        Assert.Equal(new[] { "b", "a" }, state.Notices.Select(n => n.Id));
    }

    [Fact]
    public void The_same_provider_is_not_registered_twice()
    {
        var state = new MeadowNoticeState();
        var provider = new DummyProvider("verband", Notice("a"));

        state.AddProvider(provider);
        state.AddProvider(provider);

        // Kein doppelter Eintrag, sonst zaehlte sein Changed doppelt.
        Assert.Single(state.Notices);
    }

    [Fact]
    public void A_removed_provider_no_longer_contributes_or_fires()
    {
        var state = new MeadowNoticeState();
        var provider = new DummyProvider("verband", Notice("a"));
        state.AddProvider(provider);
        state.RemoveProvider(provider);

        Assert.Empty(state.Notices);

        var raised = 0;
        // Absichtlich NACH RemoveProvider abonnieren: so zaehlt das Event nur
        // fuer Aenderungen nach dem Abmelden - ein Changed beim Remove selbst
        // soll diesen Zaehler nicht beeinflussen.
        state.Changed += () => raised++;

        // Nach dem Abmelden darf ein Changed der Quelle nichts mehr ausloesen.
        provider.Publish(Notice("a"), Notice("b"));

        Assert.Equal(0, raised);
        Assert.Empty(state.Notices);
    }

    [Fact]
    public void Refresh_picks_up_notices_that_appeared_after_registration()
    {
        // Der Fehlerfall der Hinweis-Kachel: Program.cs meldet den Provider vor
        // host.RunAsync an - da ist der Behandlungs-Cache noch leer, die einzige
        // Aggregation aggregiert also nichts. Die Daten kommen erst danach, und
        // der Provider feuert sein Changed nur bei eigenen Schreibvorgaengen.
        // Ohne einen Anstoss von aussen bliebe die Tafel dauerhaft leer.
        var state = new MeadowNoticeState();
        var provider = new DummyProvider("verband");
        state.AddProvider(provider);

        Assert.Empty(state.Notices);

        // Daten treffen ein, ohne dass es jemand meldet.
        provider.FillSilently(Notice("bandage-72"));

        var raised = 0;
        state.Changed += () => raised++;

        state.Refresh();

        Assert.Equal(1, raised);
        var only = Assert.Single(state.Notices);
        Assert.Equal("bandage-72", only.Id);
    }
}
