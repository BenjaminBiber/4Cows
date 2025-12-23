using System;

namespace BB_Cow.Services;

public class DatabaseStatusService
{
    private bool _isConnected;

    public bool IsConnected => _isConnected;

    public event Action<bool>? ConnectionStatusChanged;

    public void ReportSuccess()
    {
        UpdateStatus(true);
    }

    public void ReportFailure()
    {
        UpdateStatus(false);
    }

    private void UpdateStatus(bool isConnected)
    {
        if (_isConnected == isConnected)
        {
            return;
        }

        _isConnected = isConnected;
        ConnectionStatusChanged?.Invoke(_isConnected);
    }
}
