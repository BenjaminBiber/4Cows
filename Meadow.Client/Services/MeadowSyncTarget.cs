using System.Text.Json;

namespace Meadow.Client.Services;

/// <summary>
/// Was <see cref="OutboxProcessor"/> von einem Behandlungsdienst braucht, um
/// eine bestaetigte Antwort zurueckzuschreiben.
///
/// Als eigene, schmale Naht und nicht ueber die vier Dienstschnittstellen: die
/// dreizehn Schnittstellen in Meadow.Shared beschreiben, was die OBERFLAECHE
/// von einem Dienst erwartet, und sie bleiben unveraendert. Das hier ist eine
/// reine Client-Angelegenheit und hat in Meadow.Shared nichts verloren.
/// </summary>
public interface IMeadowSyncTarget
{
    MeadowEntityType EntityType { get; }

    /// <summary>
    /// Traegt die Zeilen aus einem Antwortrumpf in den Cache ein und wirft dabei
    /// die vorlaeufigen Ids weg.
    /// </summary>
    /// <returns>
    /// Dieselben Zeilen als Objekte - der Prozessor legt sie danach in den
    /// lokalen Speicher, und dafuer braucht er sie, nicht ihre Ids.
    /// </returns>
    IReadOnlyList<object> ApplySyncedRows(string responseJson);
}

/// <summary>
/// Liest den Antwortrumpf eines Upserts.
/// </summary>
public static class MeadowSyncPayload
{
    /// <summary>
    /// Nimmt sowohl ein Objekt als auch eine Liste.
    ///
    /// Das ist keine Grosszuegigkeit, sondern noetig: die beiden
    /// Einzel-Endpunkte antworten auf 201 mit der ANGELEGTEN ENTITAET, auf 200
    /// - also bei einer Wiederholung - dagegen mit der LISTE, die der
    /// ClientId-Upsert gefunden hat (Results.Ok(known) in ClientIdUpsert.cs).
    /// Beides sind richtige Antworten auf dieselbe Anfrage; wer nur eine davon
    /// liest, faellt genau im Wiederholungsfall um - also in dem Fall, fuer den
    /// die ganze Outbox gebaut ist.
    /// </summary>
    public static List<TRow> ReadRows<TRow>(string responseJson, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return [];
        }

        using var document = JsonDocument.Parse(responseJson);

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<TRow>>(responseJson, options) ?? [];
        }

        var single = JsonSerializer.Deserialize<TRow>(responseJson, options);
        return single is null ? [] : [single];
    }
}
