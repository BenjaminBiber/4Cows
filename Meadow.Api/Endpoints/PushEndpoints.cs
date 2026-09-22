using Meadow.Api.Infrastructure;
using Meadow.Data.Sql;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Api.Endpoints;

/// <summary>Rumpf von POST /push/subscriptions - die drei Browser-Werte.</summary>
public sealed record PushSubscriptionRequest(string? Endpoint, string? P256dh, string? Auth);

/// <summary>Rumpf von DELETE /push/subscriptions - nur der Endpoint.</summary>
public sealed record PushUnsubscribeRequest(string? Endpoint);

public static class PushEndpoints
{
    public static RouteGroupBuilder MapPushEndpoints(this RouteGroupBuilder api)
    {
        // Der oeffentliche VAPID-Schluessel, damit der Client OHNE eingebetteten
        // Schluessel subscriben kann. Fehlt die Konfiguration, ist Push aus -
        // das meldet der Endpunkt ehrlich mit enabled:false, statt eine 404 zu
        // werfen. Der Client wertet enabled aus und blendet den Opt-in dann gar
        // nicht erst ein.
        api.MapGet("/push/public-key", (PushOptions push) => Results.Ok(new
        {
            enabled = push.Enabled,
            publicKey = push.Enabled ? push.PublicKey : null
        })).WithName("PushPublicKey");

        // Upsert einer Anmeldung, idempotent ueber den Endpoint. Kein
        // BumpsOnWrite: Anmeldungen sind kein Client-Cache der dreizehn Dienste,
        // kein Tablet laedt sie je - ein Bump wuerde alle Clients grundlos zu
        // einem Neuladen bewegen.
        api.MapPost("/push/subscriptions", async (
            PushSubscriptionRequest body,
            IDbContextFactory<DatabaseContext> factory,
            HttpContext http) =>
        {
            // Vollstaendigkeit ist die reine Regel in PushLogic - ohne Endpoint
            // kein Ziel, ohne die Schluessel keine verschluesselbare Nutzlast.
            if (!PushLogic.IsValidSubscription(body.Endpoint, body.P256dh, body.Auth))
            {
                return EndpointCommon.Invalid("subscription",
                    "Eine Anmeldung braucht Endpoint, P256dh und Auth.");
            }

            var endpoint = PushLogic.NormalizeEndpoint(body.Endpoint);

            try
            {
                await using var context = await factory.CreateDbContextAsync();

                var existing = await context.PushSubscriptions
                    .FirstOrDefaultAsync(s => s.Endpoint == endpoint);

                if (existing is null)
                {
                    context.PushSubscriptions.Add(new PushSubscription
                    {
                        Endpoint = endpoint,
                        P256dh = body.P256dh!.Trim(),
                        Auth = body.Auth!.Trim(),
                        UserAgent = UserAgentOf(http),
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    // Erneutes Subscriben: der Browser kann neue Schluessel zum
                    // selben Endpoint liefern (Schluesselrotation). Ueberschreiben
                    // statt eine zweite Zeile - der Unique-Index verhindert die
                    // ohnehin.
                    existing.P256dh = body.P256dh!.Trim();
                    existing.Auth = body.Auth!.Trim();
                    existing.UserAgent = UserAgentOf(http);
                }

                await context.SaveChangesAsync();
                return Results.NoContent();
            }
            catch (Exception)
            {
                // Der Dienst-Stil kennt keinen durchgereichten Grund; wie bei den
                // uebrigen Schreibpfaden bleibt er im Protokoll.
                return EndpointCommon.WriteFailed("Die Push-Anmeldung konnte nicht gespeichert werden.");
            }
        }).WithName("PushSubscribe");

        // Abmelden ueber den Endpoint. 204 auch dann, wenn die Zeile schon weg
        // war: das Ziel - "dieser Endpoint ist nicht mehr angemeldet" - ist so
        // oder so erreicht, und der Client soll fuer einen erfolgreichen
        // Abmeldewunsch keine Fehlerkarte sehen.
        api.MapDelete("/push/subscriptions", async (
            [FromBody] PushUnsubscribeRequest body,
            IDbContextFactory<DatabaseContext> factory) =>
        {
            if (string.IsNullOrWhiteSpace(body?.Endpoint))
            {
                return EndpointCommon.Invalid("endpoint", "Ohne Endpoint gibt es nichts abzumelden.");
            }

            var endpoint = PushLogic.NormalizeEndpoint(body.Endpoint);

            try
            {
                await using var context = await factory.CreateDbContextAsync();
                await context.PushSubscriptions
                    .Where(s => s.Endpoint == endpoint)
                    .ExecuteDeleteAsync();
                return Results.NoContent();
            }
            catch (Exception)
            {
                return EndpointCommon.WriteFailed("Die Push-Anmeldung konnte nicht entfernt werden.");
            }
        }).WithName("PushUnsubscribe");

        // KEIN oeffentlicher Test-/Broadcast-Endpunkt mehr. Task 3 hatte hier
        // ein POST /push/test, das an ALLE Anmeldungen sendete - ein offener
        // "an alle pushen"-Ausloeser, der so nicht ausgeliefert werden darf
        // (Ruling aus dem Task-3-Review). Task 6 loest die fachliche Sendung
        // stattdessen SERVERINTERN aus: der BandageReminderPushBackgroundService
        // ruft IPushSender.SendTreatmentReminderToAllAsync direkt. Von aussen
        // ist Push nur noch ueber die Anmelde-Endpunkte oben erreichbar.

        return api;
    }

    /// <summary>
    /// Der User-Agent des anfragenden Browsers, auf die Spaltenlaenge gekuerzt.
    /// Rein informativ - er hilft nur, eine tote Anmeldung im Zweifel einem
    /// Geraet zuzuordnen.
    /// </summary>
    private static string? UserAgentOf(HttpContext http)
    {
        var ua = http.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(ua))
        {
            return null;
        }

        return ua.Length > 256 ? ua[..256] : ua;
    }
}
