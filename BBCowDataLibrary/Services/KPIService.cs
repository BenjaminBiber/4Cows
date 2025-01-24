using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;

namespace BB_Cow.Services;

public class KPIService
{
    private ImmutableDictionary<int, KPI> _cachedKPIs = ImmutableDictionary<int, KPI>.Empty;

    public ImmutableDictionary<int, KPI> KPIs => _cachedKPIs;

    private Dictionary<string, Action> Methods = new Dictionary<string, Action>
    {
        { "i",  }
    };

    
    public async Task GetAllDataAsync()
    {
        var kpis = await DatabaseService.ReadDataAsync(@"SELECT * FROM KPI;", reader =>
        {
            var kpi = new KPI
            {
                KPIId = reader.GetInt32("KPI_ID"),
                MethodName = reader.GetString("Method_Name"),
                SortOrder = reader.GetInt32("Sort_Order")
            };
            return kpi;
        });

        _cachedKPIs = kpis.ToImmutableDictionary(c => c.KPIId);
        LoggerService.LogInformation(typeof(KPIService), $"Loaded {_cachedKPIs.Count} KPI.");
    }

    public async Task<bool> InsertDataAsync(KPI kpi)
    {
        bool isSuccess = false;
        await DatabaseService.ExecuteQueryAsync(async command =>
        {
            command.CommandText = @"INSERT INTO `KPI` (`KPI_ID`, `Method_Name`, `Sort_Order`) VALUES (@ID, @Method_Name, @Sort_Order);";
            command.Parameters.AddWithValue("@ID", kpi.KPIId);
            command.Parameters.AddWithValue("@Method_Name", kpi.MethodName);
            command.Parameters.AddWithValue("@Sort_Order", kpi.SortOrder);
            isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
        });

        if (isSuccess)
        {
            await GetAllDataAsync();
            LoggerService.LogInformation(typeof(CowService), "Inserted cow: {@kpi}.", kpi);
        }

        return isSuccess;
    }
}