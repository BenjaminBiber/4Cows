using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using MySqlConnector;
using System.Linq;

namespace BB_Cow.Services
{
    public class CowTreatmentService
    {
        public List<Treatment_Cow> Treatments { get; set; } = new();
        public List<string> CowMedicineTreatmentList { get; set; } = new();
        public List<string> CowWhereHowList { get; set; } = new();

        public async Task GetAllDataAsync()
        {
            Treatments = await DatabaseService.ReadDataAsync(@"SELECT * FROM Cow_Treatment;", reader =>
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

            CowMedicineTreatmentList = Treatments.Select(t => t.Medicine_Name).Distinct().ToList();
            CowWhereHowList = Treatments.Select(t => t.WhereHow).Distinct().ToList();

        }

        public async Task<bool> InsertDataAsync(Treatment_Cow cow_Treatment)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"INSERT INTO `Cow_Treatment` (`Collar_Number`, `Administration_Date`, `Medicine_Dosage`, `Medicine_Name`, `WhereHow`, `Ear_Number`) VALUES (@Collar_Number, @Administration_Date, @Medicine_Dosage, @Medicine_Name, @WhereHow, @Ear_Number);";
                command.Parameters.AddWithValue("@Collar_Number", cow_Treatment.Collar_Number);
                command.Parameters.AddWithValue("@Administration_Date", cow_Treatment.Administration_Date);
                command.Parameters.AddWithValue("@Medicine_Dosage", cow_Treatment.Medicine_Dosage);
                command.Parameters.AddWithValue("@Medicine_Name", cow_Treatment.Medicine_Name);
                command.Parameters.AddWithValue("@WhereHow", cow_Treatment.WhereHow);
                command.Parameters.AddWithValue("@Ear_Number", cow_Treatment.Ear_Number);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });
            return isSuccess;
        }

        public async Task<Treatment_Cow> GetByIDAsync(int id)
        {
            var query = $"SELECT * FROM Cow_Treatment WHERE Cow_Treatment_ID = {id};";

            var treatments = await DatabaseService.ReadDataAsync(query, reader =>
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

            return treatments.FirstOrDefault() ?? new Treatment_Cow(); ;
        }

    }
}
