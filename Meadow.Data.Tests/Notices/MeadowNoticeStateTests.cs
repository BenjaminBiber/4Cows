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
        state.Changed += () => raised++;

        // Nach dem Abmelden darf ein Changed der Quelle nichts mehr ausloesen.
        provider.Publish(Notice("a"), Notice("b"));

        Assert.Equal(0, raised);
        Assert.Empty(state.Notices);
    }
}
