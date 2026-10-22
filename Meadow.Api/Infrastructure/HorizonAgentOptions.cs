namespace Meadow.Api.Infrastructure;

/// <summary>
/// Der Schluessel, mit dem sich der Horizon-Agent ausweist. Wie DemoOptions und
/// CowSyncOptions beim Start einmal aus der Konfiguration gelesen und danach
/// unveraenderlich.
///
/// Ist nichts konfiguriert, gibt es die Endpunkte fuer den Agenten nicht -
/// eine Instanz ohne Agenten soll keine offene Tuer haben, die nur zufaellig
/// niemand kennt.
/// </summary>
public sealed record HorizonAgentOptions(string? ApiKey)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
