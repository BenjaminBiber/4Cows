using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Macht aus einem Insert einen Upsert auf ClientId.
///
/// Der Client vergibt ClientId, bevor die Zeile den Server je gesehen hat.
/// Bricht die Verbindung nach dem Schreiben, aber vor der Antwort ab, schickt
/// die Outbox denselben Rumpf noch einmal - und dann muss der Server die
/// vorhandene Zeile zurueckgeben statt eine zweite anzulegen.
///
/// Die eigentliche Garantie ist der eindeutige Index, nicht die Pruefung hier.
/// Die Pruefung ist der schnelle Weg; der Index ist der verlaessliche. Deshalb
/// gibt es auch den Nachschlag nach einem gescheiterten Insert: zwei Tabs, die
/// im selben Moment denselben Stapel wiederholen, kommen beide an der Pruefung
/// vorbei, und genau einer verliert am Index.
/// </summary>
public static class ClientIdUpsert
{
    /// <summary>Was vor dem Insert zu tun ist: entweder sofort antworten, oder einfuegen.</summary>
    public sealed record Decision(IResult? Answer);

    public static async Task<Decision> PrepareAsync<T>(
        IDbContextFactory<DatabaseContext> factory,
        IReadOnlyList<T> rows,
        Func<T, Guid> clientIdOf,
        Func<DatabaseContext, List<Guid>, Task<List<T>>> findByClientIds,
        string entityName)
    {
        var ids = rows.Select(clientIdOf).ToList();

        // Guid.Empty ist kein gueltiger Schluessel, sondern ein vergessener.
        // new CowTreatment() traegt seit dem Property-Initialisierer immer einen
        // echten Wert; eine leere GUID kann nur aus einem handgeschriebenen
        // Rumpf kommen. Sie durchzulassen hiesse, dass der Index sie faengt -
        // aber erst bei der ZWEITEN solchen Zeile, waehrend die erste still
        // durchgeht. Lieber sofort und deutlich.
        if (ids.Any(id => id == Guid.Empty))
        {
            return new Decision(EndpointCommon.Invalid(
                "clientId", $"Jede {entityName} braucht eine ClientId; Guid.Empty ist keine."));
        }

        if (ids.Distinct().Count() != ids.Count)
        {
            return new Decision(EndpointCommon.Invalid(
                "clientId", "Der Stapel enthaelt dieselbe ClientId mehrfach."));
        }

        await using var context = await factory.CreateDbContextAsync();
        var known = await findByClientIds(context, ids);

        if (known.Count == rows.Count)
        {
            // Wiederholung. Derselbe Zustand wie beim ersten Mal, nur mit 200
            // statt 201 - und das ist der wertvollste Diagnosewert der ganzen
            // Phase: eine 200 hier heisst, dass das Netz einmal gelogen hat.
            // Ein frueherer Versuch hat committet, nur die Antwort ging
            // verloren. Der Client zaehlt das mit und zeigt keinen zweiten
            // "gespeichert"-Hinweis.
            return new Decision(Results.Ok(known));
        }

        if (known.Count != 0)
        {
            // Teilweise bekannt. Kann durch den Stapel selbst nicht entstehen -
            // InsertRangeAsync ist ein einziges SaveChangesAsync, also eine
            // Transaktion, alles oder nichts. Wer hier landet, hat von Hand in
            // der Datenbank gearbeitet; dann ist Raten schlechter als Sagen.
            return new Decision(Results.Conflict(new
            {
                title = "Teilweise bereits uebertragen",
                detail = $"{known.Count} von {rows.Count} Zeilen liegen bereits vor.",
                clientIds = known.Select(clientIdOf).ToArray()
            }));
        }

        return new Decision(null);
    }

    /// <summary>
    /// Nach einem gescheiterten Insert: liegt die Zeile inzwischen doch da, hat
    /// ein anderer Aufrufer das Rennen am eindeutigen Index gewonnen. Das ist
    /// kein Fehler, sondern genau das gewuenschte Ergebnis.
    ///
    /// Die Dienste fangen ihre Ausnahmen selbst und melden nur false, deshalb
    /// wird hier nachgesehen statt auf MySqlException 1062 geprueft.
    /// </summary>
    public static async Task<IResult?> RaceWinnerAsync<T>(
        IDbContextFactory<DatabaseContext> factory,
        IReadOnlyList<T> rows,
        Func<T, Guid> clientIdOf,
        Func<DatabaseContext, List<Guid>, Task<List<T>>> findByClientIds)
    {
        await using var context = await factory.CreateDbContextAsync();
        var known = await findByClientIds(context, rows.Select(clientIdOf).ToList());
        return known.Count == rows.Count ? Results.Ok(known) : null;
    }
}
