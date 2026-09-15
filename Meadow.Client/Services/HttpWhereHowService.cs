using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Wie/Wo ueber HTTP. Gegenstueck zum EF-Dienst WhereHowService.
/// </summary>
public class HttpWhereHowService : HttpServiceBase, IWhereHowService
{
    private ImmutableDictionary<int, WhereHow> _cachedWhereHows = ImmutableDictionary<int, WhereHow>.Empty;

    public ImmutableDictionary<int, WhereHow> WhereHows => _cachedWhereHows;

    // Weiterleitung wie in der EF-Fassung: der Rumpf steht in WhereHowLookups.
    public List<string> WhereHowNames => WhereHowLookups.WhereHowNames(_cachedWhereHows.Values);

    public HttpWhereHowService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpWhereHowService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    public async Task GetAllDataAsync()
    {
        var whereHows = await GetListAsync<WhereHow>("api/where-hows", "Failed to load WhereHows.");
        if (whereHows is null)
        {
            return;
        }

        _cachedWhereHows = whereHows.ToImmutableDictionary(w => w.WhereHowId);
        Logger.LogInformation("Loaded {Count} WhereHows.", _cachedWhereHows.Count);
    }

    /// <summary>
    /// <c>null</c> heisst "konnte nicht gezaehlt werden", ein leeres
    /// Woerterbuch heisst "nirgends benutzt". Die Verwechslung gaebe den
    /// Papierkorb fuer jede Zeile frei; der Endpunkt antwortet in dem Fall mit
    /// 503. Gespiegelt aus WhereHowService.GetUsageCountsAsync.
    /// </summary>
    public async Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync()
    {
        var counts = await GetJsonAsync<Dictionary<int, int>>(
            "api/where-hows/usage-counts",
            "Failed to count WhereHow usages.");

        return counts?.ToImmutableDictionary();
    }

    /// <summary>
    /// Legt einen Wie/Wo-Eintrag an und traegt die erzeugte Id in die
    /// UEBERGEBENE Instanz nach - serverseitig tut EF genau das.
    /// </summary>
    public async Task<bool> InsertDataAsync(WhereHow whereHow)
    {
        var created = await CreateAsync(
            "api/where-hows",
            whereHow,
            $"Failed to insert WhereHow {whereHow.WhereHowName}.");

        if (created is null)
        {
            return false;
        }

        whereHow.WhereHowId = created.WhereHowId;

        // Neu laden - gespiegelt aus der EF-Fassung.
        await GetAllDataAsync();
        Logger.LogInformation("Inserted WhereHow {WhereHowName}.", whereHow.WhereHowName);
        return true;
    }

    public async Task<bool> UpdateDataAsync(WhereHow whereHow)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/where-hows/{whereHow.WhereHowId}", whereHow),
            $"Failed to update WhereHow {whereHow.WhereHowId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Updated WhereHow {WhereHowName}.", whereHow.WhereHowName);
        }

        return isSuccess;
    }

    public async Task<bool> MergeAsync(int sourceId, int targetId)
    {
        // Derselbe Guard wie in der EF-Fassung: ein Merge auf sich selbst
        // wuerde erst umhaengen und dann genau das Ziel loeschen.
        if (sourceId == targetId)
        {
            return false;
        }

        var isSuccess = await WriteAsync(
            () => PostAsync($"api/where-hows/{sourceId}/merge", new MergeRequest(targetId)),
            $"Failed to merge WhereHow {sourceId} into {targetId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Merged WhereHow {Source} into {Target}.", sourceId, targetId);
        }

        return isSuccess;
    }

    public async Task<bool> RemoveByIdAsync(int whereHowId)
    {
        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/where-hows/{whereHowId}"),
            $"Failed to delete WhereHow {whereHowId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Deleted WhereHow {WhereHowId}.", whereHowId);
        }

        return isSuccess;
    }

    // Weiterleitungen; die Rumpfe stehen in WhereHowLookups, damit der
    // Leerstring bei Fehltreffer - auf den GetWhereHowNamesByIds filtert - nur
    // an einer Stelle festgelegt ist.
    public WhereHow GetById(int whereHowID) => WhereHowLookups.GetById(_cachedWhereHows, whereHowID);

    public string GetWhereHowNameById(int id) => WhereHowLookups.GetWhereHowNameById(_cachedWhereHows, id);

    public List<string> GetWhereHowNamesByIds(List<int> Ids) => WhereHowLookups.GetWhereHowNamesByIds(_cachedWhereHows, Ids);

    /// <summary>
    /// Sucht den Eintrag zum Namen und legt ihn an, wenn es ihn nicht gibt.
    ///
    /// <paramref name="showDialog"/> geht mit ueber die Leitung und wird NICHT
    /// weggelassen: der implizite Anlagepfad nahm frueher immer den
    /// Konstruktor-Default true und legte damit einen Eintrag an, dessen
    /// Viertel-Verhalten dem widersprach, was im Formular zu sehen war.
    ///
    /// Der leere Name wird hier abgefangen. Die EF-Fassung ruft an dieser
    /// Stelle name.ToLower() ohne Pruefung auf und liefe bei null in eine
    /// NullReferenceException - die darf ueber HTTP nicht nach oben schlagen,
    /// weil sie sonst die Seite abreisst statt einen roten Toast zu zeigen.
    /// int.MinValue ist derselbe Fehlwert, den die EF-Fassung fuer "weder
    /// gefunden noch angelegt" liefert.
    /// </summary>
    public async Task<int> GetWhereHowIDByName(string name, bool showDialog = true)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return int.MinValue;
        }

        // Erst im eigenen Cache nachsehen, dann erst fragen.
        //
        // Online spart das eine Netzrunde. OFFLINE ist es der Unterschied
        // zwischen "Behandlung laesst sich erfassen" und "gar nicht": ein
        // bekannter Eintrag muss sich ohne Netz aufloesen lassen, sonst bricht
        // das Speichern ab, bevor die Outbox ueberhaupt gefragt wird. Uebrig
        // bleibt der unbekannte Name - und der gehoert offline abgelehnt, weil
        // an der Behandlung sonst ein Eintrag haengt, den es serverseitig nicht
        // gibt.
        //
        // Der Vergleich ist woertlich der aus der EF-Fassung: getrimmt und
        // kleingeschrieben.
        var known = _cachedWhereHows.Values.FirstOrDefault(
            x => x.WhereHowName != null
                 && x.WhereHowName.Trim().ToLower() == trimmed.ToLower());
        if (known is not null)
        {
            return known.WhereHowId;
        }

        var response = await ReadAsync<IdResponse>(
            () => PostAsync("api/where-hows/by-name", new WhereHowByNameRequest(trimmed, showDialog)),
            $"Failed to resolve WhereHow name {trimmed}.");

        if (response?.Id is not int id)
        {
            return int.MinValue;
        }

        // Den eigenen Cache nachziehen: der Endpunkt kann den Eintrag gerade
        // erst angelegt haben, und ohne dieses Nachladen zeigte
        // GetWhereHowNameById fuer die soeben erzeugte Id einen Leerstring.
        await GetAllDataAsync();
        return id;
    }

    // Braucht zwei Caches. Der zweite kommt weiter ueber den Dienst herein -
    // der Parameter ist auf die Naht verbreitert, nicht entfernt, sonst aendert
    // sich jede Aufrufstelle.
    public string GetFullWhereHowName(int whereHow_id, IUdderService udderService, int? udder_id = null)
        => WhereHowLookups.GetFullWhereHowName(_cachedWhereHows, udderService.Udder, whereHow_id, udder_id);

    public string GetUdderString(Udder udder) => WhereHowLookups.GetUdderString(udder);
}
