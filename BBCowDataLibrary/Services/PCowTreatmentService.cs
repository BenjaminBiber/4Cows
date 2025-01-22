using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;

namespace BB_Cow.Services
{
    public class PCowTreatmentService
    {
        private ImmutableDictionary<int, PlannedCowTreatment> _cachedTreatments = ImmutableDictionary<int, PlannedCowTreatment>.Empty;
        private ImmutableList<string> _cachedMedicineList = ImmutableList<string>.Empty;
        private ImmutableList<int> _cachedWhereHowList = ImmutableList<int>.Empty;

        public ImmutableDictionary<int, PlannedCowTreatment> Treatments => _cachedTreatments;
        public ImmutableList<string> CowMedicineTreatmentList => _cachedMedicineList;
        public ImmutableList<int> CowWhereHowList => _cachedWhereHowList;

        public async Task GetAllDataAsync()
        {
            var treatments = await DatabaseService.ReadDataAsync(@"SELECT * FROM Planned_Cow_Treatment;", reader =>
            {
                var treatment = new PlannedCowTreatment
                {
                    PlannedCowTreatmentId = reader.GetInt32("Planned_Cow_Treatment_ID"),
                    EarTagNumber = reader.GetString("Ear_Tag_Number"),
                    MedicineId = reader.GetInt32("Medicine_ID"),
                    AdministrationDate = reader.GetDateTime("Administration_Date"),
                    MedicineDosage = reader.GetFloat("Medicine_Dosage"),
                    WhereHowId = reader.GetInt32("WhereHow_ID"),
                    IsFound = reader.GetBoolean("IsFound"),
                    IsTreatet = reader.GetBoolean("IsTreatet")
                };
                return treatment;
            });

            _cachedTreatments = treatments.ToImmutableDictionary(t => t.PlannedCowTreatmentId);
            _cachedMedicineList = treatments.Select(t => t.MedicineId.ToString()).Distinct().ToImmutableList();
            _cachedWhereHowList = treatments.Select(t => t.WhereHowId).Distinct().ToImmutableList();
            LoggerService.LogInformation(typeof(PCowTreatmentService), $"Loaded {_cachedTreatments.Count} planned cow treatments.");
        }

        public async Task<bool> InsertDataAsync(PlannedCowTreatment cowTreatment)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"INSERT INTO `Planned_Cow_Treatment` (`Ear_Tag_Number`, `Medicine_ID`, `Administration_Date`, `Medicine_Dosage`, `WhereHow`, `IsFound`, `IsTreatet`) VALUES (@EarTagNumber, @MedicineId, @AdministrationDate, @MedicineDosage, @WhereHow, @IsFound, @IsTreatet);";
                command.Parameters.AddWithValue("@EarTagNumber", cowTreatment.EarTagNumber);
                command.Parameters.AddWithValue("@MedicineId", cowTreatment.MedicineId);
                command.Parameters.AddWithValue("@AdministrationDate", cowTreatment.AdministrationDate);
                command.Parameters.AddWithValue("@MedicineDosage", cowTreatment.MedicineDosage);
                command.Parameters.AddWithValue("@WhereHow", cowTreatment.WhereHowId);
                command.Parameters.AddWithValue("@IsFound", cowTreatment.IsFound);
                command.Parameters.AddWithValue("@IsTreatet", cowTreatment.IsTreatet);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(PCowTreatmentService), "Data inserted successfully: {@cowTreatment}", cowTreatment);
            }

            return isSuccess;
        }

        public async Task<bool> RemoveByIDAsync(int id)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"DELETE FROM `Planned_Cow_Treatment` WHERE `Planned_Cow_Treatment_ID` = @id;";
                command.Parameters.AddWithValue("@id", id);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess && _cachedTreatments.ContainsKey(id))
            {
                _cachedTreatments = _cachedTreatments.Remove(id);
                _cachedMedicineList = _cachedTreatments.Values.Select(t => t.MedicineId.ToString()).Distinct().ToImmutableList();
                _cachedWhereHowList = _cachedTreatments.Values.Select(t => t.WhereHowId).Distinct().ToImmutableList();
                LoggerService.LogInformation(typeof(PCowTreatmentService), "Data removed successfully: {@id}", id);
            }

            return isSuccess;
        }

        public PlannedCowTreatment GetById(int id)
        {
            return _cachedTreatments.ContainsKey(id) ? _cachedTreatments[id] : null;
        }
    }
}
