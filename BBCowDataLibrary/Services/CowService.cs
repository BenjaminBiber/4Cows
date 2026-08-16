using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services;

public class CowService
{
    // Keyed by the stable Cow_ID (never the ear tag): a calf has no ear tag but always has a Cow_ID.
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
                _cachedCows = cows.ToImmutableDictionary(c => c.CowId);
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
                    _cachedCows = _cachedCows.Add(cow.CowId, cow);
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

        public async Task<bool> RemoveByIdAsync(string cowId)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.Cows
                    .Where(c => c.CowId == cowId)
                    .ExecuteDeleteAsync();

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedCows.ContainsKey(cowId))
                {
                    _cachedCows = _cachedCows.Remove(cowId);
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

        public Cow GetById(string cowId)
        {
            return _cachedCows.ContainsKey(cowId) ? _cachedCows[cowId] : null;
        }

        // Finds a cow by its ear tag FIELD (not the Cow_ID key). Needed because a promoted calf
        // keeps its GUID Cow_ID while gaining an ear tag, so ear-tag lookups can't go through the key.
        public Cow GetByEarTagNumber(string earTagNumber)
        {
            if (string.IsNullOrWhiteSpace(earTagNumber))
            {
                return null;
            }
            return _cachedCows.Values.FirstOrDefault(c => c.EarTagNumber == earTagNumber);
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

        // Resolves a collar number to the cow's stable Cow_ID. Works for calves (no ear tag) too,
        // which is why treatment dialogs use this instead of GetEarTagNumberByCollarNumber.
        public string GetCowIdByCollarNumber(int collarNumber, bool includeGone = false)
        {
            var match = includeGone
                ? _cachedCows.Values.FirstOrDefault(c => c.CollarNumber == collarNumber)
                : _cachedCows.Values.FirstOrDefault(c => c.CollarNumber == collarNumber && !c.IsGone);
            return match?.CowId ?? String.Empty;
        }

        public int GetCollarNumberByCowId(string cowId)
        {
            return !string.IsNullOrEmpty(cowId) && _cachedCows.ContainsKey(cowId) ? _cachedCows[cowId].CollarNumber : int.MinValue;
        }

        // The non-gone calf (no ear tag) that carries this collar number, if any.
        public Cow GetCalfByCollarNumber(int collarNumber)
        {
            return _cachedCows.Values.FirstOrDefault(c => c.IsCalv && !c.IsGone
                && string.IsNullOrWhiteSpace(c.EarTagNumber) && c.CollarNumber == collarNumber);
        }

        public bool IsCollarInUse(int collarNumber)
        {
            return _cachedCows.Values.Any(c => !c.IsGone && c.CollarNumber == collarNumber);
        }

        public async Task<bool> UpdateCollarNumberAsync(string cowId, int newCollarNumber)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.Cows
                    .Where(c => c.CowId == cowId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.CollarNumber, newCollarNumber));

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedCows.ContainsKey(cowId))
                {
                    var updatedCow = _cachedCows[cowId];
                    updatedCow.CollarNumber = newCollarNumber;
                    _cachedCows = _cachedCows.SetItem(cowId, updatedCow);
                    LoggerService.LogInformation(typeof(CowService), "Updated collar number for cow {cowId} to {newCollarNumber}.", cowId, newCollarNumber);
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

        public async Task<bool> UpdateIsGoneAsync(string cowId, bool isGone)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.Cows
                    .Where(c => c.CowId == cowId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.IsGone, isGone));

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedCows.ContainsKey(cowId))
                {
                    var updatedCow = _cachedCows[cowId];
                    updatedCow.IsGone = isGone;
                    _cachedCows = _cachedCows.SetItem(cowId, updatedCow);
                    LoggerService.LogInformation(typeof(CowService), "Updated is gone for cow {cowId} to {isGone}.", cowId, isGone);
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

        // Assigns an ear tag to a calf and clears the calf flag WITHOUT changing the Cow_ID (the PK),
        // so all treatment history that references this Cow_ID stays linked. Shared by the XLink
        // scraper and the manual "Ohrenmarke zuweisen" dialog.
        public async Task<bool> PromoteCalfAsync(string cowId, string earTagNumber)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.Cows
                    .Where(c => c.CowId == cowId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(c => c.EarTagNumber, earTagNumber)
                        .SetProperty(c => c.IsCalv, false));

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess && _cachedCows.ContainsKey(cowId))
                {
                    var updatedCow = _cachedCows[cowId];
                    updatedCow.EarTagNumber = earTagNumber;
                    updatedCow.IsCalv = false;
                    _cachedCows = _cachedCows.SetItem(cowId, updatedCow);
                    LoggerService.LogInformation(typeof(CowService), "Promoted calf {cowId} with ear tag number {earTagNumber}.", cowId, earTagNumber);
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(CowService), "Failed to promote calf, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        // Autocomplete search for the treatment dialogs. Matches on collar number, ear tag OR Cow_ID,
        // and returns Cow_IDs (the value the treatment stores). Excludes cows that left the farm.
        public Task<IEnumerable<string>> SearchCows(string value, CancellationToken token)
        {
            IEnumerable<Cow> query = _cachedCows.Values.Where(c => !c.IsGone);
            if (!string.IsNullOrEmpty(value))
            {
                query = query.Where(c =>
                    (c.EarTagNumber != null && c.EarTagNumber.Contains(value, StringComparison.InvariantCultureIgnoreCase))
                    || c.CollarNumber.ToString().Contains(value)
                    || c.CowId.Contains(value, StringComparison.InvariantCultureIgnoreCase));
            }

            return Task.FromResult(query.OrderBy(c => c.CollarNumber).Select(c => c.CowId));
        }

        // Ear-tag-only display for a Cow_ID: the real ear tag for identified cows, "Kalb" for a
        // calf (or an unknown/removed cow). Used by the "Ohrmarkennummer" table columns so a calf's
        // raw Cow_ID (a GUID) is never shown.
        public string GetEarTagDisplay(string cowId)
        {
            if (!string.IsNullOrEmpty(cowId) && _cachedCows.TryGetValue(cowId, out var cow)
                && !string.IsNullOrWhiteSpace(cow.EarTagNumber))
            {
                return cow.EarTagNumber;
            }
            return "Kalb";
        }

        // Friendly label for a Cow_ID so the raw GUID of a calf is never shown to the user.
        public string GetDisplayLabel(string? cowId)
        {
            if (string.IsNullOrEmpty(cowId) || !_cachedCows.ContainsKey(cowId))
            {
                return cowId ?? string.Empty;
            }

            var cow = _cachedCows[cowId];
            if (cow.IsCalv || string.IsNullOrWhiteSpace(cow.EarTagNumber))
            {
                return $"{cow.CollarNumber} (Kalb)";
            }

            return $"{cow.CollarNumber} / {cow.EarTagNumber}";
        }

        public bool FilterFuncCow(string cowId, string searchString)
        {
            if (!Cows.ContainsKey(cowId))
            {
                return false;
            }
            var cow = Cows[cowId];
            if (string.IsNullOrWhiteSpace(searchString))
                return true;
            var search = searchString.ToLower();
            var collar = cow.CollarNumber.ToString().ToLower();
            var earTag = cow.EarTagNumber?.ToLower() ?? string.Empty;
            if (searchString.Length < 3 && search == collar)
            {
                return true;
            }
            if ((searchString.Length >= 3 && earTag.Contains(search)) || collar == search)
            {
                return true;
            }
            return false;
        }
}
