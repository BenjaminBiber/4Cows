using System;
using System.Threading.Tasks;
using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace _4Cows_FE.Components.Services;

public sealed class DatabaseConnectionState : IDisposable
{
    private bool _isConnected;
    private readonly DatabaseStatusService _databaseStatusService;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;

    public bool IsConnected => _isConnected;

    public event Action? ConnectionStateChanged;

    public DatabaseConnectionState(DatabaseStatusService databaseStatusService, IDbContextFactory<DatabaseContext> contextFactory)
    {
        _databaseStatusService = databaseStatusService;
        _contextFactory = contextFactory;
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
            await using var context = await _contextFactory.CreateDbContextAsync();
            var canConnect = await context.Database.CanConnectAsync();
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
            LoggerService.LogError(typeof(DatabaseConnectionState), "Database connection check failed, with {@Message}", ex, ex.Message);
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
}
