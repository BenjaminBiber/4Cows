using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;

// Diese Klasse hatte als einzige im Projekt gar keinen Namensraum. Sie bekommt hier
// denselben wie ihre dreizehn Geschwister; ein Datei-Namensraum, damit der Rumpf nicht
// eingerueckt werden muss und der Commit nachweisbar nur Namensraumzeilen aendert.
namespace Meadow.Data.Services;

public class CowTreatmentService : ICowTreatmentService
{
    private ImmutableDictionary<int, CowTreatment> _cachedTreatments = ImmutableDictionary<int, CowTreatment>.Empty;
    private ImmutableList<int> _cachedDistinctWhereHows = ImmutableList<int>.Empty;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;

    public ImmutableDictionary<int, CowTreatment> Treatments => _cachedTreatments;
    public ImmutableList<int> DistinctWhereHows => _cachedDistinctWhereHows;
    // Auf die Naht verbreitert, nicht entfernt: dieser Dienst braucht die
    // Wie/Wo-Namen weiterhin, nur nicht mehr die EF-Implementierung davon.
    private readonly IWhereHowService _whereHowService;
    public CowTreatmentService(IWhereHowService whereHowService, IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
    {
        _whereHowService = whereHowService;
        _contextFactory = contextFactory;
        _databaseStatusService = databaseStatusService;
    }
    
    public async Task GetAllDataAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var dbResult = await context.CowTreatments.AsNoTracking().ToListAsync();
            _cachedDistinctWhereHows = dbResult.Select(t => t.WhereHowId).Distinct().ToImmutableList();
            _cachedTreatments = dbResult.ToImmutableDictionary(t => t.CowTreatmentId);
            _databaseStatusService.ReportSuccess();
            LoggerService.LogInformation(typeof(CowTreatmentService), $"Loaded {_cachedTreatments.Count} cow treatments.");
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(CowTreatmentService), "Failed to load cow treatments, with {@Message}", ex, ex.Message);
        }
    }

    /// <summary>
    /// Mehrere Behandlungen in EINEM SaveChanges und mit EINEM Cache-Neuladen.
    ///
    /// Vorher lud der Insert nach JEDER Zeile die ganze Tabelle neu. Bei
    /// mehreren Tieren mal mehreren Medikamenten waren das N mal M
    /// Volldurchlaeufe fuer einen Klick.
    ///
    /// Alles oder nichts: SaveChangesAsync auf einem Context ist eine
    /// Transaktion, ein Fehler laesst also keine Zeile stehen. Damit gibt es
    /// die halb gespeicherte Serie nicht mehr, um die sich die
    /// Teilerfolgs-Meldung im Dialog kuemmern musste. Ein BeginTransactionAsync
    /// braucht es dafuer nicht - anders als in TreatmentReasonService.MergeAsync,
    /// wo mehrere ExecuteUpdateAsync am Change Tracker vorbei laufen.
    ///
    /// Jede Behandlung muss eine EIGENE Instanz sein. Eine wiederverwendete
    /// waere beim zweiten Add dieselbe - nun getrackte - Zeile, und AddRange
    /// schriebe sie still nur einmal.
    /// </summary>
    public async Task<bool> InsertRangeAsync(IReadOnlyCollection<CowTreatment> treatments)
    {
        if (treatments.Count == 0)
        {
            return true;
        }

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.CowTreatments.AddRangeAsync(treatments);

            // Nicht > 0: eine Teilzahl waere ein Widerspruch zur Transaktion
            // und soll als Fehlschlag gemeldet werden, nicht als Erfolg.
            var isSuccess = await context.SaveChangesAsync() == treatments.Count;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(CowTreatmentService), $"Inserted {treatments.Count} cow treatments.");
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(CowTreatmentService), "Failed to insert cow treatments, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    public async Task<CowTreatment> GetByIdAsync(int id)
    {
        if (_cachedTreatments.ContainsKey(id))
        {
            return _cachedTreatments[id];
        }

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var treatmentResult = await context.CowTreatments.AsNoTracking().FirstOrDefaultAsync(t => t.CowTreatmentId == id);
            _databaseStatusService.ReportSuccess();

            if (treatmentResult != null)
            {
                // SetItem statt Add: zwei gleichzeitige Aufrufe mit derselben Id kommen
                // beide am ContainsKey oben vorbei, und der zweite Add wirft dann.
                _cachedTreatments = _cachedTreatments.SetItem(id, treatmentResult);
            }

            return treatmentResult ?? new CowTreatment();
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(CowTreatmentService), "Failed to fetch cow treatment, with {@Message}", ex, ex.Message);
            return new CowTreatment();
        }
    }

    public async Task DeleteDataAsync(int id)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.CowTreatments.Where(t => t.CowTreatmentId == id).ExecuteDeleteAsync();
            _databaseStatusService.ReportSuccess();

            if (_cachedTreatments.ContainsKey(id))
            {
                _cachedTreatments = _cachedTreatments.Remove(id);
                LoggerService.LogInformation(typeof(CowTreatmentService), "Deleted cow treatment with ID: {id}.", id);
            }
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(CowTreatmentService), "Failed to delete cow treatment, with {@Message}", ex, ex.Message);
        }
    }
    
    // Ab hier nur noch Weiterleitungen; die Rumpfe stehen in
    // CowTreatmentLookups. DateTime.Now wird dort zum Parameter, damit die
    // Rechnung nicht an der Uhr des Servers haengt - diese Signaturen bleiben
    // unveraendert.
    public int[] GetCowTreatmentChartData(int? year = null)
        => CowTreatmentLookups.GetCowTreatmentChartData(_cachedTreatments.Values, DateTime.Now, year);

    public int[] GetCowTreatmentMedicineChartData(int medicine, int? year = null)
        => CowTreatmentLookups.GetCowTreatmentMedicineChartData(_cachedTreatments.Values, DateTime.Now, medicine, year);

    // Bleibt async, obwohl nichts erwartet wird: das ist der heutige Zustand
    // und keine Aufraeumarbeit dieses Schrittes. Der Dienst-Parameter ist auf
    // die Naht verbreitert; herausgeholt wird daraus nur der Cache.
    public async Task<IEnumerable<string>> SearchCowTreatmentMedicaments(string value, CancellationToken token, IMedicineService medicineService)
    {
        return CowTreatmentLookups.SearchCowTreatmentMedicaments(medicineService.Medicines.Values, value);
    }

    public async Task<IEnumerable<string>> SearchCowTreatmentWhereHow(string value, CancellationToken token)
    {
        return CowTreatmentLookups.SearchCowTreatmentWhereHow(_whereHowService.WhereHowNames, value);
    }

    public int GetMinYear()
        => CowTreatmentLookups.GetMinYear(_cachedTreatments.Values, DateTime.Now);
}
