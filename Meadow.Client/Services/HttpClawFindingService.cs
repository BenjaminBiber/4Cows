using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die Klauenbefunde ueber HTTP. Gegenstueck zum EF-Dienst
/// ClawFindingService.
///
/// Der Fehlerwert des Anlegens heisst ClawFinding.FailedId und sitzt am Modell,
/// nicht hier.
/// </summary>
public class HttpClawFindingService : HttpServiceBase, IClawFindingService
{
    private ImmutableDictionary<int, ClawFinding> _cachedFindings = ImmutableDictionary<int, ClawFinding>.Empty;

    public ImmutableDictionary<int, ClawFinding> Findings => _cachedFindings;

    public List<string> FindingNames => ClawFindingLookups.FindingNames(_cachedFindings.Values);

    public HttpClawFindingService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpClawFindingService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    public async Task GetAllDataAsync()
    {
        var findings = await GetListAsync<ClawFinding>("api/claw-findings", "Failed to load claw findings.");
        if (findings is null)
        {
            return;
        }

        _cachedFindings = findings.ToImmutableDictionary(f => f.ClawFindingId);
        Logger.LogInformation("Loaded {Count} claw findings.", _cachedFindings.Count);
    }

    /// <summary>
    /// Anzeigename, Leerstring bei null und bei unbekannter ID. Rumpf und
    /// Begruendung stehen in ClawFindingLookups.
    /// </summary>
    public string GetNameById(int? id) => ClawFindingLookups.GetNameById(_cachedFindings, id);

    /// <summary>
    /// Wie viele BEHANDLUNGEN jeden Befund benutzen - derselbe Befund an zwei
    /// Klauen derselben Behandlung zaehlt einmal. Die Regel liegt im Dienst
    /// hinter dem Endpunkt; hier kommt nur noch das Ergebnis an.
    ///
    /// <c>null</c> heisst "konnte nicht gezaehlt werden", ein leeres
    /// Woerterbuch heisst "nirgends benutzt". Der Endpunkt antwortet im ersten
    /// Fall mit 503.
    /// </summary>
    public async Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync()
    {
        var counts = await GetJsonAsync<Dictionary<int, int>>(
            "api/claw-findings/usage-counts",
            "Failed to count claw finding usages.");

        return counts?.ToImmutableDictionary();
    }

    /// <summary>
    /// Legt einen Befund an und traegt die erzeugte Id in die UEBERGEBENE
    /// Instanz nach - serverseitig tut EF genau das.
    /// </summary>
    public async Task<bool> InsertDataAsync(ClawFinding finding)
    {
        var created = await CreateAsync(
            "api/claw-findings",
            finding,
            $"Failed to insert claw finding {finding.ClawFindingName}.");

        if (created is null)
        {
            return false;
        }

        finding.ClawFindingId = created.ClawFindingId;

        await GetAllDataAsync();
        Logger.LogInformation("Inserted claw finding {FindingName}.", finding.ClawFindingName);
        return true;
    }

    public async Task<bool> UpdateDataAsync(ClawFinding finding)
    {
        var isSuccess = await WriteAsync(
            () => PutAsync($"api/claw-findings/{finding.ClawFindingId}", finding),
            $"Failed to update claw finding {finding.ClawFindingId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Updated claw finding {FindingName}.", finding.ClawFindingName);
        }

        return isSuccess;
    }

    public async Task<bool> MergeAsync(int sourceId, int targetId, string? survivingName = null)
    {
        // Derselbe Guard wie in der EF-Fassung: ein Merge auf sich selbst
        // wuerde erst umhaengen und dann genau das Ziel loeschen. Erreichbar
        // ueber eine reine Gross-/Kleinschreibungsaenderung ("mortellaro" ->
        // "Mortellaro"), weil der Namensvergleich case-insensitiv ist.
        if (sourceId == targetId)
        {
            return false;
        }

        var isSuccess = await WriteAsync(
            () => PostAsync($"api/claw-findings/{sourceId}/merge", new NamedMergeRequest(targetId, survivingName)),
            $"Failed to merge claw finding {sourceId} into {targetId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Merged claw finding {Source} into {Target}.", sourceId, targetId);
        }

        return isSuccess;
    }

    public async Task<bool> RemoveByIdAsync(int findingId)
    {
        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/claw-findings/{findingId}"),
            $"Failed to delete claw finding {findingId}.");

        if (isSuccess)
        {
            await GetAllDataAsync();
            Logger.LogInformation("Deleted claw finding {FindingId}.", findingId);
        }

        return isSuccess;
    }

    /// <summary>
    /// Sucht den Befund zum Namen und legt ihn an, wenn es ihn nicht gibt.
    ///
    /// DREI Ausgaenge, und sie duerfen nicht verwechselt werden - gespiegelt
    /// aus ClawFindingService.GetIdByNameAsync:
    ///
    /// - <c>null</c> bei leerer Eingabe. Das ist der regulaere Fall "an dieser
    ///   Klaue wurde nichts erfasst" und KEIN Fehler; der Endpunkt antwortet
    ///   dafuer mit 200 und {"id":null}.
    /// - eine Id, wenn gefunden oder angelegt.
    /// - <see cref="ClawFinding.FailedId"/>, wenn es schiefging. Der Aufrufer
    ///   bricht dann ab, BEVOR er die Behandlung schreibt.
    ///
    /// Getrimmt geprueft, damit " " genauso als leere Eingabe gilt wie drueben.
    /// Gekuerzt wird NICHT hier: das erledigt der Dienst hinter dem Endpunkt,
    /// und zwei Stellen mit derselben Laengenregel waeren die naechste, die
    /// jemand vergisst.
    /// </summary>
    public async Task<int?> GetIdByNameAsync(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var response = await ReadAsync<IdResponse>(
            () => PostAsync("api/claw-findings/by-name", new NameRequest(trimmed)),
            $"Failed to resolve claw finding name {trimmed}.");

        if (response is null)
        {
            return ClawFinding.FailedId;
        }

        // Den eigenen Cache nachziehen: der Endpunkt kann den Befund gerade
        // erst angelegt haben, und ohne dieses Nachladen zeigte GetNameById
        // fuer die soeben erzeugte Id einen Leerstring.
        await GetAllDataAsync();
        return response.Id;
    }

    /// <summary>
    /// Vorschlaege fuer das Autocomplete. Rumpf in ClawFindingLookups; das
    /// Task.FromResult bleibt hier, weil sich diese Signatur nicht aendern darf.
    /// </summary>
    public Task<IEnumerable<string>> SearchAsync(string value, CancellationToken token)
        => Task.FromResult(ClawFindingLookups.Search(FindingNames, value));
}
