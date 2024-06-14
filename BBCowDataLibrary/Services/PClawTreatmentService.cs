using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using MySqlConnector;

namespace BB_Cow.Services
{
    public static class PClawTreatmentService
    {
        public static List<Planned_Treatment_Claw> StaticTreatments { get; set; } = new();

        public static async Task GetAllData()
        {
            StaticTreatments = await DatabaseService.ReadDataAsync(@"SELECT * FROM Planned_Claw_Treatment;", reader =>
            {
                var treatment = new Planned_Treatment_Claw
                {
                    Planned_Claw_Treatment_ID = reader.GetInt32("Planned_Claw_Treatment_ID"),
                    Collar_Number = reader.GetInt32("Collar_Number"),
                    Treatment_Date = reader.GetDateTime("Treatment_Date"),
                    Description = reader.GetString("Desciption"),
                    Claw_Finding_LV = reader.GetBoolean("Claw_Finding_LV"),
                    Claw_Finding_LH = reader.GetBoolean("Claw_Finding_LH"),
                    Claw_Finding_RV = reader.GetBoolean("Claw_Finding_RV"),
                    Claw_Finding_RH = reader.GetBoolean("Claw_Finding_RH")
                };
                return treatment;
            });
        }

        public static async Task<bool> InsertData(Planned_Treatment_Claw claw_Treatment)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"INSERT INTO `Planned_Claw_Treatment` (`Collar_Number`, `Treatment_Date`, `Desciption`, `Claw_Finding_LV`, `Claw_Finding_LH`, `Claw_Finding_RV`, `Claw_Finding_RH`) VALUES (@Collar_Number, @Treatment_Date, @Description, @Claw_Finding_LV, @Claw_Finding_LH, @Claw_Finding_RV, @Claw_Finding_RH);";
                command.Parameters.AddWithValue("@Collar_Number", claw_Treatment.Collar_Number);
                command.Parameters.AddWithValue("@Treatment_Date", claw_Treatment.Treatment_Date);
                command.Parameters.AddWithValue("@Description", claw_Treatment.Description);
                command.Parameters.AddWithValue("@Claw_Finding_LV", claw_Treatment.Claw_Finding_LV);
                command.Parameters.AddWithValue("@Claw_Finding_LH", claw_Treatment.Claw_Finding_LH);
                command.Parameters.AddWithValue("@Claw_Finding_RV", claw_Treatment.Claw_Finding_RV);
                command.Parameters.AddWithValue("@Claw_Finding_RH", claw_Treatment.Claw_Finding_RH);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });
            return isSuccess;
        }

        public static async Task<bool> RemoveByID(int id)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"DELETE FROM `Planned_Claw_Treatment` WHERE `Planned_Claw_Treatment_ID` = @id;";
                command.Parameters.AddWithValue("@id", id);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });
            return isSuccess;
        }
    }
}


