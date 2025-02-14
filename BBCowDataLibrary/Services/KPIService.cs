using System.Collections.Immutable;
using System.Data.Common;
using BB_Cow.Class;
using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using MySqlConnector;
using OpenQA.Selenium;

namespace BB_KPI.Services;

public class KPIService
{
    private ImmutableDictionary<int, KPI> _cachedKPIs = ImmutableDictionary<int, KPI>.Empty;

    public ImmutableDictionary<int, KPI> KPIs => _cachedKPIs;

    public async Task GetAllDataAsync()
    {
        var KPIs = await DatabaseService.ReadDataAsync(@"SELECT * FROM KPI;", reader =>
        {
            var KPI = new KPI
            {
                KPIId = reader.GetInt32("KPI_ID"),
                Title = reader.GetString("Title"),
                Url = reader.GetString("Url"),
                Script = reader.GetString("Script"),
                SortOrder = reader.GetInt32("Sort_Order")
            };
            return KPI;
        });

        _cachedKPIs = KPIs.ToImmutableDictionary(c => c.KPIId);
        LoggerService.LogInformation(typeof(KPIService), $"Loaded {_cachedKPIs.Count} KPIs.");
    }

    public async Task<bool> InsertDataAsync(KPI KPI)
    {
        bool isSuccess = false;

        await DatabaseService.ExecuteQueryAsync(async command =>
        {
            command.CommandText = @"
            INSERT INTO `KPI` (`Title`, `Url`, `Script`, `Sort_Order`) 
            VALUES (@Title, @Url, @Script, @SortOrder);";
            command.Parameters.AddWithValue("@Title", KPI.Title);
            command.Parameters.AddWithValue("@Url", KPI.Url);
            command.Parameters.AddWithValue("@Script", KPI.Script);
            command.Parameters.AddWithValue("@SortOrder", KPI.SortOrder);

            isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
        });

        if (isSuccess)
        {
            _cachedKPIs = _cachedKPIs.Add(KPI.KPIId, KPI);
            LoggerService.LogInformation(typeof(KPIService), "Inserted KPI: {@KPI}.", KPI);
        }

        return isSuccess;
    }

    public async Task<string> GetKPIValue(KPI kpi, bool throwError = false)
    {
        bool isSuccess = false;
        var result = new List<string>();
        try
        {
            result = await DatabaseService.ReadDataAsync(kpi.Script, reader => { return reader.GetString("value"); });
            if (!result.Any() && result.FirstOrDefault() == null)
            {
                LoggerService.LogError(typeof(KPIService), "KPI didnt return Value", new NullReferenceException());
                if (throwError)
                {
                    throw new Exception("KPI didnt return Value");
                }
                return "--";
            }
        }
        catch (MySqlException e)
        {
            if (throwError)
            {
                throw (e);
            }
            else
            {
                LoggerService.LogError(typeof(KPIService), "Error while getting KPI-Value", e);
                return "--"; 
            }
        }
        catch (Exception e)
        {
            if (throwError)
            {
                throw (e);
            }
            else
            {
                LoggerService.LogError(typeof(KPIService), "Error while getting KPI-Value", e);
                return "--"; 
            }
        }

        return result.FirstOrDefault();
    }

    public async Task<Dictionary<KPI, string>> GetAllKPIs(bool addButtonKPI = true)
    {
        var result = new Dictionary<KPI, string>();
        await GetAllDataAsync();
        foreach (var kpi in KPIs.Values)
        {
            var item = await GetKPIValue(kpi);
            result.Add(kpi, item);
        }

        if (result.Count < 8 && addButtonKPI)
        {
            var newKPI = new KPI(int.MinValue, "KPI hinzufügen", "/Test", "select '---' as value", 8 );
            result.Add(newKPI, "+");
        }
        return result;
    }
    
    public async Task<bool> UpdateDataAsync(KPI KPI)
    {
        bool isSuccess = false;

        await DatabaseService.ExecuteQueryAsync(async command =>
        {
            command.CommandText = @"
        UPDATE `KPI` 
        SET `Title` = @Title, 
            `Url` = @Url, 
            `Script` = @Script, 
            `Sort_Order` = @SortOrder
        WHERE `KPI_ID` = @KPIId;";
        
            command.Parameters.AddWithValue("@KPIId", KPI.KPIId);
            command.Parameters.AddWithValue("@Title", KPI.Title);
            command.Parameters.AddWithValue("@Url", KPI.Url);
            command.Parameters.AddWithValue("@Script", KPI.Script);
            command.Parameters.AddWithValue("@SortOrder", KPI.SortOrder);

            isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
        });

        if (isSuccess)
        {
            await GetAllDataAsync();
            LoggerService.LogInformation(typeof(KPIService), "Updated KPI: {@KPI}.", KPI);
        }

        return isSuccess;
    }

}