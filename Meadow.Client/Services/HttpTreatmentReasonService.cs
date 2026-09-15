using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die Behandlungsgruende ueber HTTP. Gegenstueck zum EF-Dienst
/// TreatmentReasonService.
/// </summary>
public class HttpTreatmentReasonService : HttpServiceBase, ITreatmentReasonService
{
    /// <summary>
    /// Anzeigetext, wenn kein Grund gesetzt oder die ID unbekannt ist.
    ///
    /// Die EINZIGE Regel dieser dreizehn Dienste, die hier wirklich doppelt
    /// steht - als Gegenstueck zu TreatmentReasonService.NoReasonText. Sie
    /// konnte nicht nach TreatmentReasonLookups wandern, weil sie dort als
    /// PARAMETER hereinkommt (GetNameById(reasons, id, noReasonText)), und sie
    /// steht nicht auf der Naht, weil Konstanten keine
    /// Schnittstellen-Member sind. Wer den Text aendert, muss beide Stellen
    /// anfassen.
    ///
    /// Als – und nicht als Zeichen geschrieben, damit der Wert unabhaengig
    /// von der Kodierung dieser Datei derselbe Gedankenstrich ist wie drueben.
    /// </summary>
    public const string NoReasonText = "–";

    private ImmutableDictionary<int, TreatmentReason> _cachedReasons = ImmutableDictionary<int, TreatmentReason>.Empty;

    public ImmutableDictionary<int, TreatmentReason> Reasons => _cachedReasons;

    public List<string> ReasonNames => TreatmentReasonLookups.ReasonNames(_cachedReasons.Values);

    public HttpTreatmentReasonService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpTreatmentReasonService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    public async Task GetAllDataAsync()
    {
        var reasons = await GetListAsync<TreatmentReason>("api/treatment-reasons", "Failed to load treatment reasons.");
        if (reasons is null)
        {
            return;
        }

        _cachedReasons = reasons.ToImmutableDictionary(r => r.TreatmentReasonId);
        Logger.LogInformation("Loaded {Count} treatment reasons.", _cachedReasons.Count);
    }

    /// <summary>
    /// <c>null</c> heisst "konnte nicht gezaehlt werden", ein leeres
    /// Woerterbuch heisst "nirgends benutzt". Der Endpunkt antwortet im ersten
    /// Fall mit 503. Gespiegelt aus TreatmentReasonService.GetUsageCountsAsync.
    /// </summary>
    public async Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync()
    {
        var counts = await GetJsonAsync<Dictionary<int, int>>(
            "api/treatment-reasons/usage-counts",
            "Failed to count treatment reason usages.");

        return counts?.ToImmutableDictionary();
    }

    /// <summary>
    /// Legt einen Grund an und traegt die erzeugte Id in die UEBERGEBENE
    /// Instanz nach - serverseitig tut EF genau das.
    /// </summary>
    public async Task<bool> InsertDataAsync(TreatmentReason reason)
    {
        var created = await CreateAsync(
            "api/treatment-reasons",
            reason,
            $"Failed to insert treatment reason {reason.TreatmentReasonName}.");

        if (created is null)
        {
            return false;
        }

        reason.TreatmentReasonId = created.TreatmentReasonId;

        await GetAllDataAsync();
        Logger.LogInformation("Inserted treatment reason {ReasonName}.", reason.TreatmentReasonName);
        return true;
    }

    public async Task<bool> UpdateDataAsync(TreatmentReason reason)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/treatment-reasons/{reason.TreatmentReasonId}", reason),
            $"Failed to update treatment reason {reason.TreatmentReasonId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Updated treatment reason {ReasonName}.", reason.TreatmentReasonName);
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
            () => PostAsync($"api/treatment-reasons/{sourceId}/merge", new MergeRequest(targetId)),
            $"Failed to merge treatment reason {sourceId} into {targetId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Merged treatment reason {Source} into {Target}.", sourceId, targetId);
        }

        return isSuccess;
    }

    public async Task<bool> RemoveByIdAsync(int reasonId)
    {
        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/treatment-reasons/{reasonId}"),
            $"Failed to delete treatment reason {reasonId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Deleted treatment reason {ReasonId}.", reasonId);
        }

        return isSuccess;
    }

    /// <summary>
    /// Anzeigename. NoReasonText bei null und bei unbekannter ID. Rumpf in
    /// TreatmentReasonLookups, die Konstante geht als Parameter hinein - genau
    /// wie in der EF-Fassung.
    /// </summary>
    public string GetNameById(int? id)
        => TreatmentReasonLookups.GetNameById(_cachedReasons, id, NoReasonText);

    /// <summary>
    /// Sucht den Grund zum Namen und legt ihn an, wenn es ihn nicht gibt.
    ///
    /// int.MinValue, wenn nichts uebrig bleibt - der Aufrufer bricht dann ab,
    /// BEVOR er die erste Behandlung schreibt. Derselbe Fehlwert wie in der
    /// EF-Fassung, und dort wie hier auch der Wert fuer eine leere Eingabe: die
    /// beiden Faelle sind im Rueckgabewert nicht zu unterscheiden.
    /// </summary>
    public async Task<int> GetIdByNameAsync(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return int.MinValue;
        }

        var response = await ReadAsync<IdResponse>(
            () => PostAsync("api/treatment-reasons/by-name", new NameRequest(trimmed)),
            $"Failed to resolve treatment reason name {trimmed}.");

        if (response?.Id is not int id)
        {
            return int.MinValue;
        }

        // Den eigenen Cache nachziehen: der Endpunkt kann den Grund gerade erst
        // angelegt haben, und ohne dieses Nachladen zeigte GetNameById fuer die
        // soeben erzeugte Id den Gedankenstrich.
        await GetAllDataAsync();
        return id;
    }

    /// <summary>
    /// Vorschlaege fuer das Autocomplete. Rumpf in TreatmentReasonLookups; das
    /// Task.FromResult bleibt hier, weil sich diese Signatur nicht aendern darf.
    /// </summary>
    public Task<IEnumerable<string>> SearchAsync(string value, CancellationToken token)
        => Task.FromResult(TreatmentReasonLookups.Search(ReasonNames, value));
}
