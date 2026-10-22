using Meadow.Shared.Services;

namespace Meadow.Api.Infrastructure;

/// <summary>
/// Welche Quelle den Kuh-Abgleich liefert. Wie DemoOptions beim Start einmal
/// aus der Konfiguration gelesen und danach unveraenderlich: Endpunkte sollen
/// erfahren, was GILT, und nicht den Rohwert noch einmal selbst auslegen.
/// </summary>
public sealed record CowSyncOptions(CowSyncSource Source);
