using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services;

public class CowService
{
    private ImmutableDictionary<string, Cow> _cachedCows = ImmutableDictionary<string, Cow>.Empty;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;

        public ImmutableDictionary<string, Cow> Cows => _cachedCows;

        public CowService(IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
        {
            _contextFactory = contextFactory;
            _databaseStatusService = databaseStatusService;
        }

        public async Task GetAllDataAsync()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var cows = await context.Cows.AsNoTracking().ToListAsync();
                _cachedCows = cows.ToImmutableDictionary(c => c.EarTagNumber);
                _databaseStatusService.ReportSuccess();
                LoggerService.LogInformation(typeof(CowService), $"Loaded {_cachedCows.Count} cows.");
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(CowService), "Failed to load cows, with {@Message}", ex, ex.Message);
            }
        }

        public async Task<bool> InsertDataAsync(Cow cow)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await context.Cows.AddAsync(cow);
                var isSuccess = await context.SaveChangesAsync() > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess)
                {
                    _cachedCows = _cachedCows.Add(cow.EarTagNumber, cow);
                    LoggerService.LogInformation(typeof(CowService), "Inserted cow: {@cow}.", cow);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(CowService), "Failed to insert cow, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public async Task<bool> RemoveByIdAsync(string earTagNumber)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.Cows
                    .Where(c => c.EarTagNumber == earTagNumber)
                    .ExecuteDeleteAsync();

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedCows.ContainsKey(earTagNumber))
                {
                    _cachedCows = _cachedCows.Remove(earTagNumber);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(CowService), "Failed to remove cow, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public Cow GetById(string earTagNumber)
        {
            return _cachedCows.ContainsKey(earTagNumber) ? _cachedCows[earTagNumber] : null;
        }

        public string GetEarTagNumberByCollarNumber(int collarNumber, bool searchContainsLeavage = true)
        {
            if(searchContainsLeavage)
            {
                return _cachedCows.Values.FirstOrDefault(c => c.CollarNumber == collarNumber)?.EarTagNumber ?? String.Empty;
            }
            else
            {
                return _cachedCows.Values.FirstOrDefault(c => c.CollarNumber == collarNumber && !c.IsGone)?.EarTagNumber ?? String.Empty;
            }
        }
        
        public int GetCollarNumberByEarTagNumber(string earTagNumber)
        {
            return _cachedCows.ContainsKey(earTagNumber) ? _cachedCows[earTagNumber].CollarNumber : int.MinValue;
        }
        public async Task<bool> UpdateCollarNumberAsync(string earTagNumber, int newCollarNumber)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.Cows
                    .Where(c => c.EarTagNumber == earTagNumber)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.CollarNumber, newCollarNumber));

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedCows.ContainsKey(earTagNumber))
                {
                    var updatedCow = _cachedCows[earTagNumber];
                    updatedCow.CollarNumber = newCollarNumber;
                    _cachedCows = _cachedCows.SetItem(earTagNumber, updatedCow);
                    LoggerService.LogInformation(typeof(CowService), "Updated collar number for cow with ear tag number {earTagNumber} to {newCollarNumber}.", earTagNumber, newCollarNumber);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(CowService), "Failed to update collar number, with {@Message}", ex, ex.Message);
                return false;
            }
        }
        
        public async Task<bool> UpdateIsGoneAsync(string earTagNumber, bool isGone)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.Cows
                    .Where(c => c.EarTagNumber == earTagNumber)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.IsGone, isGone));

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedCows.ContainsKey(earTagNumber))
                {
                    var updatedCow = _cachedCows[earTagNumber];
                    updatedCow.IsGone = isGone;
                    _cachedCows = _cachedCows.SetItem(earTagNumber, updatedCow);
                    LoggerService.LogInformation(typeof(CowService), "Updated is gone for cow with ear tag number {earTagNumber} to {isGone}.", earTagNumber, isGone);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(CowService), "Failed to update is gone flag, with {@Message}", ex, ex.Message);
                return false;
            }
        }
        
        public async Task<IEnumerable<string>> SearchCowEarTagNumbers(string value, CancellationToken token)
        {
            if (string.IsNullOrEmpty(value) || !Cows.Any())
            {
                return Cows.Keys;
            }
            return Cows.Keys.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase));
        }
        
        
        public bool FilterFuncCow(string earTagNumber, string searchString)
        {
            if (!Cows.ContainsKey(earTagNumber))
            {
                return false;
            }
            var Cow = Cows[earTagNumber];
            if (string.IsNullOrWhiteSpace(searchString))
                return true;
            if (searchString.Length < 3 && searchString.ToLower() == Cow.CollarNumber.ToString().ToLower())
            {
                return true;
            }else if(searchString.Length >= 3 && Cow.EarTagNumber.ToLower().Contains(searchString.ToLower()) || Cow.CollarNumber.ToString().ToLower() == searchString.ToLower())
            {
                return true;
            }
            return false;
        }
}
