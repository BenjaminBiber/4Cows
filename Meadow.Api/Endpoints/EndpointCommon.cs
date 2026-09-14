using System.Collections.Immutable;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Was sich ueber die zwoelf Ressourcen wiederholt: der Blick in den Cache und
/// die vier Fehlerantworten. Einmal formuliert, damit derselbe Fall nicht in
/// zwoelf Dateien zwoelf verschiedene Erklaerungen bekommt.
/// </summary>
internal static class EndpointCommon
{
    /// <summary>
    /// Cache-Treffer, sonst EIN Nachladen und dann erst "kennt keiner".
    ///
    /// Der reine Cache-Blick waere hier zu wenig: die Caches sind prozessweite
    /// Singletons, und im API-Betrieb waermt sie niemand vor - MeadowDataLoader
    /// haengt an den Razor-Seiten, nicht an /api. Direkt nach einem Neustart
    /// saehe ein GET auf eine vorhandene Zeile sonst eine 404, und ein DELETE
    /// liesse sich gar nicht mehr ausfuehren. Der Fehlgriff kostet einen
    /// Volldurchlauf, der Treffer nichts.
    /// </summary>
    internal static async Task<TValue?> FindAsync<TKey, TValue>(
        Func<ImmutableDictionary<TKey, TValue>> cache,
        TKey key,
        Func<Task> reload)
        where TKey : notnull
        where TValue : class
    {
        if (cache().TryGetValue(key, out var cached))
        {
            return cached;
        }

        await reload();
        return cache().TryGetValue(key, out var reloaded) ? reloaded : null;
    }

    internal static async Task<bool> ExistsAsync<TKey, TValue>(
        Func<ImmutableDictionary<TKey, TValue>> cache,
        TKey key,
        Func<Task> reload)
        where TKey : notnull
        where TValue : class
        => await FindAsync(cache, key, reload) is not null;

    internal static IResult NotFound(string resource, object id)
        => Results.Problem(
            title: "Nicht gefunden",
            detail: $"{resource} {id} gibt es nicht.",
            statusCode: StatusCodes.Status404NotFound);

    /// <summary>
    /// Antwort auf ein Loeschen, das der Dienst mit false quittiert, obwohl die
    /// Id vorher auffindbar war.
    ///
    /// RemoveByIdAsync unterscheidet drei Gruende nicht: noch in Benutzung
    /// (bewusster Rollback nach eigener Zaehlung), Zeile inzwischen weg
    /// (affectedRows == 0) und Ausnahme. Die 409 ist damit die
    /// WAHRSCHEINLICHSTE Deutung, nicht die sichere - sicher wuerde sie erst,
    /// wenn der Dienst den Grund mitliefert.
    /// </summary>
    internal static IResult StillInUse(string resource, object id)
        => Results.Problem(
            title: "Loeschen abgelehnt",
            detail: $"{resource} {id} liess sich nicht loeschen - der Eintrag wird vermutlich noch von Behandlungen verwendet.",
            statusCode: StatusCodes.Status409Conflict);

    /// <summary>
    /// null aus GetUsageCountsAsync heisst "konnte nicht gezaehlt werden" und
    /// NICHT "nirgends benutzt". Ein leeres Objekt im Rumpf gaebe dem Aufrufer
    /// den Papierkorb fuer jede Zeile frei.
    /// </summary>
    internal static IResult UsageCountsUnavailable(string resource)
        => Results.Problem(
            title: "Verwendungen nicht zaehlbar",
            detail: $"Die Verwendungen der {resource} konnten nicht gezaehlt werden. Ohne diese Zahlen darf nichts geloescht werden.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    /// <summary>
    /// Der Rest: die Dienste melden jeden Fehlschlag als false und schreiben
    /// den Grund ins Protokoll. Mehr als "hat nicht geklappt" laesst sich dem
    /// Aufrufer deshalb nicht sagen.
    /// </summary>
    internal static IResult WriteFailed(string what)
        => Results.Problem(
            title: "Schreiben fehlgeschlagen",
            detail: $"{what} Der Grund steht nur im Serverprotokoll - die Dienste geben ihn nicht zurueck.",
            statusCode: StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Antwort der fuenf Upsert-Endpunkte (/by-name, /by-quarters), wenn der
    /// Dienst seinen Fehlerwert geliefert hat.
    ///
    /// Die vier Dienste melden Scheitern nicht als false, sondern als SENTINEL
    /// im Rueckgabewert: int.MinValue bei Medikament, Wie/Wo und
    /// Behandlungsgrund, ClawFinding.FailedId (derselbe Wert) beim
    /// Klauenbefund. Diese Zahl darf keinesfalls als Id in den Rumpf.
    ///
    /// Ein Client, der sie fuer eine Id haelt, schreibt damit eine Behandlung
    /// - und -2147483648 ist keine Zahl, die beim Lesen auffaellt: sie sieht
    /// aus wie irgendein Schluessel, bis irgendwann jemand nach dem Namen
    /// dahinter sucht und keinen findet. Deshalb hier eine 500 statt
    /// {"id":-2147483648}.
    /// </summary>
    internal static IResult UpsertFailed(string resource, string name)
        => Results.Problem(
            title: "Anlegen fehlgeschlagen",
            detail: $"{resource} \"{name}\" liess sich weder finden noch anlegen. Der Grund steht nur im Serverprotokoll - der Dienst gibt ihn nicht zurueck.",
            statusCode: StatusCodes.Status500InternalServerError);

    internal static IResult Invalid(string field, string message)
        => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
