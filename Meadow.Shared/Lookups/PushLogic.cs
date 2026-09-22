using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Die reinen Entscheidungen rund um Web Push, herausgezogen aus Endpunkt und
/// Sender, damit sie ohne Netz und ohne Browser pruefbar sind.
///
/// Dieselbe Haltung wie bei <see cref="SettingsLookups"/>: die Regel, die
/// wirklich weh tut, wenn sie kippt (ist Push konfiguriert? ist eine Anmeldung
/// tot? ist ein eingehender Antrag ueberhaupt vollstaendig?), steht an genau
/// EINER Stelle und wird dort getestet - nicht verstreut in
/// try/catch-Bloecken, die kein Test je betritt.
/// </summary>
public static class PushLogic
{
    /// <summary>
    /// Ob VAPID vollstaendig konfiguriert ist. Fehlt eines der drei Stuecke,
    /// bleibt Push aus - der Start reisst NICHT ab (Muster wie DB_PORT/Demo).
    ///
    /// Whitespace zaehlt wie Fehlen: ein Docker-Secret, das als leere
    /// Umgebungsvariable ankommt, oder ein aus Versehen mit Leerzeichen
    /// gefuelltes Feld darf nicht als "konfiguriert" durchgehen und die
    /// WebPush-Lib erst tief im Sendeweg mit einer Ausnahme abwuergen.
    /// </summary>
    public static bool IsConfigured(string? publicKey, string? privateKey, string? subject)
        => !string.IsNullOrWhiteSpace(publicKey)
           && !string.IsNullOrWhiteSpace(privateKey)
           && !string.IsNullOrWhiteSpace(subject);

    /// <summary>
    /// Ob ein eingehender Anmelde-Antrag brauchbar ist. Ohne Endpoint gibt es
    /// kein Ziel, ohne die beiden Schluessel keine verschluesselbare Nutzlast -
    /// dann ist die Zeile wertlos und darf gar nicht erst gespeichert werden.
    /// </summary>
    public static bool IsValidSubscription(string? endpoint, string? p256dh, string? auth)
        => !string.IsNullOrWhiteSpace(endpoint)
           && !string.IsNullOrWhiteSpace(p256dh)
           && !string.IsNullOrWhiteSpace(auth);

    /// <summary>
    /// Vereinheitlicht einen Endpoint fuer den Schluesselvergleich: aussen
    /// getrimmt. Der Endpoint ist eine URL, deren Pfad GROSS-/klein-empfindlich
    /// ist (er traegt ein Registrierungs-Token), also wird NICHTS
    /// kleingeschrieben - nur umgebende Leerzeichen fallen weg, die ein Client
    /// beim Serialisieren mitschicken koennte. Ein leerer/whitespace-Endpoint
    /// wird zu string.Empty, damit der Aufrufer ihn eindeutig als "nichts"
    /// erkennt.
    /// </summary>
    public static string NormalizeEndpoint(string? endpoint)
        => string.IsNullOrWhiteSpace(endpoint) ? string.Empty : endpoint.Trim();

    /// <summary>
    /// Entscheidet, ob ein POST einer Anmeldung eine neue Zeile anlegt oder eine
    /// vorhandene aktualisiert - anhand des normalisierten Endpoints.
    ///
    /// Reine Funktion ueber die schon geladene Menge, damit die Idempotenz des
    /// Upserts testbar ist, ohne eine Datenbank zu bemuehen: derselbe Endpoint
    /// zweimal ergibt <see cref="PushUpsertKind.Update"/> auf DIESELBE Zeile,
    /// niemals eine zweite.
    /// </summary>
    public static PushUpsertDecision DecideUpsert(
        IEnumerable<PushSubscription> existing,
        string? endpoint)
    {
        var key = NormalizeEndpoint(endpoint);
        var match = existing.FirstOrDefault(s => NormalizeEndpoint(s.Endpoint) == key);
        return match is null
            ? new PushUpsertDecision(PushUpsertKind.Insert, null)
            : new PushUpsertDecision(PushUpsertKind.Update, match);
    }

    /// <summary>
    /// Ob eine Sende-Antwort bedeutet, dass die Anmeldung TOT ist und aus der
    /// Datenbank fliegen muss.
    ///
    /// 404/410 sind die beiden Codes, mit denen ein Push-Dienst "diesen
    /// Endpoint gibt es nicht mehr" sagt (Nutzer hat die App/das Abo entfernt).
    /// Genau diese - und nur diese - fuehren zum Loeschen. Ein 500 des
    /// Push-Dienstes oder ein Netzfehler ist NICHT das Aus der Anmeldung; die
    /// Nachricht darf spaeter erneut versucht werden, die Zeile bleibt stehen.
    /// </summary>
    public static bool IsGone(int statusCode)
        => statusCode is 404 or 410;
}

/// <summary>Ob ein Upsert einlegt oder aktualisiert.</summary>
public enum PushUpsertKind
{
    Insert,
    Update
}

/// <summary>
/// Ergebnis von <see cref="PushLogic.DecideUpsert"/>: bei
/// <see cref="PushUpsertKind.Update"/> traegt <see cref="Existing"/> die zu
/// aktualisierende Zeile, bei Insert ist es <c>null</c>.
/// </summary>
public sealed record PushUpsertDecision(PushUpsertKind Kind, PushSubscription? Existing);
