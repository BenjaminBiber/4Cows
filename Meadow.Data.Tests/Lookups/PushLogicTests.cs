using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Die reinen Entscheidungen des Web-Push-Kanals: ist Push konfiguriert, ist ein
/// Antrag vollstaendig, wie wird ein Endpoint verglichen, legt ein POST neu an
/// oder aktualisiert er, und welche Sende-Antwort bedeutet "Anmeldung tot".
///
/// Genau die Teile, die ohne Netz und ohne Browser fassbar sind - der Rest
/// (echtes Senden, Browser-Anmeldung) ist nur zur Laufzeit pruefbar.
/// </summary>
public class PushLogicTests
{
    // ---- IsConfigured (VAPID-Fallback) ---------------------------------

    [Fact]
    public void All_three_present_means_configured()
    {
        Assert.True(PushLogic.IsConfigured("pub", "priv", "mailto:a@b.de"));
    }

    [Theory]
    [InlineData(null, "priv", "mailto:a@b.de")]
    [InlineData("pub", null, "mailto:a@b.de")]
    [InlineData("pub", "priv", null)]
    [InlineData("", "priv", "mailto:a@b.de")]
    [InlineData("pub", "   ", "mailto:a@b.de")]
    public void A_missing_or_blank_piece_disables_push(string? publicKey, string? privateKey, string? subject)
    {
        // Wie DB_PORT/Demo: fehlt etwas, faellt Push still auf "aus". Der Start
        // darf deswegen NICHT abreissen - hier gepruefte Voraussetzung dafuer.
        Assert.False(PushLogic.IsConfigured(publicKey, privateKey, subject));
    }

    // ---- IsValidSubscription -------------------------------------------

    [Fact]
    public void A_complete_subscription_is_valid()
    {
        Assert.True(PushLogic.IsValidSubscription("https://fcm/x", "p256", "auth"));
    }

    [Theory]
    [InlineData(null, "p256", "auth")]
    [InlineData("https://fcm/x", "", "auth")]
    [InlineData("https://fcm/x", "p256", "  ")]
    public void An_incomplete_subscription_is_rejected(string? endpoint, string? p256dh, string? auth)
    {
        Assert.False(PushLogic.IsValidSubscription(endpoint, p256dh, auth));
    }

    // ---- NormalizeEndpoint ---------------------------------------------

    [Fact]
    public void Normalize_trims_surrounding_whitespace()
    {
        Assert.Equal("https://fcm/x", PushLogic.NormalizeEndpoint("  https://fcm/x \n"));
    }

    [Fact]
    public void Normalize_keeps_case_because_the_path_carries_a_token()
    {
        // Der Endpoint-Pfad ist gross-/klein-empfindlich - NICHTS wird
        // kleingeschrieben, sonst zeigte er auf ein anderes Registrierungstoken.
        Assert.Equal("https://fcm/AbC", PushLogic.NormalizeEndpoint("https://fcm/AbC"));
    }

    [Fact]
    public void Normalize_maps_nothing_to_empty()
    {
        Assert.Equal(string.Empty, PushLogic.NormalizeEndpoint("   "));
        Assert.Equal(string.Empty, PushLogic.NormalizeEndpoint(null));
    }

    // ---- DecideUpsert (Idempotenz) -------------------------------------

    [Fact]
    public void An_unknown_endpoint_inserts()
    {
        var decision = PushLogic.DecideUpsert(Array.Empty<PushSubscription>(), "https://fcm/new");

        Assert.Equal(PushUpsertKind.Insert, decision.Kind);
        Assert.Null(decision.Existing);
    }

    [Fact]
    public void A_known_endpoint_updates_the_same_row_not_a_second_one()
    {
        // Der Kern der Idempotenz: erneutes Subscriben trifft dieselbe Zeile.
        var existing = new PushSubscription { PushSubscriptionId = 7, Endpoint = "https://fcm/x" };

        var decision = PushLogic.DecideUpsert(new[] { existing }, "  https://fcm/x  ");

        Assert.Equal(PushUpsertKind.Update, decision.Kind);
        Assert.Same(existing, decision.Existing);
    }

    // ---- IsGone (tote Anmeldung) ---------------------------------------

    [Theory]
    [InlineData(404, true)]
    [InlineData(410, true)]
    [InlineData(201, false)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    public void Only_404_and_410_mean_the_subscription_is_dead(int statusCode, bool expected)
    {
        // 404/410 = Push-Dienst kennt den Endpoint nicht mehr -> Zeile loeschen.
        // Ein 500 oder 429 ist NICHT das Aus; die Nachricht darf spaeter erneut.
        Assert.Equal(expected, PushLogic.IsGone(statusCode));
    }
}
