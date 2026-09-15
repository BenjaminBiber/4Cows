using System.Collections.Immutable;
using Meadow.Client.Components.Services;

namespace Meadow.Client.Services;

/// <summary>Zustand einer einzelnen Zeile, aus Sicht der Uebertragung.</summary>
public enum MeadowRowState
{
    /// <summary>Beim Server angekommen. Der Normalfall.</summary>
    Synced,

    /// <summary>Liegt in der Outbox und wartet.</summary>
    Pending,

    /// <summary>Liegt in der Outbox und kommt allein nicht mehr weiter.</summary>
    Failed
}

/// <summary>
/// Was eine Oberflaeche ueber die Uebertragung wissen will - Zaehler, ein
/// Ereignis und die Frage "wie steht es um diese Zeile".
///
/// Diese Phase legt die Zustaende nur ab; das Statusband, die Zeilenmarkierung
/// und die Fehlerliste sind ein eigener Schritt und fassen keine .razor-Datei
/// an. Deshalb steht hier alles, was eine Komponente spaeter braucht, und
/// nichts, was sie nicht lesen kann.
///
/// SINGLETON und nicht Scoped wie LayoutState oder ThemeState, obwohl es
/// dieselbe Art Zustand ist: gelesen wird es von den dreizehn Diensten und vom
/// OutboxProcessor, und die sind Singletons, weil sie die Tabellen-Caches
/// halten. Ein Singleton darf keine Scoped-Abhaengigkeit annehmen - der
/// Container prueft das und wirft beim ersten Rendern
/// ScopedInSingletonException, wie im Kommentar in Program.cs beschrieben. In
/// WebAssembly gibt es ohnehin genau einen Bereich pro Tab.
/// </summary>
public sealed class MeadowSyncState
{
    private ImmutableDictionary<Guid, MeadowRowState> _rows = ImmutableDictionary<Guid, MeadowRowState>.Empty;

    /// <summary>Zeilen, die noch auf ihre Uebertragung warten.</summary>
    public int PendingCount { get; private set; }

    /// <summary>Zeilen, die dauerhaft gescheitert sind - ein Mensch muss ran.</summary>
    public int FailedCount { get; private set; }

    /// <summary>Die Outbox arbeitet gerade.</summary>
    public bool IsDraining { get; private set; }

    /// <summary>
    /// Wie oft der Server mit 200 statt 201 geantwortet hat.
    ///
    /// Das ist der wertvollste Diagnosewert dieser Phase: eine 200 heisst, dass
    /// ein frueherer Versuch committet hat und nur die Antwort verlorenging.
    /// Ohne den ClientId-Upsert stuende die Behandlung jetzt zweimal in der
    /// Datenbank.
    /// </summary>
    public int Replays { get; private set; }

    public DateTimeOffset? LastSyncUtc { get; private set; }

    public event Action? Changed;

    /// <summary>
    /// Brueckenereignis fuer <see cref="MeadowDataChanges"/>.
    ///
    /// Der OutboxProcessor ist Singleton, MeadowDataChanges ist Scoped - er
    /// kann es also nicht annehmen. Statt die Registrierung dort umzustellen
    /// (und damit ihre Begruendung zu entwerten) meldet der Prozessor hier, und
    /// MeadowDataChanges haengt sich an. Eine Scoped-Klasse darf eine
    /// Singleton-Abhaengigkeit haben; andersherum gilt es nicht.
    /// </summary>
    public event Action<MeadowDataKind>? DataArrived;

    /// <summary>
    /// Der Server meldet einen fremden Datenstand. Abonnent ist
    /// <see cref="MeadowDataLoader"/>, das daraufhin sein Invalidate ruft.
    /// </summary>
    public event Action? DataVersionChanged;

    /// <summary>
    /// Der Zustand dieser Zeile. Unbekannt heisst
    /// <see cref="MeadowRowState.Synced"/> - eine Zeile, von der die Outbox
    /// nichts weiss, ist entweder angekommen oder war nie hier.
    /// </summary>
    public MeadowRowState StateOf(Guid clientId)
        => _rows.TryGetValue(clientId, out var state) ? state : MeadowRowState.Synced;

    /// <summary>
    /// Rechnet die Zaehler aus der Outbox neu. Aus IHR und nicht aus
    /// mitgefuehrten Zaehlern: die Outbox ist der gespeicherte Zustand, alles
    /// andere waere eine zweite Wahrheit, die beim ersten Absturz abweicht.
    /// </summary>
    public void Apply(IReadOnlyCollection<OutboxEntry> entries)
    {
        var rows = ImmutableDictionary.CreateBuilder<Guid, MeadowRowState>();
        var pending = 0;
        var failed = 0;

        foreach (var entry in entries)
        {
            var state = entry.State == OutboxState.Failed ? MeadowRowState.Failed : MeadowRowState.Pending;

            foreach (var id in entry.ClientIds)
            {
                if (Guid.TryParse(id, out var clientId))
                {
                    rows[clientId] = state;
                }
            }

            if (state == MeadowRowState.Failed)
            {
                failed += entry.ClientIds.Length;
            }
            else
            {
                pending += entry.ClientIds.Length;
            }
        }

        _rows = rows.ToImmutable();
        PendingCount = pending;
        FailedCount = failed;
        Changed?.Invoke();
    }

    public void SetDraining(bool isDraining)
    {
        if (IsDraining == isDraining)
        {
            return;
        }

        IsDraining = isDraining;
        Changed?.Invoke();
    }

    public void CountReplay()
    {
        Replays++;
        Changed?.Invoke();
    }

    public void NoteSync(DateTimeOffset when)
    {
        LastSyncUtc = when;
        Changed?.Invoke();
    }

    public void NotifyData(MeadowDataKind kind) => DataArrived?.Invoke(kind);

    public void NotifyDataVersionChanged() => DataVersionChanged?.Invoke();
}
