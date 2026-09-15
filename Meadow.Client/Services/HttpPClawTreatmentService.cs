using System.Collections.Immutable;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die geplanten Klauenbehandlungen ueber HTTP. Gegenstueck zum EF-Dienst
/// PClawTreatmentService.
/// </summary>
public class HttpPClawTreatmentService : HttpServiceBase, IPClawTreatmentService
{
    private ImmutableDictionary<int, PlannedClawTreatment> _cachedTreatments = ImmutableDictionary<int, PlannedClawTreatment>.Empty;

    public ImmutableDictionary<int, PlannedClawTreatment> Treatments => _cachedTreatments;

    public HttpPClawTreatmentService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpPClawTreatmentService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    public async Task GetAllDataAsync()
    {
        var treatments = await GetListAsync<PlannedClawTreatment>(
            "api/planned-claw-treatments",
            "Failed to load planned claw treatments.");

        if (treatments is null)
        {
            return;
        }

        _cachedTreatments = treatments.ToImmutableDictionary(t => t.PlannedClawTreatmentId);
        Logger.LogInformation("Loaded {Count} planned claw treatments.", _cachedTreatments.Count);
    }

    /// <summary>
    /// Legt eine geplante Klauenbehandlung an und traegt die erzeugte Id in die
    /// UEBERGEBENE Instanz nach.
    ///
    /// Derselbe Fall wie in HttpClawTreatmentService: der Schluessel ist die
    /// Identity, und ohne diesen Rueckweg stuende bei zwei Inserts hintereinander
    /// zweimal die 0 im Cache - der zweite Eintrag ersetzte still den ersten.
    /// </summary>
    public async Task<bool> InsertDataAsync(PlannedClawTreatment clawTreatment)
    {
        var created = await CreateAsync(
            "api/planned-claw-treatments",
            clawTreatment,
            "Failed to insert planned claw treatment.");

        if (created is null)
        {
            return false;
        }

        clawTreatment.PlannedClawTreatmentId = created.PlannedClawTreatmentId;

        // SetItem statt Add, und kein Neuladen: gespiegelt aus der EF-Fassung.
        _cachedTreatments = _cachedTreatments.SetItem(clawTreatment.PlannedClawTreatmentId, clawTreatment);
        Logger.LogInformation("Inserted planned claw treatment {Id}.", clawTreatment.PlannedClawTreatmentId);
        return true;
    }

    public async Task<bool> RemoveByIDAsync(int id)
    {
        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/planned-claw-treatments/{id}"),
            $"Failed to delete planned claw treatment {id}.");

        if (isSuccess && _cachedTreatments.ContainsKey(id))
        {
            _cachedTreatments = _cachedTreatments.Remove(id);
            Logger.LogInformation("Removed planned claw treatment {Id}.", id);
        }

        return isSuccess;
    }

    /// <summary>
    /// Liefert <c>null</c> bei unbekannter Id und steht trotzdem als
    /// PlannedClawTreatment - die Nullability ist woertlich aus der EF-Fassung
    /// uebernommen.
    /// </summary>
    public PlannedClawTreatment GetById(int id)
    {
        return _cachedTreatments.ContainsKey(id) ? _cachedTreatments[id] : null!;
    }
}
