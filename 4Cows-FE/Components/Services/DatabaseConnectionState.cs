using System;
using System.Threading.Tasks;
using BBCowDataLibrary.SQL;

namespace _4Cows_FE.Components.Services;

public sealed class DatabaseConnectionState : IDisposable
{
    private bool _isConnected = DatabaseService.HasActiveConnection;

    public bool IsConnected => _isConnected;

    public event Action? ConnectionStateChanged;

    public DatabaseConnectionState()
    {
        DatabaseService.ConnectionStatusChanged += HandleStatusChanged;
    }

    public Task<bool> EnsureLatestStatusAsync()
    {
        return DatabaseService.IsConfigured();
    }

    private void HandleStatusChanged(bool isConnected)
    {
        _isConnected = isConnected;
        ConnectionStateChanged?.Invoke();
    }

    public void Dispose()
    {
        DatabaseService.ConnectionStatusChanged -= HandleStatusChanged;
    }
}
