using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using BB_Cow.Class;
using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.Extensions.Logging;

public class CowTreatmentService
{
    private ImmutableDictionary<int, CowTreatment> _cachedTreatments = ImmutableDictionary<int, CowTreatment>.Empty;
    private ImmutableList<int> _cachedDistinctWhereHows = ImmutableList<int>.Empty;

    public ImmutableDictionary<int, CowTreatment> Treatments => _cachedTreatments;
    public ImmutableList<int> DistinctWhereHows => _cachedDistinctWhereHows;
    private readonly WhereHowService _whereHowService;
    public CowTreatmentService(WhereHowService whereHowService)
    {
        _whereHowService = whereHowService;
    }
    
    public async Task GetAllDataAsync()
    {
        var dbResult = await DatabaseService.ReadDataAsync("SELECT * FROM Cow_Treatment;", reader =>
        {
            var treatment = new CowTreatment()
            {
                CowTreatmentId = reader.GetInt32("Cow_Treatment_ID"),
                EarTagNumber = reader.GetString("Ear_Tag_Number"),
                AdministrationDate = reader.GetDateTime("Administration_Date"),
                MedicineDosage = reader.GetFloat("Medicine_Dosage"),
                MedicineId = reader.GetInt32("Medicine_ID"),
                WhereHowId = reader.GetInt32("WhereHow_ID"),
            };
            return treatment;
        });

        _cachedDistinctWhereHows = dbResult.Select(t => t.WhereHowId).Distinct().ToImmutableList();
        _cachedTreatments = dbResult.ToImmutableDictionary(t => t.CowTreatmentId);
        LoggerService.LogInformation(typeof(CowTreatmentService), $"Loaded {_cachedTreatments.Count} cow treatments.");
    }

    public async Task<bool> InsertDataAsync(CowTreatment cowTreatment)
    {
        bool isSuccess = false;
        await DatabaseService.ExecuteQueryAsync(async command =>
        {
            command.CommandText = @"INSERT INTO `Cow_Treatment` (`Ear_Tag_Number`, `Administration_Date`, `Medicine_Dosage`, `Medicine_ID`, `WhereHow_ID`) VALUES (@EarTagNumber, @AdministrationDate, @MedicineDosage, @MedicineId, @WhereHow);";
            command.Parameters.AddWithValue("@EarTagNumber", cowTreatment.EarTagNumber);
            command.Parameters.AddWithValue("@AdministrationDate", cowTreatment.AdministrationDate);
            command.Parameters.AddWithValue("@MedicineDosage", cowTreatment.MedicineDosage);
            command.Parameters.AddWithValue("@MedicineId", cowTreatment.MedicineId);
            command.Parameters.AddWithValue("@WhereHow_ID", cowTreatment.WhereHowId);
            isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
        });

        if (isSuccess)
        {
            await GetAllDataAsync();
            LoggerService.LogInformation(typeof(CowTreatmentService), "Inserted cow treatment: {@cowTreatment}.", cowTreatment);
        }

        return isSuccess;
    }

    public async Task<CowTreatment> GetByIdAsync(int id)
    {
        if (_cachedTreatments.ContainsKey(id))
        {
            return _cachedTreatments[id];
        }

        var query = "SELECT * FROM Cow_Treatment WHERE Cow_Treatment_ID = @Id;";

        var treatments = await DatabaseService.ReadDataAsync(query, reader =>
        {
            var treatment = new CowTreatment
            {
                CowTreatmentId = reader.GetInt32("Cow_Treatment_ID"),
                EarTagNumber = reader.GetString("Ear_Tag_Number"),
                AdministrationDate = reader.GetDateTime("Administration_Date"),
                MedicineDosage = reader.GetFloat("Medicine_Dosage"),
                MedicineId = reader.GetInt32("Medicine_ID"),
                WhereHowId = reader.GetInt32("WhereHow_ID"),
            };
            return treatment;
        }, new { Id = id });

        var treatmentResult = treatments.FirstOrDefault();
        if (treatmentResult != null)
        {
            _cachedTreatments = _cachedTreatments.Add(id, treatmentResult);
        }

        return treatmentResult ?? new CowTreatment();
    }

    public async Task DeleteDataAsync(int id)
    {
        await DatabaseService.ExecuteQueryAsync(async command =>
        {
            command.CommandText = "DELETE FROM Cow_Treatment WHERE Cow_Treatment_ID = @Id;";
            command.Parameters.AddWithValue("@Id", id);
            await command.ExecuteNonQueryAsync();
        });

        if (_cachedTreatments.ContainsKey(id))
        {
            _cachedTreatments = _cachedTreatments.Remove(id);
            LoggerService.LogInformation(typeof(CowTreatmentService), "Deleted cow treatment with ID: {id}.", id);
        }
    }
    
    public int[] GetCowTreatmentChartData(int? year = null)
    {
        var currentYear = year.HasValue ? year.Value :  DateTime.Now.Year;
        var months = Enumerable.Range(1, 12);

        var groupedData = _cachedTreatments.Values
            .Where(obj => obj.AdministrationDate.Year == currentYear) 
            .GroupBy(obj => obj.AdministrationDate.Month)
            .ToDictionary(g => g.Key, g => g.Count());

        return months
            .Select(month => groupedData.ContainsKey(month) ? groupedData[month] : 0)
            .ToArray();
    }

    public int[] GetCowTreatmentMedicineChartData(int medicine, int? year = null)
    {
        var currentYear = DateTime.Now.Year;
        if (year.HasValue)
        {
            currentYear = year.Value;
        }
        
        var months = Enumerable.Range(1, 12);

        return months
            .Select(month => _cachedTreatments.Values
                .Count(obj => obj.AdministrationDate.Year == currentYear && 
                              obj.AdministrationDate.Month == month &&
                              obj.MedicineId == medicine))
            .ToArray();
    }
    
    public async Task<IEnumerable<string>> SearchCowTreatmentMedicaments(string value, CancellationToken token, MedicineService medicineService)
    {
        if (string.IsNullOrEmpty(value))
        {
            return medicineService.GetMedicineNames().Order();
        }
        return medicineService.GetMedicineNames().Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase));
    }

    public async Task<IEnumerable<string>> SearchCowTreatmentWhereHow(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value) || !DistinctWhereHows.Any())
        {
            return _whereHowService.WhereHowNames;
        }
        return _whereHowService.WhereHowNames.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase));
    }

    public int GetMinYear()
    {
        return _cachedTreatments.Values.Min(t => t.AdministrationDate).Year;
    }
}
