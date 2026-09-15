using System.Collections.Immutable;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Meadow.Client.Services;

/// <summary>
/// Die geplanten Kuhbehandlungen ueber HTTP. Gegenstueck zum EF-Dienst
/// PCowTreatmentService.
/// </summary>
public class HttpPCowTreatmentService : HttpServiceBase, IPCowTreatmentService
{
    private ImmutableDictionary<int, PlannedCowTreatment> _cachedTreatments = ImmutableDictionary<int, PlannedCowTreatment>.Empty;
    private ImmutableList<string> _cachedMedicineList = ImmutableList<string>.Empty;
    private ImmutableList<int> _cachedWhereHowList = ImmutableList<int>.Empty;

    public ImmutableDictionary<int, PlannedCowTreatment> Treatments => _cachedTreatments;

    public ImmutableList<string> CowMedicineTreatmentList => _cachedMedicineList;

    public ImmutableList<int> CowWhereHowList => _cachedWhereHowList;

    public HttpPCowTreatmentService(HttpClient http, DatabaseStatusService databaseStatusService, ILogger<HttpPCowTreatmentService> logger)
        : base(http, databaseStatusService, logger)
    {
    }

    public async Task GetAllDataAsync()
    {
        var treatments = await GetListAsync<PlannedCowTreatment>(
            "api/planned-cow-treatments",
            "Failed to load planned cow treatments.");

        if (treatments is null)
        {
            return;
        }

        _cachedTreatments = treatments.ToImmutableDictionary(t => t.PlannedCowTreatmentId);
        _cachedMedicineList = treatments.Select(t => t.MedicineId.ToString()).Distinct().ToImmutableList();
        _cachedWhereHowList = treatments.Select(t => t.WhereHowId).Distinct().ToImmutableList();
        Logger.LogInformation("Loaded {Count} planned cow treatments.", _cachedTreatments.Count);
    }

    /// <summary>
    /// Mehrere Planungen in EINEM Aufruf. Der Id-Rueckweg laeuft
    /// POSITIONSWEISE: der Endpunkt gibt die Liste in ANFRAGEREIHENFOLGE
    /// zurueck, mit den Ids, die EF in die Instanzen geschrieben hat. Stimmt
    /// die Anzahl nicht, wird gar nichts zurueckgeschrieben und der Aufruf gilt
    /// als gescheitert - eine verschobene Zuordnung traegt die Id der einen
    /// Planung an der anderen und waere schlimmer als gar keine.
    /// </summary>
    public async Task<bool> InsertRangeAsync(IReadOnlyCollection<PlannedCowTreatment> treatments)
    {
        // Gespiegelt aus der EF-Fassung: ein leerer Stapel ist kein Fehlschlag.
        if (treatments.Count == 0)
        {
            return true;
        }

        var ordered = treatments.ToList();

        var created = await ReadAsync<List<PlannedCowTreatment>>(
            () => PostAsync("api/planned-cow-treatments/batch", ordered),
            $"Failed to insert {ordered.Count} planned cow treatments.");

        if (created is null)
        {
            return false;
        }

        if (created.Count != ordered.Count)
        {
            Logger.LogError(
                "Insert of {Sent} planned cow treatments answered with {Received} rows - keys not written back.",
                ordered.Count,
                created.Count);
            return false;
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].PlannedCowTreatmentId = created[i].PlannedCowTreatmentId;
        }

        // Ein Neuladen deckt beide abgeleiteten Listen mit ab - gespiegelt aus
        // der EF-Fassung.
        await GetAllDataAsync();
        Logger.LogInformation("Inserted {Count} planned cow treatments.", ordered.Count);
        return true;
    }

    public async Task<bool> RemoveByIDAsync(int id)
    {
        var isSuccess = await WriteAsync(
            () => DeleteAsync($"api/planned-cow-treatments/{id}"),
            $"Failed to delete planned cow treatment {id}.");

        if (isSuccess && _cachedTreatments.ContainsKey(id))
        {
            _cachedTreatments = _cachedTreatments.Remove(id);
            _cachedMedicineList = _cachedTreatments.Values.Select(t => t.MedicineId.ToString()).Distinct().ToImmutableList();
            _cachedWhereHowList = _cachedTreatments.Values.Select(t => t.WhereHowId).Distinct().ToImmutableList();
            Logger.LogInformation("Removed planned cow treatment {Id}.", id);
        }

        return isSuccess;
    }

    /// <summary>
    /// Liefert <c>null</c> bei unbekannter Id und steht trotzdem als
    /// PlannedCowTreatment - die Nullability ist woertlich aus der EF-Fassung
    /// uebernommen. Ein PlannedCowTreatment? zoege CS8602 durch die
    /// Razor-Rumpfe und braeche die Zusage, dass sich an den Komponenten nur
    /// die Registrierung aendert.
    /// </summary>
    public PlannedCowTreatment GetById(int id)
    {
        return _cachedTreatments.ContainsKey(id) ? _cachedTreatments[id] : null!;
    }
}
