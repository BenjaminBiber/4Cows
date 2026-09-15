namespace Meadow.Client.Services;

/// <summary>Was mit der Zeile geschehen soll.</summary>
public enum MeadowOperation
{
    Insert = 0,
    Update = 1,
    Delete = 2
}

/// <summary>Wo der Eintrag in seinem Leben steht.</summary>
public enum OutboxState
{
    /// <summary>Wartet auf seinen naechsten Versuch.</summary>
    Pending = 0,

    /// <summary>Ist gerade unterwegs. Ueberlebt einen Absturz - siehe OutboxProcessor.</summary>
    InFlight = 1,

    /// <summary>Dauerhaft gescheitert. Nur ein Mensch bringt den noch weiter.</summary>
    Failed = 2
}

/// <summary>
/// Ein Schreibvorgang, der noch nicht beim Server angekommen ist.
///
/// Liegt im Object Store <c>outbox</c> mit keyPath <c>seq</c> und
/// autoIncrement - die Reihenfolge der Erfassung IST die Reihenfolge der
/// Uebertragung, und die vergibt IndexedDB, nicht dieser Code.
/// </summary>
public sealed record OutboxEntry
{
    /// <summary>Von IndexedDB vergeben. 0 heisst "noch nicht geschrieben".</summary>
    public long Seq { get; init; }

    /// <summary>
    /// Kennzeichnet den VORGANG, nicht die Zeile. Taucht in jeder Protokollzeile
    /// auf und macht aus "irgendein Eintrag scheiterte" eine Spur, der man von
    /// der Erfassung bis zur Antwort folgen kann.
    /// </summary>
    public Guid OperationId { get; init; }

    public MeadowEntityType EntityType { get; init; }

    public MeadowOperation Operation { get; init; }

    /// <summary>
    /// ClientIds[], nicht clientId.
    ///
    /// Add_Cow_Treatment_Dialog baut ein Kreuzprodukt aus Tieren mal
    /// Medikamenten, und InsertRangeAsync schreibt es in EINEM
    /// SaveChangesAsync - eine Transaktion, alles oder nichts. Ein
    /// Outbox-Eintrag pro Zeile braeche genau diese Zusage, sobald die
    /// Verbindung mitten im Stapel abreisst: sechs von zehn Gaben stuenden
    /// dann in der Datenbank und vier nicht, ohne dass es jemandem auffiele.
    /// Der Stapel ist die Einheit, also ist er auch der Eintrag.
    /// </summary>
    public string[] ClientIds { get; init; } = [];

    /// <summary>Fuer Update und Delete, die eine Server-Id brauchen.</summary>
    public int? ServerId { get; init; }

    /// <summary>Der fertige Anfragerumpf, mit MeadowJson serialisiert.</summary>
    public string Payload { get; init; } = "";

    public DateTimeOffset CreatedUtc { get; init; }

    public int Attempts { get; init; }

    public string? LastError { get; init; }

    /// <summary>
    /// Der Zustand gehoert in den Speicher und nicht in den Arbeitsspeicher.
    ///
    /// Ein dauerhaft gescheiterter Eintrag muss ein App-Schliessen ueberleben,
    /// sonst sieht er nach dem naechsten Start aus wie ein frischer und wird
    /// erneut gegen denselben Fehler gefahren. Der Landwirt schliesst die App -
    /// das ist keine Ausnahme, das ist der Normalfall.
    /// </summary>
    public OutboxState State { get; init; }

    /// <summary>
    /// Wann der naechste Versuch fruehestens laufen darf.
    ///
    /// Ebenfalls im Speicher und nicht nur im RAM: ein Backoff, der beim
    /// Neuladen der Seite zurueckspringt, haemmert einen Server, der ohnehin
    /// gerade weg ist - und im Stall ist "Seite neu geladen" die erste
    /// Massnahme, die jemand ergreift.
    /// </summary>
    public DateTimeOffset? NextAttemptUtc { get; init; }
}
