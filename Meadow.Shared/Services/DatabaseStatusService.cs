using System;

namespace Meadow.Shared.Services;

/// <summary>
/// Merkt sich, ob der letzte Datenaufruf geklappt hat, und meldet jeden
/// Wechsel per Ereignis.
///
/// Diese Klasse liegt als KONKRETER Typ im geteilten Projekt und bekommt
/// bewusst KEIN Interface, anders als die dreizehn Dienste daneben:
///
/// - Es gibt genau eine richtige Implementierung. Der Zustand ist ein bool im
///   Hauptspeicher, die Klasse hat null Abhaengigkeiten - nichts daran haengt
///   an EF oder an HTTP.
/// - Die Semantik ("der letzte Datenaufruf hat geklappt") ist auf beiden
///   Seiten der Naht dieselbe. Ein HTTP-Dienst meldet denselben Erfolg oder
///   Misserfolg, nur eben den einer Anfrage statt den einer Abfrage.
/// - Eine zweite Implementierung waere deshalb Zeile fuer Zeile eine Kopie
///   dieser hier - und damit genau die Drift, die die Naht verhindern soll.
/// </summary>
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
