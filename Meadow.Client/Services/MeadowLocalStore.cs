using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Meadow.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Meadow.Client.Services;

/// <summary>
/// Die C#-Huelle um meadow-db.js.
///
/// JEDE Methode ist fehlertolerant. IndexedDB ist kein verlaesslicher Dienst,
/// sondern ein Angebot des Browsers: im privaten Modus von Safari wirft schon
/// das Oeffnen, bei vollem Speicher wirft das Schreiben, und ein Nutzer darf
/// den Speicher jederzeit von Hand leeren. Reisst einer dieser Faelle die App
/// ab, hat der lokale Speicher genau das kaputtgemacht, wogegen er gebaut
/// wurde.
///
/// Das Muster ist woertlich das aus ThemeState: JSException fangen, auf den
/// dokumentierten Rueckfallwert gehen, weiterlaufen. Der Unterschied ist nur,
/// dass hier "weiterlaufen" heisst: die App laeuft ohne lokalen Speicher,
/// also wie vor dieser Phase.
/// </summary>
public sealed class MeadowLocalStore
{
    private static readonly JsonSerializerOptions Json = MeadowJson.CreateOptions();

    private readonly IJSRuntime _js;
    private readonly ILogger<MeadowLocalStore> _logger;

    /// <summary>
    /// Schaltet sich nach einem gescheiterten Oeffnen ab.
    ///
    /// Nur beim Oeffnen und nicht bei jedem Fehler: ein einzelnes
    /// QuotaExceededError betrifft eine Tabelle, ein gescheitertes open
    /// betrifft alles. Ohne diese Unterscheidung schaltete ein voller Speicher
    /// auch das Lesen ab - und Lesen ist genau das, was dann noch geht.
    /// </summary>
    private bool _unavailable;

    private bool _opened;

    public MeadowLocalStore(IJSRuntime js, ILogger<MeadowLocalStore> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <summary>
    /// Ob der lokale Speicher benutzbar ist. Wer etwas ablegen MUSS, bevor er
    /// dem Nutzer Erfolg meldet - die Outbox -, fragt vorher hier.
    /// </summary>
    public bool IsAvailable => !_unavailable;

    public async Task<bool> OpenAsync()
    {
        if (_unavailable)
        {
            return false;
        }

        if (_opened)
        {
            return true;
        }

        try
        {
            await _js.InvokeVoidAsync("meadowDb.open");
            _opened = true;
            return true;
        }
        catch (Exception ex)
        {
            _unavailable = true;
            _logger.LogWarning(ex, "Lokaler Speicher nicht verfuegbar - die App laeuft ohne ihn weiter.");
            return false;
        }
    }

    // ---- Tabellen -----------------------------------------------------------

    /// <summary>
    /// Der gespeicherte Schnappschuss einer Tabelle, als JSON-Rumpf - genau in
    /// der Form, in der der Server ihn geliefert hat.
    ///
    /// Bewusst als Zeichenkette und nicht als Objekt: der Rumpf geht unveraendert
    /// an denselben Deserialisierer, der sonst die Antwort liest. Ein zweiter
    /// Weg in die Modelle waere ein zweiter Ort, an dem der JSON-Vertrag
    /// auseinanderlaufen kann.
    /// </summary>
    public async Task<string?> ReadTableJsonAsync(string store)
    {
        if (!await OpenAsync())
        {
            return null;
        }

        try
        {
            return await _js.InvokeAsync<string?>("meadowDb.getAllJson", store);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lokale Tabelle {Store} konnte nicht gelesen werden.", store);
            return null;
        }
    }

    /// <summary>
    /// Schreibt den Schnappschuss einer Tabelle.
    ///
    /// <paramref name="keepPending"/> fuer die vier Behandlungstabellen: die
    /// Server-Kopie ueberschreibt ihren eigenen Datensatz (gleiche clientId),
    /// die noch wartende Zeile bleibt stehen. Ohne das waere jeder Neuabruf
    /// ein Datenverlust fuer alles, was offline erfasst wurde.
    /// </summary>
    public async Task WriteTableJsonAsync(string store, string json, bool keepPending)
    {
        if (!await OpenAsync())
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("meadowDb.putAllJson", store, json, keepPending);
        }
        catch (Exception ex)
        {
            // Kein Abbruch: die Antwort ist da, die Tabelle ist gefuellt, nur
            // der naechste Kaltstart faellt aermer aus.
            _logger.LogWarning(ex, "Lokale Tabelle {Store} konnte nicht geschrieben werden.", store);
        }
    }

    /// <summary>
    /// Legt eine einzelne Zeile ab. <paramref name="pending"/> setzt das
    /// Wartemerkmal; die bestaetigte Server-Kopie ersetzt die wartende Zeile am
    /// selben Schluessel und raeumt es wieder weg.
    /// </summary>
    public async Task<bool> PutRowAsync<TRow>(string store, TRow row, bool pending = false)
    {
        if (!await OpenAsync())
        {
            return false;
        }

        try
        {
            // Mit dem LAUFZEITTYP und nicht mit TRow: der Prozessor reicht die
            // bestaetigten Zeilen als object herein, und System.Text.Json
            // serialisiert dann die Eigenschaften von object - also keine.
            // Dieselbe Falle wie in HttpServiceBase.Request.
            var json = JsonSerializer.Serialize(row, row!.GetType(), Json);
            await _js.InvokeVoidAsync("meadowDb.putJson", store, json, pending);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Zeile konnte nicht in {Store} gelegt werden.", store);
            return false;
        }
    }

    public async Task DeleteRowAsync(string store, object key)
    {
        if (!await OpenAsync())
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("meadowDb.delete", store, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Zeile konnte nicht aus {Store} entfernt werden.", store);
        }
    }

    /// <summary>
    /// Nimmt einer Zeile das Wartemerkmal.
    ///
    /// Aenderung und Loeschung antworten mit 204 und bringen keine Zeile
    /// zurueck, mit der sich die wartende ueberschreiben liesse - anders als
    /// der Insert, dessen Antwort genau das tut.
    /// </summary>
    public async Task ClearPendingAsync(string store, object key)
    {
        if (!await OpenAsync())
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("meadowDb.clearPending", store, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wartemerkmal in {Store} konnte nicht entfernt werden.", store);
        }
    }

    // ---- meta ---------------------------------------------------------------

    public async Task<string?> GetMetaAsync(string key)
    {
        if (!await OpenAsync())
        {
            return null;
        }

        try
        {
            return await _js.InvokeAsync<string?>("meadowDb.getMeta", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "meta[{Key}] konnte nicht gelesen werden.", key);
            return null;
        }
    }

    public async Task SetMetaAsync(string key, string value)
    {
        if (!await OpenAsync())
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("meadowDb.setMeta", key, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "meta[{Key}] konnte nicht geschrieben werden.", key);
        }
    }

    // ---- outbox -------------------------------------------------------------

    /// <summary>
    /// Haengt einen Eintrag an. Rueckgabe ist das von IndexedDB vergebene
    /// <c>seq</c>, oder 0, wenn nichts geschrieben wurde - dann hat der
    /// Aufrufer NICHTS in der Hand und darf keinen Erfolg melden.
    /// </summary>
    /// <remarks>
    /// Das DynamicDependency haelt <see cref="OutboxEntry"/> vor dem Trimmer
    /// fest - derselbe Fall wie in TrimmerRoots.xml, nur fuer einen Typ, der in
    /// Meadow.Client liegt und deshalb nicht unter die dortige Regel faellt.
    ///
    /// Der Eintrag geht als JSON nach IndexedDB und kommt von dort zurueck,
    /// beides ueber Reflexion. Entfernt der Trimmer die Eigenschaften, ist das
    /// KEIN Fehler, sondern ein leeres Objekt: der Vorgang liegt dann mit einer
    /// Nutzlast von "{}" im Speicher, wird nach dem naechsten Start als leerer
    /// Stapel abgeschickt und gilt danach als erledigt. Die Behandlung waere
    /// weg, und nichts haette sich beschwert.
    /// </remarks>
    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors,
        typeof(OutboxEntry))]
    public async Task<long> AppendOutboxAsync(OutboxEntry entry)
    {
        if (!await OpenAsync())
        {
            return 0;
        }

        try
        {
            var json = JsonSerializer.Serialize(entry, Json);
            return await _js.InvokeAsync<long>("meadowDb.outboxAdd", json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox-Eintrag konnte nicht abgelegt werden.");
            return 0;
        }
    }

    /// <summary>Alle Eintraege, aufsteigend nach <c>seq</c>.</summary>
    public async Task<List<OutboxEntry>> ReadOutboxAsync()
    {
        if (!await OpenAsync())
        {
            return [];
        }

        try
        {
            var json = await _js.InvokeAsync<string?>("meadowDb.outboxAllJson");
            if (string.IsNullOrEmpty(json))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<OutboxEntry>>(json, Json) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox konnte nicht gelesen werden.");
            return [];
        }
    }

    public async Task UpdateOutboxAsync(OutboxEntry entry)
    {
        if (!await OpenAsync())
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(entry, Json);
            await _js.InvokeVoidAsync("meadowDb.outboxPut", json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox-Eintrag {Seq} konnte nicht fortgeschrieben werden.", entry.Seq);
        }
    }

    public async Task DeleteOutboxAsync(long seq)
    {
        if (!await OpenAsync())
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("meadowDb.outboxDelete", seq);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox-Eintrag {Seq} konnte nicht geloescht werden.", seq);
        }
    }

    // ---- Umgebung -----------------------------------------------------------

    /// <summary>
    /// Was der Browser ueber die Verbindung glaubt. Nur als AUSLOESER benutzen:
    /// navigator.onLine meldet "verbunden", sobald irgendeine Schnittstelle
    /// oben ist - der Hofrouter ohne Internet zaehlt dazu.
    /// </summary>
    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>("meadowDb.isOnline");
        }
        catch (Exception)
        {
            // Ohne JS-Antwort lieber einen Versuch zu viel als einen zu wenig.
            return true;
        }
    }

    public async Task WatchConnectivityAsync<TListener>(DotNetObjectReference<TListener> listener)
        where TListener : class
    {
        try
        {
            await _js.InvokeVoidAsync("meadowDb.watchConnectivity", listener);
        }
        catch (Exception ex)
        {
            // Ohne Ereignisse bleiben App-Start, Sichtbarkeit und der manuelle
            // Knopf als Ausloeser - die Outbox steht also nicht still.
            _logger.LogWarning(ex, "Verbindungsereignisse konnten nicht abonniert werden.");
        }
    }
}
