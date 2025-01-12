using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;

namespace BB_Cow.Services
{
    public class PClawTreatmentService
    {
        private ImmutableDictionary<int, PlannedClawTreatment> _cachedTreatments = ImmutableDictionary<int, PlannedClawTreatment>.Empty;

        public ImmutableDictionary<int, PlannedClawTreatment> Treatments => _cachedTreatments;

        public async Task GetAllDataAsync()
        {
            var treatments = await DatabaseService.ReadDataAsync(@"SELECT * FROM Planned_Claw_Treatment;", reader =>
            {
                var descriptionIndex = reader.GetOrdinal("Desciption"); 

                var treatment = new PlannedClawTreatment
                {
                    PlannedClawTreatmentId = reader.GetInt32("Planned_Claw_Treatment_ID"),
                    EarTagNumber = reader.GetString("Ear_Tag_Number"),
                    TreatmentDate = reader.GetDateTime("Treatment_Date"),
                    Desciption = reader.IsDBNull(descriptionIndex) ? null : reader.GetString(descriptionIndex),
                    ClawFindingLV = reader.GetBoolean("Claw_Finding_LV"),
                    ClawFindingLH = reader.GetBoolean("Claw_Finding_LH"),
                    ClawFindingRV = reader.GetBoolean("Claw_Finding_RV"),
                    ClawFindingRH = reader.GetBoolean("Claw_Finding_RH")
                };
                return treatment;
            });

            _cachedTreatments = treatments.ToImmutableDictionary(t => t.PlannedClawTreatmentId);
            LoggerService.LogInformation(typeof(PClawTreatmentService), $"Loaded {_cachedTreatments.Count} planned claw treatments.");
        }

        public async Task<bool> InsertDataAsync(PlannedClawTreatment clawTreatment)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"INSERT INTO `Planned_Claw_Treatment` (`Ear_Tag_Number`, `Treatment_Date`, `Desciption`, `Claw_Finding_LV`, `Claw_Finding_LH`, `Claw_Finding_RV`, `Claw_Finding_RH`) VALUES (@EarTagNumber, @TreatmentDate, @Description, @ClawFindingLV, @ClawFindingLH, @ClawFindingRV, @ClawFindingRH);";
                command.Parameters.AddWithValue("@EarTagNumber", clawTreatment.EarTagNumber);
                command.Parameters.AddWithValue("@TreatmentDate", clawTreatment.TreatmentDate);
                command.Parameters.AddWithValue("@Description", clawTreatment.Desciption);
                command.Parameters.AddWithValue("@ClawFindingLV", clawTreatment.ClawFindingLV);
                command.Parameters.AddWithValue("@ClawFindingLH", clawTreatment.ClawFindingLH);
                command.Parameters.AddWithValue("@ClawFindingRV", clawTreatment.ClawFindingRV);
                command.Parameters.AddWithValue("@ClawFindingRH", clawTreatment.ClawFindingRH);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess)
            {
                _cachedTreatments = _cachedTreatments.Add(clawTreatment.PlannedClawTreatmentId, clawTreatment);
                LoggerService.LogInformation(typeof(PClawTreatmentService), "Data inserted successfully: {@clawTreatment}", clawTreatment);
            }

            return isSuccess;
        }

        public async Task<bool> RemoveByIDAsync(int id)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"DELETE FROM `Planned_Claw_Treatment` WHERE `Planned_Claw_Treatment_ID` = @id;";
                command.Parameters.AddWithValue("@id", id);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess && _cachedTreatments.ContainsKey(id))
            {
                _cachedTreatments = _cachedTreatments.Remove(id);
                LoggerService.LogInformation(typeof(PClawTreatmentService), "Data removed successfully: {@id}", id);
            }

            return isSuccess;
        }

        public PlannedClawTreatment GetById(int id)
        {
            return _cachedTreatments.ContainsKey(id) ? _cachedTreatments[id] : null;
        }
    }
}
