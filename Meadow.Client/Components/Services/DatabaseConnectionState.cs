using System.Net.Http.Json;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Components.Services;

/// <summary>
/// Oeffentliche Oberflaeche unveraendert: IsConnected, ConnectionStateChanged,
/// EnsureLatestStatusAsync. Index.razor und DatabaseInfoDialog merken nichts.
///
/// Was sich aendert, ist nur die Frage dahinter. Vorher:
/// context.Database.CanConnectAsync() - "erreiche ich MariaDB". Jetzt
/// GET /api/health - "erreicht die API MariaDB", und implizit "erreiche ich
/// die API". Das sind zwei Ausfaelle statt einem, und der Unterschied ist
/// fuer den Nutzer im Stall gerade der wichtige: eine unerreichbare API sah
/// vorher aus wie gar nichts.
/// </summary>
public sealed class DatabaseConnectionState : IDisposable
{
    private bool _isConnected;
    private readonly DatabaseStatusService _databaseStatusService;
    private readonly HttpClient _http;
    private readonly ILogger<DatabaseConnectionState> _logger;

    public bool IsConnected => _isConnected;

    public event Action? ConnectionStateChanged;

    public DatabaseConnectionState(
        DatabaseStatusService databaseStatusService,
        HttpClient http,
        ILogger<DatabaseConnectionState> logger)
    {
        _databaseStatusService = databaseStatusService;
        _http = http;
        _logger = logger;
        _isConnected = databaseStatusService.IsConnected;
        _databaseStatusService.ConnectionStatusChanged += HandleStatusChanged;
    }

    public async Task<bool> EnsureLatestStatusAsync()
    {
        var canConnect = await CheckConnectionAsync();
        UpdateState(canConnect);
        return _isConnected;
    }

    private async Task<bool> CheckConnectionAsync()
    {
        try
        {
            // /api/health antwortet absichtlich IMMER mit 200 und traegt den
            // Zustand im Rumpf. Eine 503 haette hier nicht gereicht: sie liesse
            // "API tot" und "Datenbank tot" gleich aussehen.
            var health = await _http.GetFromJsonAsync<HealthResponse>("api/health");
            var canConnect = health?.Database ?? false;

            if (canConnect)
            {
                _databaseStatusService.ReportSuccess();
            }
            else
            {
                _databaseStatusService.ReportFailure();
            }

            return canConnect;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            _logger.LogError(ex, "Zustandsabfrage der API fehlgeschlagen: {Message}", ex.Message);
            return false;
        }
    }

    private void HandleStatusChanged(bool isConnected)
    {
        UpdateState(isConnected);
    }

    private void UpdateState(bool isConnected)
    {
        if (_isConnected == isConnected)
        {
            return;
        }

        _isConnected = isConnected;
        ConnectionStateChanged?.Invoke();
    }

    public void Dispose()
    {
        _databaseStatusService.ConnectionStatusChanged -= HandleStatusChanged;
    }

    private sealed record HealthResponse(string Status, bool Database, string Version);
}
