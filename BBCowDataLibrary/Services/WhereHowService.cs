using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;

namespace BB_Cow.Services;

public class WhereHowService
{
    private ImmutableDictionary<int, WhereHow> _cachedWhereHows = ImmutableDictionary<int, WhereHow>.Empty;
    public ImmutableDictionary<int, WhereHow> WhereHows => _cachedWhereHows;

    public List<string> WhereHowNames => _cachedWhereHows.Values.Select(x => x.WhereHowName).Distinct().ToList();

        public async Task GetAllDataAsync()
        {
            var whereHows = await DatabaseService.ReadDataAsync(@"SELECT * FROM WhereHow;", reader =>
            {
                var whereHow = new WhereHow
                {
                    WhereHowId = reader.GetInt32("WhereHow_ID"),
                    WhereHowName = reader.GetString("WhereHow_Name"),
                    ShowDialog = reader.GetBoolean("ShowDialog")
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
                                (`WhereHow_Name`, `ShowDialog`) 
                                VALUES (@WhereHowName, @ShowDialog);";
                command.Parameters.AddWithValue("@WhereHowName", whereHow.WhereHowName);
                command.Parameters.AddWithValue("@ShowDialog", whereHow.ShowDialog);
                isSuccess = (await command.ExecuteNonQueryAsync()) > 0;
            });

            if (isSuccess)
            {
                await GetAllDataAsync();
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
                id = (_cachedWhereHows.Values
                        .FirstOrDefault(x => x.WhereHowName.ToLower().Trim() == name.ToLower().Trim()) ?? new WhereHow())
                    .WhereHowId;
            }

            return id;
        }

        public string GetFullWhereHowName(int whereHow_id,UdderService udderService, int? udder_id = null)
        {
            var whereHow = GetById(whereHow_id);
            if (!udder_id.HasValue)
            {
                return whereHow.WhereHowName;
            }
            else
            {
                var udder = udderService.GetById(udder_id.Value);
                return $"{whereHow.WhereHowName} {GetUdderString(udder)}";
            }
        }

        public string GetUdderString(Udder udder)
        {
            if (udder.QuarterLH && udder.QuarterLV && udder.QuarterRV && udder.QuarterRH)
            {
                return "(Alle 4)";
            }else if (!udder.QuarterLH && !udder.QuarterLV && !udder.QuarterRV && !udder.QuarterRH)
            {
                return "";
            }

            List<string> results = new List<string>();
            results.Add(udder.QuarterLV ? "LV" : "");
            results.Add(udder.QuarterLH ? "LH" : "");
            results.Add(udder.QuarterRV ? "RV" : "");
            results.Add(udder.QuarterRH ? "RH" : "");
            return $"({String.Join("/ ", results.Where(x => !string.IsNullOrEmpty(x)))})";

        }
}