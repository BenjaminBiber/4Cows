using Meadow.Shared.Lookups;

namespace Meadow.Api.Infrastructure;

/// <summary>
/// Die VAPID-Konfiguration, so wie sie aus <c>Vapid:PublicKey</c>,
/// <c>Vapid:PrivateKey</c> und <c>Vapid:Subject</c> gelesen wird.
///
/// Eigener, aus der Konfiguration abgeleiteter Typ - genau wie
/// <see cref="Meadow.Api.BackgroundServices.DemoOptions"/> aus Demo:* - damit
/// jeder Nutzer den GELTENDEN Zustand (inkl. Rueckfall auf "aus") aus der DI
/// bekommt und nicht selbst dieselbe Fallback-Regel noch einmal schreibt.
///
/// <see cref="Enabled"/> stammt aus <see cref="PushLogic.IsConfigured"/>: fehlt
/// eines der drei Stuecke, ist Push aus - der Start reisst deswegen NICHT ab.
/// </summary>
public sealed record PushOptions(
    bool Enabled,
    string? PublicKey,
    string? PrivateKey,
    string? Subject)
{
    /// <summary>
    /// Liest die drei Werte aus der Konfiguration und entscheidet ueber die
    /// reine Regel in <see cref="PushLogic.IsConfigured"/>, ob Push laeuft.
    /// Wirft nie - fehlende Werte fuehren zu <c>Enabled == false</c>.
    /// </summary>
    public static PushOptions FromConfiguration(IConfiguration configuration)
    {
        var publicKey = configuration["Vapid:PublicKey"];
        var privateKey = configuration["Vapid:PrivateKey"];
        var subject = configuration["Vapid:Subject"];

        return new PushOptions(
            Enabled: PushLogic.IsConfigured(publicKey, privateKey, subject),
            PublicKey: publicKey,
            PrivateKey: privateKey,
            Subject: subject);
    }
}
