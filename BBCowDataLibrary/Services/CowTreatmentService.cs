using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using BB_Cow.Class;
using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

public class CowTreatmentService
{
    private ImmutableDictionary<int, CowTreatment> _cachedTreatments = ImmutableDictionary<int, CowTreatment>.Empty;
    private ImmutableList<int> _cachedDistinctWhereHows = ImmutableList<int>.Empty;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;

    public ImmutableDictionary<int, CowTreatment> Treatments => _cachedTreatments;
    public ImmutableList<int> DistinctWhereHows => _cachedDistinctWhereHows;
    private readonly WhereHowService _whereHowService;
    public CowTreatmentService(WhereHowService whereHowService, IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
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

    public async Task<bool> InsertDataAsync(CowTreatment cowTreatment)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.CowTreatments.AddAsync(cowTreatment);
            var isSuccess = await context.SaveChangesAsync() > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(CowTreatmentService), "Inserted cow treatment: {@cowTreatment}.", cowTreatment);
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(CowTreatmentService), "Failed to insert cow treatment, with {@Message}", ex, ex.Message);
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
                _cachedTreatments = _cachedTreatments.Add(id, treatmentResult);
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
    
    public int[] GetCowTreatmentChartData(int? year = null)
    {
        var currentYear = year.HasValue ? year.Value :  DateTime.Now.Year;
        var months = Enumerable.Range(1, 12);

        var groupedData = _cachedTreatments.Values
            .Where(obj => obj.AdministrationDate.Year == currentYear) 
            .GroupBy(obj => obj.AdministrationDate.Month)
            .ToDictionary(g => g.Key, g => g.Count());

        return months
            .Select(month => groupedData.ContainsKey(month) ? groupedData[month] : 0)
            .ToArray();
    }

    public int[] GetCowTreatmentMedicineChartData(int medicine, int? year = null)
    {
        var currentYear = DateTime.Now.Year;
        if (year.HasValue)
        {
            currentYear = year.Value;
        }
        
        var months = Enumerable.Range(1, 12);

        return months
            .Select(month => _cachedTreatments.Values
                .Count(obj => obj.AdministrationDate.Year == currentYear && 
                              obj.AdministrationDate.Month == month &&
                              obj.MedicineId == medicine))
            .ToArray();
    }
    
    public async Task<IEnumerable<string>> SearchCowTreatmentMedicaments(string value, CancellationToken token, MedicineService medicineService)
    {
        if (string.IsNullOrEmpty(value))
        {
            return medicineService.GetMedicineNames().Order();
        }
        return medicineService.GetMedicineNames().Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase));
    }

    public async Task<IEnumerable<string>> SearchCowTreatmentWhereHow(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value) || !_whereHowService.WhereHowNames.Any())
        {
            return _whereHowService.WhereHowNames;
        }

        if(!string.IsNullOrEmpty(value) && !_whereHowService.WhereHowNames.Any(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)))
        {
            return new List<string>() { value.Trim() };
        }
        
        return _whereHowService.WhereHowNames.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase));
    }

    public int GetMinYear()
    {
        return _cachedTreatments.Values.Min(t => t.AdministrationDate).Year;
    }
}
