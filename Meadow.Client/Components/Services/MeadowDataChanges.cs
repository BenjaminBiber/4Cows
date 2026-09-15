using Meadow.Client.Services;

namespace Meadow.Client.Components.Services;

/// <summary>
/// Welche Datenart sich geaendert hat. Eine Seite laedt nur nach, wenn es
/// ihre eigene ist - sonst zahlte jede Tabelle die Datenbankrunden aller
/// anderen mit.
/// </summary>
public enum MeadowDataKind
{
    CowTreatment,
    ClawTreatment,
    PlannedCowTreatment,
    PlannedClawTreatment
}

/// <summary>
/// Sagt der gerade gerenderten Seite, dass sich Daten geaendert haben.
///
/// Hintergrund: die Hinzufuegen-Dialoge kommen nicht nur von der Tabelle
/// selbst, sondern auch vom FAB der Tabbar, vom Vier-Punkte-Menue und von
/// den Aktionen des Dashboards. Der Knopf auf der Tabellenseite ist
/// ausserdem .mw-only-desktop - mobil ist der FAB der EINZIGE Weg. Diese
/// Aufrufer sitzen im Layout und kennen die Seite nicht; sie konnten ihr
/// also nie sagen, dass eine Zeile dazugekommen ist. Der Eintrag erschien
/// erst nach dem Neuladen.
///
/// Scoped wie <see cref="LayoutState"/> und <see cref="ThemeState"/>: eine
/// Meldung gehoert in den eigenen Circuit, nicht in fremde Sitzungen.
/// </summary>
public sealed class MeadowDataChanges : IDisposable
{
    private readonly MeadowSyncState _sync;

    /// <summary>
    /// Seit Phase 4 kommt eine Meldung nicht mehr nur aus einem Dialog,
    /// sondern auch aus der Outbox: dort wird eine Behandlung fertig, die
    /// niemand gerade gespeichert hat.
    ///
    /// Der OutboxProcessor ist ein Singleton und kann diese Scoped-Klasse nicht
    /// annehmen - der Container prueft das und wirft
    /// ScopedInSingletonException. Andersherum geht es: er meldet an
    /// <see cref="MeadowSyncState"/>, und wir haengen uns hier an. Die
    /// .razor-Seiten abonnieren weiterhin nur <see cref="Changed"/> und merken
    /// von alledem nichts.
    /// </summary>
    public MeadowDataChanges(MeadowSyncState sync)
    {
        _sync = sync;
        _sync.DataArrived += Notify;
    }

    public event Action<MeadowDataKind>? Changed;

    public void Notify(MeadowDataKind kind) => Changed?.Invoke(kind);

    public void Dispose() => _sync.DataArrived -= Notify;
}
