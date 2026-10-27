using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Meadow.Shared.Models.Horizon;

namespace Meadow.HorizonAgent.Sending;

/// <summary>Schickt Lebenszeichen und Snapshots an die Meadow-API.</summary>
public sealed class MeadowClient : IMeadowClient
{
    /// <summary>Server erreichbar, Anfrage abgewiesen - erneut senden hilft nicht.</summary>
    private static readonly HttpStatusCode[] Rejected =
    [
        HttpStatusCode.Unauthorized,
        HttpStatusCode.Forbidden,
        HttpStatusCode.NotFound,
        HttpStatusCode.Conflict,
        HttpStatusCode.UnprocessableEntity,
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string Version =
        typeof(MeadowClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MeadowClient).Assembly.GetName().Version?.ToString()
        ?? "unbekannt";

    private readonly HttpClient _http;

    public MeadowClient(HttpClient http) => _http = http;

    public async Task<AgentHeartbeatResponse> SendHeartbeatAsync(AgentHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        heartbeat.AgentVersion = Version;
        heartbeat.MachineName = Environment.MachineName;

        return await PostAsync<AgentHeartbeat, AgentHeartbeatResponse>(
            "api/horizon/heartbeat", heartbeat, cancellationToken);
    }

    public async Task<HorizonSyncResult> SendSnapshotAsync(HorizonSnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.AgentVersion = Version;
        snapshot.MachineName = Environment.MachineName;

        return await PostAsync<HorizonSnapshot, HorizonSyncResult>(
            "api/horizon/snapshot", snapshot, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path, TRequest body, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(path, body, Json, cancellationToken);

        if (Array.IndexOf(Rejected, response.StatusCode) >= 0)
        {
            throw new MeadowRejectedException(
                response.StatusCode,
                await ReadDetailAsync(response, cancellationToken));
        }

        // Alles andere (z. B. 500) gilt als "nicht erreichbar" und wird mit Backoff wiederholt.
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TResponse>(Json, cancellationToken);
        return payload ?? throw new HttpRequestException("Leere Antwort von Meadow.");
    }

    /// <summary>Begruendung aus ProblemDetails; fehlt sie, der rohe Text.</summary>
    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }
        }
        catch (JsonException)
        {
            // Keine ProblemDetails - dann der rohe Text.
        }

        return text.Length <= 500 ? text : text[..500];
    }
}
