using MySqlConnector;
using BB_Cow.Class;
using System.Reflection.Metadata.Ecma335;

namespace BB_Cow.Services
{
    public static class Planned_Cow_Treatment_Static
    {
        public static List<Planned_Treatment_Cow> StaticTreatments { get; set; } = new();
        public static List<string> StaticCowMedicineTreatmentList { get; set; } = new();
        public static List<string> StaticCowWhereHowList { get; set; } = new();

        public static void GetAllData()
        {
            StaticTreatments = DatabaseService.ReadData(@"SELECT * FROM planned_Cow_Treatment;", reader =>
            {
                var treatment = new Planned_Treatment_Cow
                {
                    Planned_Cow_Treatment_ID = reader.GetInt32("Planned_Cow_Treatment_ID"),
                    Collar_Number = reader.GetInt32("Collar_Number"),
                    Administration_Date = reader.GetDateTime("Administration_Date"),
                    Medicine_Dosage = reader.GetFloat("Medicine_Dosage"),
                    Medicine_Name = reader.GetString("Medicine_Name"),
                    WhereHow = reader.GetString("WhereHow"),
                    IsFound = reader.GetBoolean("IsFound"),
                    IsTreatet = reader.GetBoolean("IsTreatet"),
                    Ear_Number = reader.GetInt32("Ear_Number")

                };
                return treatment;
            });

            StaticCowMedicineTreatmentList = StaticTreatments.Select(t => t.Medicine_Name).Distinct().ToList();
            StaticCowWhereHowList = StaticTreatments.Select(t => t.WhereHow).Distinct().ToList();

        }

        public static bool InsertData(Planned_Treatment_Cow cow_Treatment)
        {
            bool isSuccess = false;
            DatabaseService.ExecuteQuery(command =>
            {
                command.CommandText = @"INSERT INTO `planned_Cow_Treatment` (`Collar_Number`, `Administration_Date`, `Medicine_Dosage`, `Medicine_Name`, `WhereHow`, `IsFound`, `IsTreatet`, `Ear_Number`) VALUES (@Collar_Number, @Administration_Date, @Medicine_Dosage, @Medicine_Name, @WhereHow, @IsFound, @IsTreatet, @Ear_Number);";
                command.Parameters.AddWithValue("@Collar_Number", cow_Treatment.Collar_Number);
                command.Parameters.AddWithValue("@Administration_Date", cow_Treatment.Administration_Date);
                command.Parameters.AddWithValue("@Medicine_Dosage", cow_Treatment.Medicine_Dosage);
                command.Parameters.AddWithValue("@Medicine_Name", cow_Treatment.Medicine_Name);
                command.Parameters.AddWithValue("@WhereHow", cow_Treatment.WhereHow);
                command.Parameters.AddWithValue("@IsFound", cow_Treatment.IsFound);
                command.Parameters.AddWithValue("@IsTreatet", cow_Treatment.IsTreatet);
                command.Parameters.AddWithValue("@Ear_Number", cow_Treatment.Ear_Number);
                isSuccess = command.ExecuteNonQuery() > 0;
            });
            return isSuccess;
        }
        public static bool RemoveByID(int id)
        {
            bool isSuccess = false;
            DatabaseService.ExecuteQuery(command =>
            {
                command.CommandText = @"DELETE FROM `planned_Cow_Treatment` WHERE `Planned_Cow_Treatment_ID` = @id;";
                command.Parameters.AddWithValue("@id", id);
                isSuccess = command.ExecuteNonQuery() > 0;
            });
            return isSuccess;
        }
    }
}

