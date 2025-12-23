using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services;

public class WhereHowService
{
    private ImmutableDictionary<int, WhereHow> _cachedWhereHows = ImmutableDictionary<int, WhereHow>.Empty;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;
    public ImmutableDictionary<int, WhereHow> WhereHows => _cachedWhereHows;

    public List<string> WhereHowNames => _cachedWhereHows.Values.Select(x => x.WhereHowName).Distinct().ToList();

        public WhereHowService(IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
        {
            _contextFactory = contextFactory;
            _databaseStatusService = databaseStatusService;
        }

        public async Task GetAllDataAsync()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var whereHows = await context.WhereHows.AsNoTracking().ToListAsync();
                _cachedWhereHows = whereHows.ToImmutableDictionary(c => c.WhereHowId);
                _databaseStatusService.ReportSuccess();
                LoggerService.LogInformation(typeof(WhereHowService), $"Loaded {_cachedWhereHows.Count} WhereHows.");
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(WhereHowService), "Failed to load WhereHows, with {@Message}", ex, ex.Message);
            }
        }

        public async Task<bool> InsertDataAsync(WhereHow whereHow)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await context.WhereHows.AddAsync(whereHow);
                var isSuccess = await context.SaveChangesAsync() > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess)
                {
                    await GetAllDataAsync();
                    LoggerService.LogInformation(typeof(WhereHowService), "Inserted WhereHow: {@whereHow}.", whereHow);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(WhereHowService), "Failed to insert WhereHow, with {@Message}", ex, ex.Message);
                return false;
            }
        }


        public async Task<bool> RemoveByIdAsync(int whereHowId)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.WhereHows
                    .Where(w => w.WhereHowId == whereHowId)
                    .ExecuteDeleteAsync();

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedWhereHows.ContainsKey(whereHowId))
                {
                    _cachedWhereHows = _cachedWhereHows.Remove(whereHowId);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(WhereHowService), "Failed to delete WhereHow, with {@Message}", ex, ex.Message);
                return false;
            }
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
