using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using System.Text.Json;
using Meadow.Shared;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die Warteschlange der noch nicht uebertragenen Schreibvorgaenge.
///
/// Diese Klasse SCHREIBT und LIEST die Outbox; abgearbeitet wird sie in
/// <see cref="OutboxProcessor"/>. Die Trennung ist keine Kosmetik: die vier
/// Behandlungsdienste legen hier Eintraege ab, und der Prozessor kennt
/// dieselben vier Dienste, um die Antwort zurueckzuschreiben. Haengte beides
/// in einer Klasse, waere das ein Ring im Container.
/// </summary>
public sealed class MeadowOutbox
{
    private static readonly JsonSerializerOptions Json = MeadowJson.CreateOptions();

    private readonly MeadowLocalStore _store;
    private readonly MeadowSyncState _sync;
    private readonly ILogger<MeadowOutbox> _logger;

    /// <summary>
    /// Die zuletzt vergebene vorlaeufige Id. 0 heisst "noch nicht aus meta
    /// gelesen" - vergeben wird ab -1 abwaerts.
    /// </summary>
    private int _lastProvisionalId;

    public MeadowOutbox(MeadowLocalStore store, MeadowSyncState sync, ILogger<MeadowOutbox> logger)
    {
        _store = store;
        _sync = sync;
        _logger = logger;
    }

    /// <summary>
    /// Nimmt einen Insert an, der nicht hinausgehen konnte.
    ///
    /// Die Reihenfolge der Schritte ist die eigentliche Aussage dieser Methode:
    ///
    /// 1. Rumpf serialisieren, SOLANGE die Zeilen noch ihre Id 0 tragen.
    ///    Serverseitig ist der Schluessel eine Identity; EF schreibt einen
    ///    gesetzten Wert mit in das INSERT, und eine -3 als Primaerschluessel
    ///    waere kein Fehler, sondern eine Zeile, die auf ewig falsch liegt.
    /// 2. Outbox-Eintrag schreiben. Erst wenn der liegt, ist irgendetwas
    ///    versprochen - schlaegt das fehl, meldet diese Methode false und der
    ///    Dialog bleibt offen.
    /// 3. Vorlaeufige Ids vergeben und die Zeilen lokal ablegen. Scheitert das,
    ///    bleibt der Eintrag trotzdem stehen und geht spaeter hinaus; die Zeile
    ///    fehlt dann nur in der Tabelle, bis der naechste Neuabruf laeuft. Die
    ///    umgekehrte Reihenfolge verloere den Vorgang - und das ist der
    ///    Unterschied zwischen "sieht kurz komisch aus" und "Behandlung weg".
    /// </summary>
    public async Task<bool> QueueInsertAsync<TRow>(
        MeadowEntityType entityType,
        IReadOnlyList<TRow> rows,
        Func<TRow, Guid> clientIdOf,
        Action<TRow, int> assignProvisionalId)
        where TRow : class
    {
        if (rows.Count == 0)
        {
            return true;
        }

        if (!_store.IsAvailable)
        {
            // Ohne lokalen Speicher gibt es kein Versprechen, das sich halten
            // liesse. Dann lieber ehrlich scheitern: der Dialog bleibt offen,
            // die Eingaben stehen noch da.
            _logger.LogWarning("Kein lokaler Speicher - {Kind} kann nicht zwischengelagert werden.",
                MeadowStores.Describe(entityType));
            return false;
        }

        var payload = MeadowStores.IsBatch(entityType)
            ? JsonSerializer.Serialize(rows, Json)
            : JsonSerializer.Serialize(rows[0], Json);

        var entry = new OutboxEntry
        {
            OperationId = Guid.NewGuid(),
            EntityType = entityType,
            Operation = MeadowOperation.Insert,
            ClientIds = rows.Select(r => clientIdOf(r).ToString()).ToArray(),
            Payload = payload,
            CreatedUtc = DateTimeOffset.UtcNow,
            State = OutboxState.Pending
        };

        var seq = await _store.AppendOutboxAsync(entry);
        if (seq == 0)
        {
            return false;
        }

        var store = MeadowStores.StoreOf(entityType);

        foreach (var row in rows)
        {
            assignProvisionalId(row, await NextProvisionalIdAsync());
            await _store.PutRowAsync(store, row, pending: true);
        }

        _logger.LogInformation(
            "Outbox {Seq}: {Count} {Kind} zwischengelagert ({OperationId}).",
            seq, rows.Count, MeadowStores.Describe(entityType), entry.OperationId);

        await RefreshCountersAsync();
        return true;
    }

    /// <summary>
    /// Die naechste vorlaeufige Id - NEGATIV und absteigend.
    ///
    /// Cow_Table.razor benutzt RowKey="@(r => r.CowTreatmentId)". Zwei wartende
    /// Zeilen mit der 0 gaeben damit zweimal denselben Blazor-@key, und das ist
    /// ein Renderfehler und kein Datenfehler - die Tabelle zeigt eine Zeile,
    /// wo zwei stehen, oder wirft beim Diffen.
    ///
    /// Negativ, weil die Datenbank nur positive Identities vergibt: eine
    /// Kollision ist damit ausgeschlossen, nicht bloss unwahrscheinlich. Und im
    /// Debugger sieht man an der -3 sofort, woran man ist.
    ///
    /// Der Zaehler liegt in meta und nicht nur im RAM, sonst begaenne er nach
    /// jedem Neuladen wieder bei -1 - und traefe genau die wartenden Zeilen,
    /// die noch von vorhin dastehen.
    /// </summary>
    private async Task<int> NextProvisionalIdAsync()
    {
        if (_lastProvisionalId == 0)
        {
            var stored = await _store.GetMetaAsync(MeadowStores.MetaNextProvisionalId);
            _lastProvisionalId = int.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value < 0
                ? value
                : 0;
        }

        _lastProvisionalId--;
        await _store.SetMetaAsync(
            MeadowStores.MetaNextProvisionalId,
            _lastProvisionalId.ToString(CultureInfo.InvariantCulture));

        return _lastProvisionalId;
    }

    /// <summary>Alle Eintraege, aufsteigend nach seq - die Uebertragungsreihenfolge.</summary>
    public Task<List<OutboxEntry>> LoadAsync() => _store.ReadOutboxAsync();

    public Task UpdateAsync(OutboxEntry entry) => _store.UpdateOutboxAsync(entry);

    public Task RemoveAsync(long seq) => _store.DeleteOutboxAsync(seq);

    /// <summary>
    /// Nimmt eine noch nicht uebertragene Zeile aus der Outbox.
    ///
    /// Loescht der Nutzer eine Zeile, die den Server noch nie gesehen hat, darf
    /// kein DELETE hinausgehen - es traefe eine Id, die es dort nicht gibt.
    /// Zurueckgezogen werden muss stattdessen der Insert. Ohne das entfernt der
    /// Cache die Zeile, der Eintrag bleibt liegen, und beim naechsten Durchlauf
    /// steht sie wieder da: geloescht, und trotzdem wieder aufgetaucht.
    ///
    /// Ein Stapel wird dabei NICHT als Ganzes verworfen. Das Kreuzprodukt aus
    /// Tieren und Medikamenten liegt als ein Eintrag mit mehreren ClientIds;
    /// eine Zeile daraus zu loeschen darf die anderen nicht mitnehmen. Deshalb
    /// wird die Nutzlast selbst beschnitten - sie ist ein JSON-Array, in dem
    /// jedes Element seine clientId traegt, und das laesst sich ohne Kenntnis
    /// des Typs filtern.
    ///
    /// Ein Eintrag, der gerade uebertragen wird (InFlight), wird nicht
    /// angefasst: dort ist nicht mehr zu entscheiden, ob der Server ihn schon
    /// hat. Die Zeile kommt dann mit ihrer echten Id zurueck und laesst sich
    /// regulaer loeschen.
    /// </summary>
    /// <returns><c>true</c>, wenn der Insert zurueckgezogen wurde.</returns>
    public async Task<bool> CancelPendingInsertAsync(Guid clientId, string store)
    {
        var key = clientId.ToString();
        var entries = await LoadAsync();

        var entry = entries.FirstOrDefault(e =>
            e.Operation == MeadowOperation.Insert
            && e.State != OutboxState.InFlight
            && e.ClientIds.Contains(key, StringComparer.OrdinalIgnoreCase));

        if (entry is null)
        {
            return false;
        }

        var remaining = entry.ClientIds
            .Where(id => !string.Equals(id, key, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (remaining.Length == 0)
        {
            await RemoveAsync(entry.Seq);
            _logger.LogInformation(
                "Outbox-Eintrag {Seq} zurueckgezogen - die einzige Zeile darin wurde geloescht.", entry.Seq);
        }
        else
        {
            await UpdateAsync(entry with
            {
                ClientIds = remaining,
                Payload = RemoveFromPayload(entry.Payload, key)
            });
            _logger.LogInformation(
                "Eine Zeile aus Outbox-Eintrag {Seq} entfernt, {Rest} bleiben.", entry.Seq, remaining.Length);
        }

        await _store.DeleteRowAsync(store, key);
        await RefreshCountersAsync();
        return true;
    }

    /// <summary>
    /// Entfernt das Element mit dieser clientId aus einer JSON-Nutzlast. Die
    /// Nutzlast ist entweder ein Array (Stapel) oder ein einzelnes Objekt.
    /// Typfrei, damit es fuer alle vier Behandlungsarten dieselbe Stelle bleibt.
    /// </summary>
    private static string RemoveFromPayload(string payload, string clientId)
    {
        try
        {
            var node = JsonNode.Parse(payload);
            if (node is not JsonArray array)
            {
                return payload;
            }

            for (var i = array.Count - 1; i >= 0; i--)
            {
                var value = array[i]?["clientId"]?.GetValue<string>();
                if (string.Equals(value, clientId, StringComparison.OrdinalIgnoreCase))
                {
                    array.RemoveAt(i);
                }
            }

            return array.ToJsonString();
        }
        catch (JsonException)
        {
            // Lieber die Nutzlast unveraendert lassen als einen Eintrag
            // zerschiessen, der sonst uebertragbar waere.
            return payload;
        }
    }
    /// <summary>
    /// Rechnet die Zaehler in <see cref="MeadowSyncState"/> aus dem
    /// gespeicherten Zustand neu.
    /// </summary>
    public async Task RefreshCountersAsync()
    {
        var entries = await LoadAsync();
        _sync.Apply(entries);
    }
}
