using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;

namespace BB_Cow.Services;

public class MedicineService
{
    private ImmutableDictionary<int, Medicine> _cachedMedicines = ImmutableDictionary<int, Medicine>.Empty;

        public ImmutableDictionary<int, Medicine> Medicines => _cachedMedicines;

        public async Task GetAllDataAsync()
        {
            var medicines = await DatabaseService.ReadDataAsync(@"SELECT * FROM Medicine;", reader =>
            {
                var medicine = new Medicine
                {
                    MedicineId = reader.GetInt32("Medicine_ID"),
                    MedicineName = reader.GetString("Medicine_Name")
                };
                return medicine;
            });

            _cachedMedicines = medicines.ToImmutableDictionary(m => m.MedicineId);
            LoggerService.LogInformation(typeof(MedicineService), $"Loaded {_cachedMedicines.Count} medicines.");
        }

        public async Task<bool> InsertDataAsync(Medicine medicine)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"INSERT INTO `Medicine` (`Medicine_Name`) VALUES (@MedicineName);";
                command.Parameters.AddWithValue("@MedicineName", medicine.MedicineName);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(MedicineService), $"Inserted medicine {medicine.MedicineName}.");
            }

            return isSuccess;
        }

        public async Task<bool> RemoveByIdAsync(int medicineId)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"DELETE FROM `Medicine` WHERE `Medicine_ID` = @MedicineId;";
                command.Parameters.AddWithValue("@MedicineId", medicineId);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess && _cachedMedicines.ContainsKey(medicineId))
            {
                _cachedMedicines = _cachedMedicines.Remove(medicineId);
            }

            return isSuccess;
        }

        public Medicine GetById(int medicineId)
        {
            return _cachedMedicines.ContainsKey(medicineId) ? _cachedMedicines[medicineId] : null;
        }
        
        public string GetMedicineNameById(int medicineId)
        {
            return _cachedMedicines.ContainsKey(medicineId) ? _cachedMedicines[medicineId].MedicineName : null;
        }
        
        public List<string> GetMedicineNames()
        {
            return _cachedMedicines.Values.Select(m => m.MedicineName).ToList();
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