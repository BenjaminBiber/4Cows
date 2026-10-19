using System.Collections.Immutable;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die Abbrueche ueber HTTP. Gegenstueck zum EF-Dienst
/// TreatmentPlanCancellationService.
///
/// Gelesen wird auch ohne Netz - der MeadowOfflineHandler beantwortet den GET
/// aus dem lokalen Schnappschuss. GESCHRIEBEN wird nur online: ein Abbruch
/// wandert bewusst NICHT in die Outbox. Er ist keine Erfassung, die man
/// nachreicht, sondern eine Entscheidung ueber einen laufenden Plan, und die
/// kann sich bis zur naechsten Verbindung erledigt haben. Die Oberflaeche
/// meldet stattdessen "Abbrechen ist nur online moeglich."
/// </summary>
public class HttpTreatmentPlanCancellationService : HttpServiceBase, ITreatmentPlanCancellationService
{
    private ImmutableDictionary<Guid, TreatmentPlanCancellation> _cachedCancellations =
        ImmutableDictionary<Guid, TreatmentPlanCancellation>.Empty;

    public ImmutableDictionary<Guid, TreatmentPlanCancellation> Cancellations => _cachedCancellations;

    /// <summary>
    /// Feuert nach jedem erfolgreichen Laden und nach jedem erfolgreichen
    /// Schreiben.
    ///
    /// Die Hinweis-Provider rechnen aus diesem Cache und haengen an keiner
    /// Seite - ohne das Ereignis zeigten sie den Abbruch erst beim naechsten
    /// Seitenwechsel. Deshalb liegt es an der KLASSE und nicht an der
    /// Schnittstelle: die EF-Fassung hat keinen Zuhoerer, fuer den es sich
    /// lohnte.
    /// </summary>
    public event Action? Changed;

    public HttpTreatmentPlanCancellationService(
        HttpClient http,
        DatabaseStatusService databaseStatusService,
        ILogger<HttpTreatmentPlanCancellationService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    public async Task GetAllDataAsync()
    {
        var cancellations = await GetListAsync<TreatmentPlanCancellation>(
            "api/treatment-plan-cancellations",
            "Failed to load treatment plan cancellations.");

        if (cancellations is null)
        {
            return;
        }

        _cachedCancellations = cancellations.ToImmutableDictionary(c => c.StartTreatmentClientId);
        Logger.LogInformation("Loaded {Count} treatment plan cancellations.", _cachedCancellations.Count);
        Changed?.Invoke();
    }

    /// <summary>
    /// Bricht einen Durchlauf ab. <c>false</c> heisst "nicht gespeichert" -
    /// ohne Verbindung, und auch bei einer Ablehnung des Servers. Der Aufrufer
    /// unterscheidet die beiden nicht, weil er in beiden Faellen dasselbe tut:
    /// nichts anzeigen und den Nutzer darauf hinweisen.
    /// </summary>
    public async Task<bool> CancelAsync(TreatmentPlanCancellation cancellation)
    {
        if (cancellation.StartTreatmentClientId == Guid.Empty)
        {
            return false;
        }

        // Ohne Leitung gar nicht erst fragen: der POST liefe in eine
        // HttpRequestException, die GuardAsync ohnehin zu demselben false
        // macht - nur mit einer Fehlerzeile im Protokoll, die nichts erklaert.
        if (!IsConnected)
        {
            Logger.LogInformation(
                "Durchlauf {ClientId} laesst sich ohne Verbindung nicht abbrechen.",
                cancellation.StartTreatmentClientId);
            return false;
        }

        var stored = await CreateAsync(
            "api/treatment-plan-cancellations",
            cancellation,
            $"Failed to cancel treatment plan run {cancellation.StartTreatmentClientId}.");

        if (stored is null)
        {
            return false;
        }

        // Die Antwort und nicht der gesendete Rumpf: bei einer Wiederholung ist
        // das der schon stehende Abbruch mit seinem urspruenglichen Datum.
        _cachedCancellations = _cachedCancellations.SetItem(stored.StartTreatmentClientId, stored);
        Logger.LogInformation("Cancelled treatment plan run {ClientId}.", stored.StartTreatmentClientId);
        Changed?.Invoke();
        return true;
    }

    public async Task<bool> RevokeAsync(Guid startTreatmentClientId)
    {
        if (!IsConnected)
        {
            Logger.LogInformation(
                "Abbruch des Durchlaufs {ClientId} laesst sich ohne Verbindung nicht zuruecknehmen.",
                startTreatmentClientId);
            return false;
        }

        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/treatment-plan-cancellations/{startTreatmentClientId}"),
            $"Failed to revoke treatment plan cancellation {startTreatmentClientId}.");

        if (!isSuccess)
        {
            return false;
        }

        _cachedCancellations = _cachedCancellations.Remove(startTreatmentClientId);
        Logger.LogInformation("Revoked cancellation of treatment plan run {ClientId}.", startTreatmentClientId);
        Changed?.Invoke();
        return true;
    }
}
