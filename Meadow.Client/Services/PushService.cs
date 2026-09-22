using System.Net.Http.Json;
using System.Text.Json;
using Meadow.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Meadow.Client.Services;

/// <summary>Der Push-Zustand, wie ihn /api/push/public-key liefert.</summary>
public sealed record PushPublicKeyResponse(bool Enabled, string? PublicKey);

/// <summary>Die flache Anmelde-Form aus push.js: endpoint + die zwei Schluessel.</summary>
public sealed record PushSubscriptionDto(string Endpoint, string P256dh, string Auth);

/// <summary>
/// Die C#-Huelle um push.js. Kapselt Berechtigungsabfrage, An- und Abmelden und
/// postet die Anmeldung an den Server.
///
/// Singleton wie die uebrigen Zustandsdienste - er haelt keinen eigenen Cache,
/// aber es gibt je Tab genau eine Push-Anmeldung, und ein Singleton darf keine
/// Scoped-Abhaengigkeit haben (HttpClient und IJSRuntime sind beide Singleton
/// registriert bzw. prozessweit).
///
/// JEDE Methode ist fehlertolerant, gleiche Haltung wie MeadowLocalStore: Push
/// ist ein Angebot des Browsers, kein verlaesslicher Dienst. Faellt etwas aus,
/// laeuft die App unveraendert weiter - Push ist Beiwerk, kein Kernpfad.
/// </summary>
public sealed class PushService
{
    private static readonly JsonSerializerOptions Json = MeadowJson.CreateOptions();

    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private readonly ILogger<PushService> _logger;

    public PushService(IJSRuntime js, HttpClient http, ILogger<PushService> logger)
    {
        _js = js;
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Ob dieser Browser Push ueberhaupt beherrscht. Eine Oberflaeche blendet
    /// den Opt-in gar nicht erst ein, wenn das false ist.
    /// </summary>
    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>("meadowPush.isSupported");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push-Unterstuetzung konnte nicht ermittelt werden.");
            return false;
        }
    }

    /// <summary>Der aktuelle Berechtigungsstand ohne nachzufragen.</summary>
    public async Task<string> PermissionAsync()
    {
        try
        {
            return await _js.InvokeAsync<string>("meadowPush.permission");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push-Berechtigungsstand konnte nicht gelesen werden.");
            return "unsupported";
        }
    }

    /// <summary>
    /// Der ganze Weg in einem Aufruf, gedacht fuer einen Opt-in-Klick:
    /// Berechtigung anfragen, beim Push-Dienst anmelden, Anmeldung an den Server
    /// posten. <c>true</c> nur, wenn am Ende eine Anmeldung serverseitig liegt.
    ///
    /// MUSS aus einer Nutzergeste laufen - die Berechtigungsabfrage verlangt sie.
    /// </summary>
    public async Task<bool> EnableAsync()
    {
        try
        {
            var permission = await _js.InvokeAsync<string>("meadowPush.requestPermission");
            if (permission != "granted")
            {
                _logger.LogInformation("Push-Berechtigung nicht erteilt: {Permission}.", permission);
                return false;
            }

            // Den oeffentlichen Schluessel erst NACH erteilter Berechtigung
            // holen: ist Push serverseitig aus, gibt es nichts anzumelden.
            var key = await GetPublicKeyAsync();
            if (key is null || !key.Enabled || string.IsNullOrWhiteSpace(key.PublicKey))
            {
                _logger.LogInformation("Push ist serverseitig nicht konfiguriert - keine Anmeldung.");
                return false;
            }

            var subscription = await _js.InvokeAsync<PushSubscriptionDto?>("meadowPush.subscribe", key.PublicKey);
            if (subscription is null || string.IsNullOrWhiteSpace(subscription.Endpoint))
            {
                _logger.LogWarning("Push-Anmeldung im Browser fehlgeschlagen.");
                return false;
            }

            return await PostSubscriptionAsync(subscription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push-Aktivierung fehlgeschlagen.");
            return false;
        }
    }

    /// <summary>
    /// Meldet den Browser ab und entfernt die Anmeldung serverseitig. Idempotent:
    /// gab es nichts abzumelden, gilt das ebenfalls als Erfolg.
    /// </summary>
    public async Task<bool> DisableAsync()
    {
        try
        {
            var endpoint = await _js.InvokeAsync<string?>("meadowPush.unsubscribe");
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return true;
            }

            using var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "api/push/subscriptions")
            {
                Content = JsonContent.Create(new { endpoint }, options: Json)
            });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push-Abmeldung fehlgeschlagen.");
            return false;
        }
    }

    /// <summary>Der Push-Zustand des Servers (enabled + oeffentlicher Schluessel).</summary>
    public async Task<PushPublicKeyResponse?> GetPublicKeyAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<PushPublicKeyResponse>("api/push/public-key", Json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Oeffentlicher Push-Schluessel konnte nicht geladen werden.");
            return null;
        }
    }

    private async Task<bool> PostSubscriptionAsync(PushSubscriptionDto subscription)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("api/push/subscriptions", subscription, Json);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Push-Anmeldung wurde vom Server abgelehnt: HTTP {Status}.", (int)response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push-Anmeldung konnte nicht an den Server gesendet werden.");
            return false;
        }
    }
}
