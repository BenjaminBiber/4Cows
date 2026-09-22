using Meadow.Shared.Models;
using Meadow.Shared.Notices;

namespace Meadow.Client.Services;

/// <summary>
/// Der Sammelpunkt aller Hinweise. Baugleich zu <see cref="MeadowSyncState"/>:
/// ein Zustand mit einem <see cref="Changed"/>-Ereignis, an das sich eine
/// Komponente haengt und bei dem sie <c>StateHasChanged</c> ruft (Task 4). Nur
/// ohne dessen Zaehler - hier ist der Zustand die aggregierte Hinweisliste.
///
/// SINGLETON und nicht Scoped, aus demselben Grund wie <see cref="MeadowSyncState"/>:
/// Provider sind die Dienste (Singletons, die die Caches halten), und ein
/// Singleton darf keine Scoped-Abhaengigkeit annehmen. In WebAssembly gibt es
/// ohnehin genau einen Bereich pro Tab.
///
/// Provider-Registrierung: bewusst NICHT ueber Konstruktor-Injektion einer
/// <c>IEnumerable&lt;INoticeProvider&gt;</c>, sondern ueber <see cref="AddProvider"/>.
/// Task 5 registriert seinen Provider im Program.cs als Singleton und meldet ihn
/// danach hier an - eine Konstruktor-Liste wuerde alle Provider beim ersten
/// Aufloesen erzwingen und die Reihenfolge an die Registrierungsreihenfolge
/// binden. <see cref="AddProvider"/> abonniert das <see cref="INoticeProvider.Changed"/>
/// der Quelle; feuert es, wird neu aggregiert und <see cref="Changed"/> gemeldet.
/// </summary>
public sealed class MeadowNoticeState
{
    private readonly List<INoticeProvider> _providers = new();

    /// <summary>Die aggregierte, sortierte, nach Id deduplizierte Anzeigeliste.</summary>
    public IReadOnlyList<MeadowNotice> Notices { get; private set; } = Array.Empty<MeadowNotice>();

    /// <summary>
    /// Feuert nach jeder Neuaggregation. Abonnent ist die Anzeige-Komponente
    /// (Task 4), die daraufhin neu zeichnet - wie <c>MeadowSyncBand</c> an
    /// <c>MeadowSyncState.Changed</c>.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Meldet eine Quelle an. Idempotent: derselbe Provider wird nicht zweimal
    /// gefuehrt (sonst zaehlte sein Changed doppelt). Abonniert das Ereignis der
    /// Quelle und aggregiert sofort neu, damit die schon vorhandenen Hinweise
    /// der Quelle ohne erstes Changed sichtbar werden.
    /// </summary>
    public void AddProvider(INoticeProvider provider)
    {
        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        if (_providers.Contains(provider))
        {
            return;
        }

        _providers.Add(provider);
        provider.Changed += Reaggregate;
        Reaggregate();
    }

    /// <summary>
    /// Meldet eine Quelle ab und haengt ihr Ereignis aus. Fuer den Fall, dass
    /// ein Provider aufhoert zu liefern; ohne Aushaengen bliebe die
    /// Ereignis-Referenz haengen (Leck) und der abgemeldete Provider wuerde
    /// weiter mitaggregiert.
    /// </summary>
    public void RemoveProvider(INoticeProvider provider)
    {
        if (provider is null || !_providers.Remove(provider))
        {
            return;
        }

        provider.Changed -= Reaggregate;
        Reaggregate();
    }

    /// <summary>
    /// Aggregiert auf Anforderung neu - fuer den Fall, dass die Daten eines
    /// Providers sich gefuellt haben, ohne dass er es melden konnte.
    ///
    /// Genau das passiert beim Start: <c>AddProvider</c> laeuft in Program.cs
    /// vor <c>host.RunAsync()</c>, und die dortige Aggregation sieht noch leere
    /// Caches. Danach meldet <c>BandageReminderNoticeProvider</c> nur eigene
    /// Schreibvorgaenge (<c>DataArrived</c>), nicht aber das erste Laden durch
    /// <c>MeadowDataLoader</c> - ohne diesen Anstoss bliebe die Hinweistafel
    /// nach einem frischen Seitenaufruf dauerhaft leer.
    ///
    /// Ruft die Anzeige-Komponente, nachdem sie ihre Daten abgewartet hat.
    /// </summary>
    public void Refresh() => Reaggregate();

    /// <summary>
    /// Liest alle Provider synchron neu ein, fuehrt sie ueber
    /// <see cref="NoticeAggregation"/> zusammen und meldet die Aenderung. Aus DEN
    /// PROVIDERN und nicht aus einem mitgefuehrten Zwischenstand - der Cache der
    /// Provider ist die Wahrheit, alles andere waere eine zweite, die abweicht.
    /// </summary>
    private void Reaggregate()
    {
        Notices = NoticeAggregation.Aggregate(_providers.Select(p => p.GetNotices()));
        Changed?.Invoke();
    }
}
