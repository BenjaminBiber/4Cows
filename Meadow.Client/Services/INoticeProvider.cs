using Meadow.Shared.Models;

namespace Meadow.Client.Services;

/// <summary>
/// Eine Hinweisquelle. Task 5 (Verband-Reminder) ist die erste Implementierung;
/// der Kern kennt keine Quelle konkret.
///
/// Zwei Pflichten, dem Vorbild der Dienste nachempfunden:
/// <list type="bullet">
///   <item><see cref="GetNotices"/> liest SYNCHRON aus dem schon vorhandenen
///     Cache des Providers - genauso wie die dreizehn Dienste ihre
///     Tabellen-Caches synchron auslesen. Kein Netz, kein await: die Aggregation
///     im <see cref="MeadowNoticeState"/> muss guenstig genug sein, um bei jeder
///     Aenderung durchlaufen zu koennen.</item>
///   <item><see cref="Changed"/> feuert, wenn sich die Hinweise dieser Quelle
///     geaendert haben. Darauf haengt sich der Zustand und aggregiert neu - so
///     wie <c>MeadowSyncBand</c> sich an <c>MeadowSyncState.Changed</c> haengt.</item>
/// </list>
/// </summary>
public interface INoticeProvider
{
    /// <summary>
    /// Name der Quelle, landet in <see cref="MeadowNotice.Source"/>. Konstant je
    /// Provider, dient der Herkunft und Diagnose.
    /// </summary>
    string Source { get; }

    /// <summary>Die aktuellen Hinweise dieser Quelle, synchron aus dem Cache.</summary>
    IEnumerable<MeadowNotice> GetNotices();

    /// <summary>
    /// Meldet, dass sich die Hinweise dieser Quelle geaendert haben. Der
    /// <see cref="MeadowNoticeState"/> abonniert das und aggregiert daraufhin
    /// neu. Ohne Argument, wie <c>MeadowSyncState.Changed</c> - der Zustand liest
    /// ohnehin alle Provider neu ein.
    /// </summary>
    event Action? Changed;
}
