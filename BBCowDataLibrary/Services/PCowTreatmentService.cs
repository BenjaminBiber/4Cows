using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services
{
    public class PCowTreatmentService
    {
        private ImmutableDictionary<int, PlannedCowTreatment> _cachedTreatments = ImmutableDictionary<int, PlannedCowTreatment>.Empty;
        private ImmutableList<string> _cachedMedicineList = ImmutableList<string>.Empty;
        private ImmutableList<int> _cachedWhereHowList = ImmutableList<int>.Empty;
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;
        private readonly DatabaseStatusService _databaseStatusService;

        public ImmutableDictionary<int, PlannedCowTreatment> Treatments => _cachedTreatments;
        public ImmutableList<string> CowMedicineTreatmentList => _cachedMedicineList;
        public ImmutableList<int> CowWhereHowList => _cachedWhereHowList;

        public PCowTreatmentService(IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
        {
            _contextFactory = contextFactory;
            _databaseStatusService = databaseStatusService;
        }

        public async Task GetAllDataAsync()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var treatments = await context.PlannedCowTreatments.AsNoTracking().ToListAsync();
                _cachedTreatments = treatments.ToImmutableDictionary(t => t.PlannedCowTreatmentId);
                _cachedMedicineList = treatments.Select(t => t.MedicineId.ToString()).Distinct().ToImmutableList();
                _cachedWhereHowList = treatments.Select(t => t.WhereHowId).Distinct().ToImmutableList();
                _databaseStatusService.ReportSuccess();
                LoggerService.LogInformation(typeof(PCowTreatmentService), $"Loaded {_cachedTreatments.Count} planned cow treatments.");
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(PCowTreatmentService), "Failed to load planned cow treatments, with {@Message}", ex, ex.Message);
            }
        }

        public async Task<bool> InsertDataAsync(PlannedCowTreatment cowTreatment)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await context.PlannedCowTreatments.AddAsync(cowTreatment);
                var isSuccess = await context.SaveChangesAsync() > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess)
                {
                    await GetAllDataAsync();
                    LoggerService.LogInformation(typeof(PCowTreatmentService), "Data inserted successfully: {@cowTreatment}", cowTreatment);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(PCowTreatmentService), "Failed to insert planned cow treatment, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public async Task<bool> RemoveByIDAsync(int id)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.PlannedCowTreatments
                    .Where(t => t.PlannedCowTreatmentId == id)
                    .ExecuteDeleteAsync();

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedTreatments.ContainsKey(id))
                {
                    _cachedTreatments = _cachedTreatments.Remove(id);
                    _cachedMedicineList = _cachedTreatments.Values.Select(t => t.MedicineId.ToString()).Distinct().ToImmutableList();
                    _cachedWhereHowList = _cachedTreatments.Values.Select(t => t.WhereHowId).Distinct().ToImmutableList();
                    LoggerService.LogInformation(typeof(PCowTreatmentService), "Data removed successfully: {@id}", id);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(PCowTreatmentService), "Failed to delete planned cow treatment, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public PlannedCowTreatment GetById(int id)
        {
            return _cachedTreatments.ContainsKey(id) ? _cachedTreatments[id] : null;
        }
    }
}
