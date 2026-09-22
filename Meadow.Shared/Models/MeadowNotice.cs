namespace Meadow.Shared.Models;

/// <summary>
/// Der Rang eines Hinweises. Bestimmt Farbe/Reihenfolge in der Anzeige (Task 4)
/// und die Dringlichkeit eines spaeteren Push (Task 6).
///
/// Drei Werte, final festgelegt und bewusst knapp gehalten:
/// <list type="bullet">
///   <item><see cref="Info"/> - eine Mitteilung, nichts ist zu tun.</item>
///   <item><see cref="Warning"/> - etwas sollte bald erledigt werden.</item>
///   <item><see cref="Critical"/> - etwas muss dringend erledigt werden.</item>
/// </list>
/// Die Reihenfolge der Deklaration ist zugleich die Rangfolge: hoehere Werte
/// stehen weiter oben, wenn <see cref="Notices.NoticeAggregation"/> sortiert.
/// Deshalb ist die Reihung Teil des Vertrags und darf nicht umgestellt werden.
/// </summary>
public enum MeadowNoticeSeverity
{
    /// <summary>Eine Mitteilung. Kein Handlungsbedarf.</summary>
    Info,

    /// <summary>Sollte bald erledigt werden.</summary>
    Warning,

    /// <summary>Muss dringend erledigt werden.</summary>
    Critical
}

/// <summary>
/// Ein einzelner Hinweis fuer die Oberflaeche - feature-agnostisch. Was ihn
/// erzeugt (Verband-Reminder, Server-Push, ...) weiss dieses Modell nicht; es
/// traegt nur, was zum Anzeigen und Deduplizieren noetig ist.
///
/// Unveraenderlich (init-only): ein Hinweis ist eine Momentaufnahme einer
/// Quelle. Neuberechnung heisst neues Objekt, nicht Mutation - so kann
/// <see cref="Notices.NoticeAggregation"/> Listen gefahrlos zusammenfuehren und
/// sortieren, ohne dass ein Abonnent eine halb geaenderte Instanz sieht.
///
/// Die Aktion ist ein <see cref="ActionHref"/>+<see cref="ActionLabel"/>-Paar,
/// KEIN Callback: das Modell liegt in Meadow.Shared und muss paketfrei und
/// serialisierbar bleiben (Task 6 schickt Hinweise vom Server per Push). Ein
/// Action-Delegat liesse sich nicht sinnvoll serialisieren. Braucht ein
/// spaeterer Client-Fall doch einen Callback, gehoert er an einen
/// client-seitigen Wrapper - nicht hierher.
/// </summary>
public sealed record MeadowNotice
{
    /// <summary>
    /// Stabile Kennung dieses Hinweises. "Stabil" heisst: dieselbe Quelle
    /// vergibt fuer denselben Sachverhalt ueber Neuberechnungen hinweg dieselbe
    /// Id. Darauf dedupliziert <see cref="Notices.NoticeAggregation"/> - liefern
    /// zwei Quellen denselben Sachverhalt (oder eine Quelle ihn doppelt), bleibt
    /// er nur einmal stehen.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Der Text, den der Nutzer liest.</summary>
    public required string Text { get; init; }

    /// <summary>Bestimmt Rang fuer die Sortierung (siehe <see cref="Notices.NoticeAggregation"/>) und die Farbe (Task 4).</summary>
    public MeadowNoticeSeverity Severity { get; init; } = MeadowNoticeSeverity.Info;

    /// <summary>
    /// Ziel der optionalen Aktion (z.B. "/verbaende"). <c>null</c>, wenn der
    /// Hinweis nur informiert. Ohne <see cref="ActionLabel"/> gibt es keine
    /// sinnvolle Schaltflaeche - beide gehoeren zusammen.
    /// </summary>
    public string? ActionHref { get; init; }

    /// <summary>Beschriftung der optionalen Aktion (z.B. "Ansehen").</summary>
    public string? ActionLabel { get; init; }

    /// <summary>
    /// Welche Quelle den Hinweis erzeugt hat (der Name des Providers). Dient der
    /// Herkunft und Diagnose; nicht Teil der Dedup-Regel - die haengt allein an
    /// <see cref="Id"/>, damit dieselbe Sache aus zwei Quellen zusammenfaellt.
    /// </summary>
    public required string Source { get; init; }
}
