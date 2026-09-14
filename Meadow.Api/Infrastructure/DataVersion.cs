namespace Meadow.Api.Infrastructure;

/// <summary>Ein Zaehler je Tabelle. Die Reihenfolge ist egal, der Name nicht.</summary>
public enum DataScope
{
    Cows, Medicines, WhereHows, TreatmentReasons, ClawFindings, Udders,
    CowTreatments, ClawTreatments, PlannedCowTreatments, PlannedClawTreatments,
    Settings, Kpis
}

public interface IDataVersion
{
    /// <summary>Wandert in den X-Data-Version-Header jeder Antwort.</summary>
    string Token { get; }

    IReadOnlyDictionary<string, long> Scopes { get; }
    string BootId { get; }

    void Bump(DataScope scope);
    void BumpAll();
}

/// <summary>
/// Sagt einem Client, ob sich seit seinem letzten Aufruf etwas geaendert hat.
///
/// Die Datenschicht hat kein Aenderungsereignis - das steht so im Kommentar von
/// DemoResetBackgroundService.ReloadCachesAsync, und dort ist es auch das
/// Problem: die Dienste sind prozessweite Singletons mit unveraenderlichen
/// Caches, also musste der naechtliche Demo-Reset sie von Hand alle zehn neu
/// laden. Solange Oberflaeche und Daten im selben Prozess liegen, reicht das.
/// Sobald ein Tablet die Daten haelt, reicht es nicht mehr: es zeigt nach dem
/// Reset Zeilen mit IDs, die es nicht mehr gibt.
///
/// Die Boot-Id ist die wichtigere Haelfte. Ohne sie stuende ein Client bei 137,
/// saehe nach einem Neustart des Servers die 3 und schloesse daraus "nichts
/// Neues" - ein Rueckwaertszaehler sieht aus wie Stillstand.
/// </summary>
public sealed class DataVersion : IDataVersion
{
    private readonly string _bootId = Guid.NewGuid().ToString("N");
    private long _global;
    private readonly long[] _scopes = new long[Enum.GetValues<DataScope>().Length];

    public string BootId => _bootId;

    public string Token => $"{_bootId}:{Volatile.Read(ref _global)}";

    public IReadOnlyDictionary<string, long> Scopes =>
        Enum.GetValues<DataScope>()
            .ToDictionary(s => s.ToString(), s => Volatile.Read(ref _scopes[(int)s]));

    public void Bump(DataScope scope)
    {
        Interlocked.Increment(ref _scopes[(int)scope]);
        Interlocked.Increment(ref _global);
    }

    /// <summary>Nach dem naechtlichen Demo-Reset: alles ist weg und neu.</summary>
    public void BumpAll()
    {
        foreach (var scope in Enum.GetValues<DataScope>())
        {
            Interlocked.Increment(ref _scopes[(int)scope]);
        }
        Interlocked.Increment(ref _global);
    }
}
