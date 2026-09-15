using System.Net.Http.Json;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Der XLink-Abgleich ueber HTTP. Gegenstueck zum EF-Dienst XLinkService.
///
/// Der Scraper selbst hat hier KEIN Gegenstueck und soll auch keines bekommen:
/// er holt HTML von einer Adresse im Stallnetz, liest es mit zwei regulaeren
/// Ausdruecken und schreibt anschliessend fuer jede abgegangene und jede neue
/// Kuh einzeln. Aus dem Browser waere davon nichts erreichbar - weder der Host
/// noch die Schreibrunden. Dieser Dienst stoesst den Lauf deshalb nur an und
/// liest sein Ergebnis.
/// </summary>
public class HttpXLinkService : HttpServiceBase, IXLinkService
{
    /// <summary>
    /// Abstand zwischen zwei Blicken auf /xlink/status. Kurz genug, dass der
    /// Dialog nicht spuerbar haengt, lang genug, dass ein Lauf ueber mehrere
    /// Minuten nicht hunderte Anfragen kostet.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Obergrenze fuers Warten. Der Lauf wird serverseitig nach zehn Minuten
    /// selbst abgebrochen; eine Minute mehr, damit dieser Dienst den Abbruch
    /// noch als Ergebnis sieht und nicht vorher selbst aufgibt.
    /// </summary>
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(11);

    /// <summary>Zeitpunkt des letzten Abgleichs, null vor dem ersten Lauf.</summary>
    public DateTimeOffset? LastSyncUtc { get; private set; }

    public bool LastSyncSucceeded { get; private set; }

    /// <summary>Fehlermeldung des letzten fehlgeschlagenen Abgleichs.</summary>
    public string? LastSyncError { get; private set; }

    public HttpXLinkService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpXLinkService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    /// <summary>
    /// Stoesst einen Abgleich an und wartet auf sein Ergebnis.
    ///
    /// WIRFT im Fehlerfall - als einzige Methode dieser dreizehn Dienste, und
    /// das ist hier richtig: die EF-Fassung tut dasselbe (vermerken, dann
    /// weiterwerfen), und die eine Aufrufstelle in der Oberflaeche,
    /// BaseDataCow.RefreshFromXLink, hat ihr try/catch bereits und zeigt
    /// e.Message im roten Toast. Ein stilles false machte aus einem
    /// abgeschalteten Demo-Modus eine Meldung ohne Grund.
    ///
    /// Die drei Eigenschaften sind danach IN JEDEM FALL gesetzt - auf dem
    /// Erfolgs- wie auf dem Fehlerweg. DatabaseInfoDialog liest genau sie.
    ///
    /// Warum GEWARTET wird: /xlink/refresh antwortet mit 202, also
    /// "angenommen", und der Lauf faengt danach erst an. Ein einzelner Blick
    /// auf /xlink/status unmittelbar nach dem POST zeigte deshalb die Werte des
    /// VORIGEN Laufs - beim allerersten Mal also lastSyncUtc = null. Der Dialog
    /// behauptete dann "aktiv, nur noch nie gelaufen", waehrend gerade ein Lauf
    /// lief.
    /// </summary>
    public async Task RefreshCowsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await StartAndWaitAsync(cancellationToken);

            // Der Zeitstempel des SERVERS, nicht der eigene: er gehoert zu dem
            // Lauf, der gerade fertig geworden ist.
            LastSyncUtc = status.LastSyncUtc ?? DateTimeOffset.UtcNow;
            LastSyncSucceeded = true;
            LastSyncError = null;

            Logger.LogInformation("XLink sync finished at {LastSyncUtc}.", LastSyncUtc);
        }
        catch (Exception ex)
        {
            // Gespiegelt aus der EF-Fassung: erst vermerken, dann weiterwerfen.
            // Ohne das Vermerken zeigte der Info-Dialog nach einem
            // fehlgeschlagenen Lauf weiter den Zustand von davor.
            LastSyncUtc = DateTimeOffset.UtcNow;
            LastSyncSucceeded = false;
            LastSyncError = ex.Message;

            Logger.LogError(ex, "XLink sync failed.");
            throw;
        }
    }

    /// <summary>
    /// Startet den Lauf und liefert den Zustand, sobald er durch ist. Wirft bei
    /// jedem Fehlschlag - die Aufrufstelle darueber vermerkt ihn und reicht ihn
    /// weiter.
    /// </summary>
    private async Task<XLinkStatusResponse> StartAndWaitAsync(CancellationToken cancellationToken)
    {
        using (var response = await PostAsync("api/xlink/refresh"))
        {
            if (!response.IsSuccessStatusCode)
            {
                // 403 im Demo-Betrieb, 409 wenn schon ein Lauf laeuft. Beides
                // sind benennbare Faelle, und der Grund steht im
                // ProblemDetails-Rumpf - er gehoert in den Toast und nicht nur
                // ins Protokoll.
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Der XLink-Abgleich wurde nicht angenommen (HTTP {(int)response.StatusCode}). {detail}");
            }
        }

        var status = await WaitForRunAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Der XLink-Abgleich wurde angestossen, sein Ergebnis war aber nicht abrufbar.");

        if (!status.LastSyncSucceeded)
        {
            // Die EF-Fassung wirft hier die Ausnahme des Laufs selbst weiter.
            // Ueber HTTP ist von ihr nur die Meldung uebrig - mehr gibt
            // /xlink/status nicht her, und mehr braucht der Toast auch nicht.
            throw new InvalidOperationException(status.LastSyncError ?? "Der XLink-Abgleich ist fehlgeschlagen.");
        }

        return status;
    }

    /// <summary>
    /// Fragt /xlink/status, bis kein Lauf mehr laeuft. <c>null</c>, wenn der
    /// Zustand nicht abrufbar war oder die Wartezeit ueberschritten wurde.
    ///
    /// Der ERSTE Blick zaehlt schon: laeuft dort bereits nichts mehr, war der
    /// Lauf so kurz, dass er zwischen POST und GET durch war - der Normalfall
    /// auf einem kleinen Bestand.
    /// </summary>
    private async Task<XLinkStatusResponse?> WaitForRunAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + MaxWait;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            XLinkStatusResponse? status;
            using (var response = await GetAsync("api/xlink/status"))
            {
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError("XLink status answered HTTP {StatusCode}.", (int)response.StatusCode);
                    return null;
                }

                status = await response.Content.ReadFromJsonAsync<XLinkStatusResponse>(Json, cancellationToken);
            }

            if (status is null)
            {
                return null;
            }

            if (!status.Running)
            {
                return status;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                Logger.LogError("XLink sync still running after {Minutes} minutes - stopped waiting.", MaxWait.TotalMinutes);
                return null;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }
}
