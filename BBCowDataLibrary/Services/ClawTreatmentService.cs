using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services
{
    public class ClawTreatmentService
    {
        private ImmutableDictionary<int, ClawTreatment> _cachedTreatments = ImmutableDictionary<int, ClawTreatment>.Empty;
        private ImmutableList<string> _cachedClawFindingList = ImmutableList<string>.Empty;
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;
        private readonly DatabaseStatusService _databaseStatusService;

        public ImmutableDictionary<int, ClawTreatment> Treatments => _cachedTreatments;
        public ImmutableList<string> ClawFindingList => _cachedClawFindingList;

        public ClawTreatmentService(IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
        {
            _contextFactory = contextFactory;
            _databaseStatusService = databaseStatusService;
        }

        public async Task GetAllDataAsync()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var treatments = await context.ClawTreatments.AsNoTracking().ToListAsync();
                _cachedTreatments = treatments.ToImmutableDictionary(t => t.ClawTreatmentId);
                _cachedClawFindingList = treatments.SelectMany(t => new[] { t.ClawFindingLV, t.ClawFindingLH, t.ClawFindingRV, t.ClawFindingRH }).Distinct().ToImmutableList();
                _databaseStatusService.ReportSuccess();
                LoggerService.LogInformation(typeof(ClawTreatmentService), $"Loaded {_cachedTreatments.Count} claw treatments.");
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(ClawTreatmentService), "Failed to load claw treatments, with {@Message}", ex, ex.Message);
            }
        }

        public async Task<bool> InsertDataAsync(ClawTreatment clawTreatment)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await context.ClawTreatments.AddAsync(clawTreatment);
                var isSuccess = await context.SaveChangesAsync() > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess)
                {
                    _cachedTreatments = _cachedTreatments.Add(clawTreatment.ClawTreatmentId, clawTreatment);
                    _cachedClawFindingList = _cachedClawFindingList.AddRange(new[] { clawTreatment.ClawFindingLV, clawTreatment.ClawFindingLH, clawTreatment.ClawFindingRV, clawTreatment.ClawFindingRH }).Distinct().ToImmutableList();
                    LoggerService.LogInformation(typeof(ClawTreatmentService), "Inserted claw treatment: {@clawTreatment}.", clawTreatment);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(ClawTreatmentService), "Failed to insert claw treatment, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public async Task<ClawTreatment> GetByIDAsync(int id)
        {
            if (_cachedTreatments.ContainsKey(id))
            {
                return _cachedTreatments[id];
            }

            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var treatmentResult = await context.ClawTreatments.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.ClawTreatmentId == id);
                _databaseStatusService.ReportSuccess();

                if (treatmentResult != null)
                {
                    _cachedTreatments = _cachedTreatments.Add(id, treatmentResult);
                }

                return treatmentResult ?? new ClawTreatment();
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(ClawTreatmentService), "Failed to load claw treatment, with {@Message}", ex, ex.Message);
                return new ClawTreatment();
            }
        }

        public async Task<bool> RemoveBandageAsync(int id)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.ClawTreatments
                    .Where(t => t.ClawTreatmentId == id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsBandageRemoved, true));

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedTreatments.ContainsKey(id))
                {
                    var updatedTreatment = _cachedTreatments[id];
                    updatedTreatment.IsBandageRemoved = true;
                    _cachedTreatments = _cachedTreatments.SetItem(id, updatedTreatment);
                    LoggerService.LogInformation(typeof(ClawTreatmentService), "Removed bandage from claw treatment: {@clawTreatment}.", updatedTreatment);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(ClawTreatmentService), "Failed to update bandage flag, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public async Task DeleteDataAsync(int id)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await context.ClawTreatments.Where(t => t.ClawTreatmentId == id).ExecuteDeleteAsync();
                _databaseStatusService.ReportSuccess();

                if (_cachedTreatments.ContainsKey(id))
                {
                    _cachedTreatments = _cachedTreatments.Remove(id);
                    LoggerService.LogInformation(typeof(ClawTreatmentService), "Deleted claw treatment with ID: {id}.", id);
                }
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(ClawTreatmentService), "Failed to delete claw treatment, with {@Message}", ex, ex.Message);
            }
        }
        
        public int[] GetClawTreatmentChartData(int? year = null)
        {
            var currentYear = year.HasValue ? year.Value : DateTime.Now.Year;
            var months = Enumerable.Range(1, 12);

            var groupedData = _cachedTreatments.Values
                .Where(obj => obj.TreatmentDate.Year == currentYear)
                .GroupBy(obj => obj.TreatmentDate.Month)
                .ToDictionary(g => g.Key, g => g.Count());

            return months
                .Select(month => groupedData.ContainsKey(month) ? groupedData[month] : 0)
                .ToArray();
        }

        public List<ClawTreatment> GetClawTreatments()
        {
            return Treatments.Values.ToList();
        }

        public List<ClawTreatment> GetClawTreatmentsWithBandage()
        {
            return Treatments.Values.Where(x => (x.BandageLH || x.BandageLV || x.BandageRV || x.BandageRH) && !x.IsBandageRemoved).OrderBy(x => x.TreatmentDate).ToList();
        }

    }
}
