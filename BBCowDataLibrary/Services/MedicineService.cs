using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services;

public class MedicineService
{
    private ImmutableDictionary<int, Medicine> _cachedMedicines = ImmutableDictionary<int, Medicine>.Empty;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;

        public ImmutableDictionary<int, Medicine> Medicines => _cachedMedicines;

        public MedicineService(IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
        {
            _contextFactory = contextFactory;
            _databaseStatusService = databaseStatusService;
        }

        public async Task GetAllDataAsync()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var medicines = await context.Medicines.AsNoTracking().ToListAsync();
                _cachedMedicines = medicines.ToImmutableDictionary(m => m.MedicineId);
                _databaseStatusService.ReportSuccess();
                LoggerService.LogInformation(typeof(MedicineService), $"Loaded {_cachedMedicines.Count} medicines.");
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(MedicineService), "Failed to load medicines, with {@Message}", ex, ex.Message);
            }
        }

        public async Task<bool> InsertDataAsync(Medicine medicine)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await context.Medicines.AddAsync(medicine);
                var isSuccess = await context.SaveChangesAsync() > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess)
                {
                    await GetAllDataAsync();
                    LoggerService.LogInformation(typeof(MedicineService), $"Inserted medicine {medicine.MedicineName}.");
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(MedicineService), "Failed to insert medicine, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public async Task<bool> RemoveByIdAsync(int medicineId)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.Medicines
                    .Where(m => m.MedicineId == medicineId)
                    .ExecuteDeleteAsync();

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedMedicines.ContainsKey(medicineId))
                {
                    _cachedMedicines = _cachedMedicines.Remove(medicineId);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(MedicineService), "Failed to delete medicine, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public async Task<bool> UpdateDataAsync(Medicine medicine)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.Medicines
                    .Where(m => m.MedicineId == medicine.MedicineId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.MedicineName, medicine.MedicineName));

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess)
                {
                    await GetAllDataAsync();
                    LoggerService.LogInformation(typeof(MedicineService), $"Updated medicine {medicine.MedicineName}.");
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(MedicineService), "Failed to update medicine, with {@Message}", ex, ex.Message);
                return false;
            }
        }
        
        public Medicine GetById(int medicineId)
        {
            return _cachedMedicines.ContainsKey(medicineId) ? _cachedMedicines[medicineId] : null;
        }
        
        public string GetMedicineNameById(int medicineId)
        {
            return _cachedMedicines.ContainsKey(medicineId) ? _cachedMedicines[medicineId].MedicineName : "--";
        }
        
        public List<string> GetMedicineNames()
        {
            return _cachedMedicines.Values.Select(m => m.MedicineName).ToList();
        }
        
        public List<string> GetMedicineNamesByIds(List<int> medicineIds)
        {
            return _cachedMedicines.Where(m => medicineIds.Contains(m.Key)).Select(m => m.Value.MedicineName).ToList();
        }
        
        public async Task<int> GetMedicineIdByName(string medicineName)
        {
            if(medicineName == null)
            {
                return int.MinValue;
            }
            
            var id =  _cachedMedicines.Values.FirstOrDefault(m => m.MedicineName.Trim().ToLower() == medicineName.Trim().ToLower())?.MedicineId ?? -1;
            if(id  == -1)
            {
                var newMedicine = new Medicine(0, medicineName.Trim());
                await InsertDataAsync(newMedicine);
                id = _cachedMedicines.FirstOrDefault(x => x.Value.MedicineName.Trim().ToLower() == medicineName.Trim().ToLower()).Key;
            }

            return id;
        }
}
