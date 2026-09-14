using Meadow.Data.Services;
using Meadow.Shared.Services;

namespace Meadow.Api.Infrastructure;

/// <summary>
/// Startet den XLink-Abgleich im Hintergrund und laesst genau einen davon
/// gleichzeitig zu.
///
/// Warum ueberhaupt im Hintergrund: XLinkService.RefreshCowsAsync hat keine
/// obere Schranke, die einer Antwortzeit nahekommt. Es holt bis zu MaxPages =
/// 1000 Seiten NACHEINANDER ueber einen statischen HttpClient mit dem
/// Standard-Timeout von 100 Sekunden je Seite, und schreibt danach fuer jede
/// abgegangene und jede neue Kuh einzeln in die Datenbank. Im schlechtesten
/// Fall sind das ueber 27 Stunden allein fuer die Abrufe. Synchron beantwortet
/// haette dieser Endpunkt eine Zeit, die jeder Aufrufer vorher abbricht - und
/// der Lauf liefe trotzdem weiter, nur ohne dass noch jemand sein Ergebnis
/// erfaehrt.
///
/// Warum EIN Lauf: zwei gleichzeitige Abgleiche arbeiten auf demselben
/// Kuh-Cache. Der eine markiert Tiere als abgegangen, die der andere gerade
/// erst angelegt hat; SaveCowData liest den Cache am Anfang einmal und trifft
/// seine IsGone-Entscheidung darauf.
/// </summary>
public sealed class XLinkRunner
{
    /// <summary>
    /// Obergrenze fuer einen Lauf. Ohne sie haengt ein Lauf an einem
    /// XLink-Host, der die Verbindung offen haelt, potenziell Stunden - und
    /// blockiert damit jeden weiteren Aufruf von TryStart, weil das
    /// running-Flag gesetzt bleibt. Zehn Minuten sind grosszuegig gegenueber
    /// dem beobachteten Fall (17 Seiten) und kurz genug, dass sich ein
    /// haengender Lauf von selbst aufloest.
    /// </summary>
    private static readonly TimeSpan MaxRunTime = TimeSpan.FromMinutes(10);

    private readonly IXLinkService _xLink;
    private readonly IDataVersion _version;

    /// <summary>
    /// 0 = frei, 1 = laeuft. Interlocked statt lock, weil der Endpunkt nur
    /// wissen will, ob er starten darf, und dafuer nicht warten soll: ein
    /// zweiter POST bekommt sofort seine 409 und nicht erst, wenn der erste
    /// Lauf fertig ist.
    /// </summary>
    private int _running;

    public XLinkRunner(IXLinkService xLink, IDataVersion version)
    {
        _xLink = xLink;
        _version = version;
    }

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>
    /// Startet einen Lauf, wenn keiner laeuft. Kehrt SOFORT zurueck; der
    /// Rueckgabewert sagt nur, ob gestartet wurde, nichts ueber das Ergebnis.
    /// Das steht danach in <see cref="IXLinkService.LastSyncSucceeded"/>.
    /// </summary>
    public bool TryStart()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return false;
        }

        // Bewusst verworfen und NICHT erwartet - das ist der Sinn der Uebung.
        // RunAsync faengt deshalb restlos alles: eine Ausnahme aus einer Task,
        // die niemand erwartet, kommt beim Aufraeumen des Finalizers wieder
        // hoch und beendet in der Voreinstellung den Prozess. Der Server waere
        // dann wegen eines nicht erreichbaren Scrapers weg.
        _ = Task.Run(RunAsync);
        return true;
    }

    private async Task RunAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(MaxRunTime);
            await _xLink.RefreshCowsAsync(cts.Token);

            // Erst NACH dem erfolgreichen Lauf, und nicht schon beim 202: zu
            // diesem Zeitpunkt hat sich wirklich etwas an der Kuh-Tabelle
            // geaendert. Deshalb traegt /xlink/refresh auch kein BumpsOnWrite -
            // der Filter sieht nur den Statuscode der Annahme.
            _version.Bump(DataScope.Cows);
        }
        catch (OperationCanceledException)
        {
            // Der eigene Zeitgeber, nicht das Herunterfahren: ein Lauf hat
            // MaxRunTime ueberschritten. RefreshCowsAsync hat den Fehlschlag
            // bereits in LastSyncError vermerkt, /xlink/status zeigt ihn an.
            LoggerService.LogWarning(typeof(XLinkRunner),
                "XLink-Abgleich nach {@Minutes} Minuten abgebrochen.", MaxRunTime.TotalMinutes);
        }
        catch (Exception e)
        {
            LoggerService.LogError(typeof(XLinkRunner),
                "Fehler beim angeforderten XLink-Abgleich: {@Message}", e, e.Message);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }
}
