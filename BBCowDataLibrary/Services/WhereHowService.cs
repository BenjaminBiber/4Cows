using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;

namespace BB_Cow.Services;

public class WhereHowService
{
    private ImmutableDictionary<int, WhereHow> _cachedWhereHows = ImmutableDictionary<int, WhereHow>.Empty;
    public ImmutableDictionary<int, WhereHow> WhereHows => _cachedWhereHows;

        public async Task GetAllDataAsync()
        {
            var whereHows = await DatabaseService.ReadDataAsync(@"SELECT * FROM WhereHow;", reader =>
            {
                var whereHow = new WhereHow
                {
                    WhereHowId = reader.GetInt32("WhereHow_ID"),
                    WhereHowName = reader.GetString("WhereHow_Name"),
                    ShowDialog = reader.GetBoolean("ShowDialog"),
                    QuarterLV = reader.GetBoolean("Quarter_LV"),
                    QuarterLH = reader.GetBoolean("Quarter_LH"),
                    QuarterRV = reader.GetBoolean("Quarter_RV"),
                    QuarterRH = reader.GetBoolean("Quarter_RH"),

                };
                return whereHow;
            });

            _cachedWhereHows = whereHows.ToImmutableDictionary(c => c.WhereHowId);
            LoggerService.LogInformation(typeof(CowService), $"Loaded {_cachedWhereHows.Count} WhereHows.");
        }

        public async Task<bool> InsertDataAsync(WhereHow whereHow)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"INSERT INTO `WhereHow` 
                                (`WhereHow_Name`, `ShowDialog`, `Quarter_LV`, `Quarter_LH`, `Quarter_RV`, `Quarter_RH`) 
                                VALUES (@WhereHowName, @ShowDialog, @QuarterLV, @QuarterLH, @QuarterRV, @QuarterRH);";
                command.Parameters.AddWithValue("@WhereHowName", whereHow.WhereHowName);
                command.Parameters.AddWithValue("@ShowDialog", whereHow.ShowDialog);
                command.Parameters.AddWithValue("@QuarterLV", whereHow.QuarterLV);
                command.Parameters.AddWithValue("@QuarterLH", whereHow.QuarterLH);
                command.Parameters.AddWithValue("@QuarterRV", whereHow.QuarterRV);
                command.Parameters.AddWithValue("@QuarterRH", whereHow.QuarterRH);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess)
            {
                _cachedWhereHows = _cachedWhereHows.Add(whereHow.WhereHowId, whereHow);
                LoggerService.LogInformation(typeof(WhereHowService), "Inserted WhereHow: {@whereHow}.", whereHow);
            }

            return isSuccess;
        }


        public async Task<bool> RemoveByIdAsync(int whereHowId)
        {
            bool isSuccess = false;
            await DatabaseService.ExecuteQueryAsync(async command =>
            {
                command.CommandText = @"DELETE FROM `WhereHow` WHERE `WhereHow_ID` = @ID;";
                command.Parameters.AddWithValue("@ID", whereHowId);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess && _cachedWhereHows.ContainsKey(whereHowId))
            {
                _cachedWhereHows = _cachedWhereHows.Remove(whereHowId);
            }

            return isSuccess;
        }

        public WhereHow GetById(int whereHowID)
        {
            return _cachedWhereHows.ContainsKey(whereHowID) ? _cachedWhereHows[whereHowID] : new WhereHow();
        }

        public string GetWhereHowNameById(int id)
        {
            return _cachedWhereHows.ContainsKey(id) ? _cachedWhereHows[id].WhereHowName : String.Empty;
        }
        
        public List<string> GetWhereHowNamesByIds(List<int> Ids)
        {
            var returnList = new List<string>();

            foreach (var id in Ids)
            {
                var returnId = GetWhereHowNameById(id);
                if (!string.IsNullOrEmpty(returnId) && !returnList.Contains(returnId))
                {
                    returnList.Add(returnId);
                }
            }

            return returnList;
        }

        public async Task<int> GetWhereHowIDByName(string name)
        {
            var id =  _cachedWhereHows.Values.Any(x => x.WhereHowName.ToLower().Trim() == name.ToLower().Trim())
                ? _cachedWhereHows.Values.FirstOrDefault(x => x.WhereHowName.ToLower().Trim() == name.ToLower().Trim())
                    .WhereHowId
                : int.MinValue;

            if (id == int.MinValue)
            {
                var newWhereHow = new WhereHow()
                {
                    WhereHowName = name
                };
                await InsertDataAsync(newWhereHow);
                id = _cachedWhereHows.Values
                    .FirstOrDefault(x => x.WhereHowName.ToLower().Trim() == name.ToLower().Trim())
                    .WhereHowId;
            }

            return id;
        }
}