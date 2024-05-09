using BB_Cow.Class;
using MySqlConnector;
using System.Linq;

namespace BB_Cow.Services
{
    public static class Cow_Treatment_Static
    {
        public static List<Treatment_Cow> StaticTreatments { get; set; } = new();
        public static List<string> StaticCowMedicineTreatmentList { get; set; } = new();
        public static List<string> StaticCowWhereHowList { get; set; } = new();

        public static void GetAllData()
        {
            StaticTreatments = DatabaseService.ReadData(@"SELECT * FROM Cow_Treatment;", reader =>
            {
                var treatment = new Treatment_Cow
                {
                    Cow_Treatment_ID = reader.GetInt32("Cow_Treatment_ID"),
                    Collar_Number = reader.GetInt32("Collar_Number"),
                    Administration_Date = reader.GetDateTime("Administration_Date"),
                    Medicine_Dosage = reader.GetFloat("Medicine_Dosage"),
                    Medicine_Name = reader.GetString("Medicine_Name"),
                    WhereHow = reader.GetString("WhereHow"),
                    Ear_Number = reader.GetInt32("Ear_Number")
                };
                return treatment;
            });

            StaticCowMedicineTreatmentList = StaticTreatments.Select(t => t.Medicine_Name).Distinct().ToList();
            StaticCowWhereHowList = StaticTreatments.Select(t => t.WhereHow).Distinct().ToList();

        }

        public static bool InsertData(Treatment_Cow cow_Treatment)
        {
            bool isSuccess = false;
            DatabaseService.ExecuteQuery(command =>
            {
                command.CommandText = @"INSERT INTO `Cow_Treatment` (`Collar_Number`, `Administration_Date`, `Medicine_Dosage`, `Medicine_Name`, `WhereHow`, `Ear_Number`) VALUES (@Collar_Number, @Administration_Date, @Medicine_Dosage, @Medicine_Name, @WhereHow, @Ear_Number);";
                command.Parameters.AddWithValue("@Collar_Number", cow_Treatment.Collar_Number);
                command.Parameters.AddWithValue("@Administration_Date", cow_Treatment.Administration_Date);
                command.Parameters.AddWithValue("@Medicine_Dosage", cow_Treatment.Medicine_Dosage);
                command.Parameters.AddWithValue("@Medicine_Name", cow_Treatment.Medicine_Name);
                command.Parameters.AddWithValue("@WhereHow", cow_Treatment.WhereHow);
                command.Parameters.AddWithValue("@Ear_Number", cow_Treatment.Ear_Number);
                isSuccess = command.ExecuteNonQuery() > 0;
            });
            return isSuccess;
        }

        public static Treatment_Cow GetByID(int id)
        {
            return DatabaseService.ReadData($"SELECT * FROM Cow_Treatment WHERE Cow_Treatment_ID = {id};", reader =>
            {
                var treatment = new Treatment_Cow
                {
                    Cow_Treatment_ID = reader.GetInt32("Cow_Treatment_ID"),
                    Collar_Number = reader.GetInt32("Collar_Number"),
                    Administration_Date = reader.GetDateTime("Administration_Date"),
                    Medicine_Dosage = reader.GetFloat("Medicine_Dosage"),
                    Medicine_Name = reader.GetString("Medicine_Name"),
                    WhereHow = reader.GetString("WhereHow"),
                    Ear_Number = reader.GetInt32("Ear_Number")

                };
                return treatment;
            }).FirstOrDefault();
        }
    }
}
