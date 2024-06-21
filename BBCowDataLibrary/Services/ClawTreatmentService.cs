using MySqlConnector;
using BB_Cow.Class;
using System.Reflection.Metadata.Ecma335;
using BBCowDataLibrary.SQL;

namespace BB_Cow.Services
{
    public class ClawTreatmentService
    {
        public List<Treatment_Claw> Treatments { get; set; } = new();
        public List<string> ClawFindingList { get; set; } = new();

        public async Task GetAllDataAsync()
        {
            Treatments = await DatabaseService.ReadDataAsync(@"SELECT * FROM `Claw_Treatment`;",reader =>
            {
                var treatment = new Treatment_Claw
                {
                    Claw_Treatment_ID = reader.GetInt32("Claw_Treatment_ID"),
                    Collar_Number = reader.GetInt32("Collar_Number"),
                    Treatment_Date = reader.GetDateTime("Treatment_Date"),
                    Claw_Finding_LV = reader.GetString("Claw_Finding_LV"),
                    Bandage_LV = reader.GetBoolean("Bandage_LV"),
                    Block_LV = reader.GetBoolean("Block_LV"),
                    Claw_Finding_LH = reader.GetString("Claw_Finding_LH"),
                    Bandage_LH = reader.GetBoolean("Bandage_LH"),
                    Block_LH = reader.GetBoolean("Block_LH"),
                    Claw_Finding_RV = reader.GetString("Claw_Finding_RV"),
                    Bandage_RV = reader.GetBoolean("Bandage_RV"),
                    Block_RV = reader.GetBoolean("Block_RV"),
                    Claw_Finding_RH = reader.GetString("Claw_Finding_RH"),
                    Bandage_RH = reader.GetBoolean("Bandage_RH"),
                    Block_RH = reader.GetBoolean("Block_RH"),
                    IsBandageRemoved = reader.GetBoolean("IsBandageRemoved")
                };
                return treatment;
            });
            ClawFindingList = Treatments.SelectMany(t => new[] { t.Claw_Finding_LV, t.Claw_Finding_LH, t.Claw_Finding_RV, t.Claw_Finding_RH }).Distinct().ToList();
        }

        public async Task<bool> InsertDataAsync(Treatment_Claw claw_Treatment)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync( async command =>
            {
                command.CommandText = @"INSERT INTO `Claw_Treatment` (`Collar_Number`, `Treatment_Date`, `Claw_Finding_LV`, `Bandage_LV`, `Block_LV`, `Claw_Finding_LH`, `Bandage_LH`, `Block_LH`, `Claw_Finding_RV`, `Bandage_RV`, `Block_RV`, `Claw_Finding_RH`, `Bandage_RH`, `Block_RH`, `IsBandageRemoved`) VALUES (@Collar_Number, @Treatment_Date, @Claw_Finding_LV, @Bandage_LV, @Block_LV, @Claw_Finding_LH, @Bandage_LH, @Block_LH, @Claw_Finding_RV, @Bandage_RV, @Block_RV, @Claw_Finding_RH, @Bandage_RH, @Block_RH, @IsBandageRemoved);";
                command.Parameters.AddWithValue("@Collar_Number", claw_Treatment.Collar_Number);
                command.Parameters.AddWithValue("@Treatment_Date", claw_Treatment.Treatment_Date);
                command.Parameters.AddWithValue("@Claw_Finding_LV", claw_Treatment.Claw_Finding_LV);
                command.Parameters.AddWithValue("@Bandage_LV", claw_Treatment.Bandage_LV);
                command.Parameters.AddWithValue("@Block_LV", claw_Treatment.Block_LV);
                command.Parameters.AddWithValue("@Claw_Finding_LH", claw_Treatment.Claw_Finding_LH);
                command.Parameters.AddWithValue("@Bandage_LH", claw_Treatment.Bandage_LH);
                command.Parameters.AddWithValue("@Block_LH", claw_Treatment.Block_LH);
                command.Parameters.AddWithValue("@Claw_Finding_RV", claw_Treatment.Claw_Finding_RV);
                command.Parameters.AddWithValue("@Bandage_RV", claw_Treatment.Bandage_RV);
                command.Parameters.AddWithValue("@Block_RV", claw_Treatment.Block_RV);
                command.Parameters.AddWithValue("@Claw_Finding_RH", claw_Treatment.Claw_Finding_RH);
                command.Parameters.AddWithValue("@Bandage_RH", claw_Treatment.Bandage_RH);
                command.Parameters.AddWithValue("@Block_RH", claw_Treatment.Block_RH);
                command.Parameters.AddWithValue("@IsBandageRemoved", claw_Treatment.IsBandageRemoved);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });
            return isSuccess;
        }

        public async Task<Treatment_Claw> GetByIDAsync(int id)
        {
            var treatments =  await DatabaseService.ReadDataAsync($"SELECT * FROM `Claw_Treatment` WHERE Claw_Treatment_ID = {id};", reader =>
            {
                var treatment = new Treatment_Claw
                {
                    Claw_Treatment_ID = reader.GetInt32("Claw_Treatment_ID"),
                    Collar_Number = reader.GetInt32("Collar_Number"),
                    Treatment_Date = reader.GetDateTime("Treatment_Date"),
                    Claw_Finding_LV = reader.GetString("Claw_Finding_LV"),
                    Bandage_LV = reader.GetBoolean("Bandage_LV"),
                    Block_LV = reader.GetBoolean("Block_LV"),
                    Claw_Finding_LH = reader.GetString("Claw_Finding_LH"),
                    Bandage_LH = reader.GetBoolean("Bandage_LH"),
                    Block_LH = reader.GetBoolean("Block_LH"),
                    Claw_Finding_RV = reader.GetString("Claw_Finding_RV"),
                    Bandage_RV = reader.GetBoolean("Bandage_RV"),
                    Block_RV = reader.GetBoolean("Block_RV"),
                    Claw_Finding_RH = reader.GetString("Claw_Finding_RH"),
                    Bandage_RH = reader.GetBoolean("Bandage_RH"),
                    Block_RH = reader.GetBoolean("Block_RH"),
                    IsBandageRemoved = reader.GetBoolean("IsBandageRemoved")
                };
                return treatment;
            });

            return treatments.FirstOrDefault() ?? new Treatment_Claw();
        }

        public async Task<bool> RemoveBandageAsync(int id)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"UPDATE `Claw_Treatment` SET `IsBandageRemoved` = true WHERE `Claw_Treatment_ID` = @id;";
                command.Parameters.AddWithValue("@id", id);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });
            return isSuccess;
        }
    }

}
