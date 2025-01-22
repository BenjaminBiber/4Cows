using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;

namespace BB_Cow.Services;

public class UdderService
{
    private ImmutableDictionary<int, Udder> _cachedUdder = ImmutableDictionary<int, Udder>.Empty;
    public ImmutableDictionary<int, Udder> Udder => _cachedUdder;

    public async Task GetAllDataAsync()
    {
        var Udder = await DatabaseService.ReadDataAsync(@"SELECT * FROM Udder;", reader =>
        {
            var whereHow = new Udder
            {
                UdderId = reader.GetInt32("UDDER_ID"),
                QuarterLV = reader.GetBoolean("Quarter_LV"),
                QuarterLH = reader.GetBoolean("Quarter_LH"),
                QuarterRV = reader.GetBoolean("Quarter_RV"),
                QuarterRH = reader.GetBoolean("Quarter_RH"),
            };
            return whereHow;
        });

        _cachedUdder = Udder.ToImmutableDictionary(c => c.UdderId);
        LoggerService.LogInformation(typeof(CowService), $"Loaded {_cachedUdder.Count} Udder.");
    }

    public async Task<bool> InsertDataAsync(Udder udder)
    {
        bool isSuccess = false;
        await DatabaseService.ExecuteQueryAsync(async command =>
        {
            command.CommandText = @"INSERT INTO `Udder` (`Quarter_LV`, `Quarter_LH`, `Quarter_RV`, `Quarter_RH`)  VALUES (@LV, @LH, @RV, @RH);";
            command.Parameters.AddWithValue("@RV", udder.QuarterRV);
            command.Parameters.AddWithValue("@RH", udder.QuarterRH);
            command.Parameters.AddWithValue("@LH", udder.QuarterLH);
            command.Parameters.AddWithValue("@LV", udder.QuarterLV);
            isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
        });

        if (isSuccess)
        {
            await GetAllDataAsync();
            LoggerService.LogInformation(typeof(UdderService), "Inserted Udder: {@udder}.", udder);
        }

        return isSuccess;
    }

    public async Task<int> GetIDByBools(Udder emptyUdder)
    {
        var id = _cachedUdder.Values.FirstOrDefault(x =>
            x.QuarterLV == emptyUdder.QuarterLV && x.QuarterRV == emptyUdder.QuarterRV &&
            x.QuarterLH == emptyUdder.QuarterLH && x.QuarterRH == emptyUdder.QuarterRH) ?? null;

        if (id == null)
        {
            await InsertDataAsync(emptyUdder);
            return (_cachedUdder.Values.FirstOrDefault(x =>
                x.QuarterLV == emptyUdder.QuarterLV && x.QuarterRV == emptyUdder.QuarterRV &&
                x.QuarterLH == emptyUdder.QuarterLH && x.QuarterRH == emptyUdder.QuarterRH) ?? new Udder()).UdderId;
        }
        else
        {
            return id.UdderId;
        }
    }
    
}