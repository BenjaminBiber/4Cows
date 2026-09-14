using System.Collections.Immutable;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Data.Services;

public class CowService : ICowService
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
                    // SetItem statt Add. Hier ist Add ungefaehrlich, weil Cow_ID ein vom
                    // Client vergebener String ist (Cow.CreateCalf wuerfelt eine GUID) und
                    // nicht von der Datenbank kommt. Einheitlich trotzdem, damit beim
                    // naechsten Cache-Schreiber niemand erst pruefen muss, welche der
                    // beiden Varianten hier warum steht.
                    _cachedCows = _cachedCows.SetItem(cow.CowId, cow);
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

        // Ab hier nur noch Weiterleitungen: die Rumpfe stehen in CowLookups,
        // damit es jede dieser Regeln genau einmal gibt - der EF-Dienst und der
        // spaetere HTTP-Dienst koennen sich sonst darueber uneinig werden, was
        // "Kalb" heisst. Die erklaerenden Kommentare sind mit den Rumpfen
        // umgezogen.
        public Cow GetById(string cowId) => CowLookups.GetById(_cachedCows, cowId);

        public Cow GetByEarTagNumber(string earTagNumber) => CowLookups.GetByEarTagNumber(_cachedCows, earTagNumber);

        public string GetEarTagNumberByCollarNumber(int collarNumber, bool searchContainsLeavage = true)
            => CowLookups.GetEarTagNumberByCollarNumber(_cachedCows, collarNumber, searchContainsLeavage);

        public string GetCowIdByCollarNumber(int collarNumber, bool includeGone = false)
            => CowLookups.GetCowIdByCollarNumber(_cachedCows, collarNumber, includeGone);

        public int GetCollarNumberByCowId(string cowId) => CowLookups.GetCollarNumberByCowId(_cachedCows, cowId);

        public Cow GetCalfByCollarNumber(int collarNumber) => CowLookups.GetCalfByCollarNumber(_cachedCows, collarNumber);

        public bool IsCollarInUse(int collarNumber) => CowLookups.IsCollarInUse(_cachedCows, collarNumber);

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

        // Task.FromResult bleibt hier, nicht in CowLookups: die Rechnung ist
        // synchron, aber die Signatur dieser Methode darf sich nicht aendern.
        public Task<IEnumerable<string>> SearchCows(string value, CancellationToken token)
            => Task.FromResult(CowLookups.SearchCows(_cachedCows, value));

        public string GetEarTagDisplay(string cowId) => CowLookups.GetEarTagDisplay(_cachedCows, cowId);

        public string GetDisplayLabel(string? cowId) => CowLookups.GetDisplayLabel(_cachedCows, cowId);

        public bool FilterFuncCow(string cowId, string searchString) => CowLookups.FilterFuncCow(_cachedCows, cowId, searchString);
}
