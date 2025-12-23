using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services
{
    public class PClawTreatmentService
    {
        private ImmutableDictionary<int, PlannedClawTreatment> _cachedTreatments = ImmutableDictionary<int, PlannedClawTreatment>.Empty;
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;
        private readonly DatabaseStatusService _databaseStatusService;

        public ImmutableDictionary<int, PlannedClawTreatment> Treatments => _cachedTreatments;

        public PClawTreatmentService(IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
        {
            _contextFactory = contextFactory;
            _databaseStatusService = databaseStatusService;
        }

        public async Task GetAllDataAsync()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var treatments = await context.PlannedClawTreatments.AsNoTracking().ToListAsync();
                _cachedTreatments = treatments.ToImmutableDictionary(t => t.PlannedClawTreatmentId);
                _databaseStatusService.ReportSuccess();
                LoggerService.LogInformation(typeof(PClawTreatmentService), $"Loaded {_cachedTreatments.Count} planned claw treatments.");
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(PClawTreatmentService), "Failed to load planned claw treatments, with {@Message}", ex, ex.Message);
            }
        }

        public async Task<bool> InsertDataAsync(PlannedClawTreatment clawTreatment)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await context.PlannedClawTreatments.AddAsync(clawTreatment);
                var isSuccess = await context.SaveChangesAsync() > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess)
                {
                    _cachedTreatments = _cachedTreatments.Add(clawTreatment.PlannedClawTreatmentId, clawTreatment);
                    LoggerService.LogInformation(typeof(PClawTreatmentService), "Data inserted successfully: {@clawTreatment}", clawTreatment);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(PClawTreatmentService), "Failed to insert planned claw treatment, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public async Task<bool> RemoveByIDAsync(int id)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.PlannedClawTreatments
                    .Where(t => t.PlannedClawTreatmentId == id)
                    .ExecuteDeleteAsync();

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedTreatments.ContainsKey(id))
                {
                    _cachedTreatments = _cachedTreatments.Remove(id);
                    LoggerService.LogInformation(typeof(PClawTreatmentService), "Data removed successfully: {@id}", id);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(PClawTreatmentService), "Failed to delete planned claw treatment, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public PlannedClawTreatment GetById(int id)
        {
            return _cachedTreatments.ContainsKey(id) ? _cachedTreatments[id] : null;
        }
    }
}
