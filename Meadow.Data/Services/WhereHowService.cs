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

        /// <summary>
        /// Wie oft jeder Eintrag in Cow_Treatment und Planned_Cow_Treatment
        /// benutzt wird. Steuert auf der Pflegeseite, ob geloescht werden darf,
        /// und wird deshalb bewusst NICHT aus den Service-Caches gezaehlt: die
        /// sind Singletons und koennen aelter sein als die Datenbank.
        ///
        /// Null heisst "konnte nicht gezaehlt werden". Ein leeres Dictionary
        /// waere hier gefaehrlich - es saehe aus wie "nirgends benutzt" und
        /// gaebe den Papierkorb fuer jede Zeile frei.
        /// </summary>
        public async Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();

                var treated = await context.CowTreatments
                    .GroupBy(t => t.WhereHowId)
                    .Select(g => new { Id = g.Key, Count = g.Count() })
                    .ToListAsync();

                var planned = await context.PlannedCowTreatments
                    .GroupBy(t => t.WhereHowId)
                    .Select(g => new { Id = g.Key, Count = g.Count() })
                    .ToListAsync();

                var counts = new Dictionary<int, int>();
                foreach (var row in treated.Concat(planned))
                {
                    counts[row.Id] = counts.GetValueOrDefault(row.Id) + row.Count;
                }

                _databaseStatusService.ReportSuccess();
                return counts.ToImmutableDictionary();
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(WhereHowService), "Failed to count WhereHow usages, with {@Message}", ex, ex.Message);
                return null;
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


        public async Task<bool> UpdateDataAsync(WhereHow whereHow)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var affectedRows = await context.WhereHows
                    .Where(w => w.WhereHowId == whereHow.WhereHowId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(w => w.WhereHowName, whereHow.WhereHowName)
                        .SetProperty(w => w.ShowDialog, whereHow.ShowDialog));

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess)
                {
                    await GetAllDataAsync();
                    LoggerService.LogInformation(typeof(WhereHowService), $"Updated WhereHow {whereHow.WhereHowName}.");
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(WhereHowService), "Failed to update WhereHow, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Haengt alle Behandlungen von <paramref name="sourceId"/> auf
        /// <paramref name="targetId"/> um und loescht die Quelle. Der Weg, um
        /// einen Tippfehler-Eintrag loszuwerden, der schon benutzt wird und
        /// deshalb nicht loeschbar ist.
        /// </summary>
        public async Task<bool> MergeAsync(int sourceId, int targetId)
        {
            // Ohne diesen Guard wuerde ein Merge auf sich selbst erst umhaengen
            // und dann genau das Ziel loeschen. Erreichbar ueber eine reine
            // Gross-/Kleinschreibungsaenderung ("oral" -> "Oral"), weil der
            // Namensvergleich case-insensitiv ist.
            if (sourceId == targetId)
            {
                return false;
            }

            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await using var transaction = await context.Database.BeginTransactionAsync();

                await context.CowTreatments
                    .Where(t => t.WhereHowId == sourceId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.WhereHowId, targetId));

                await context.PlannedCowTreatments
                    .Where(t => t.WhereHowId == sourceId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.WhereHowId, targetId));

                await context.WhereHows
                    .Where(w => w.WhereHowId == sourceId)
                    .ExecuteDeleteAsync();

                await transaction.CommitAsync();
                _databaseStatusService.ReportSuccess();

                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(WhereHowService),
                    "Merged WhereHow {@Source} into {@Target}.", sourceId, targetId);

                return true;
            }
            catch (Exception ex)
            {
                // Die Transaktion wird beim Dispose zurueckgerollt - es bleibt
                // nichts halb umgehaengt liegen.
                _databaseStatusService.ReportFailure();
                LoggerService.LogError(typeof(WhereHowService), "Failed to merge WhereHow, with {@Message}", ex, ex.Message);
                return false;
            }
        }

        public async Task<bool> RemoveByIdAsync(int whereHowId)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await using var transaction = await context.Database.BeginTransactionAsync();

                // Der Zaehler auf der Pflegeseite stammt aus dem Seitenaufbau,
                // das Loeschen passiert danach. Fremdschluessel gibt es laut
                // InitialCreate keine, die Datenbank haelt hier also nichts auf.
                // Deshalb zaehlt der Service unmittelbar vor dem Delete selbst
                // nach, statt dem Aufrufer zu glauben.
                var inUse = await context.CowTreatments.AnyAsync(t => t.WhereHowId == whereHowId)
                            || await context.PlannedCowTreatments.AnyAsync(t => t.WhereHowId == whereHowId);

                if (inUse)
                {
                    await transaction.RollbackAsync();
                    LoggerService.LogInformation(typeof(WhereHowService),
                        $"Refused to delete WhereHow {whereHowId}: still referenced by treatments.");
                    return false;
                }

                var affectedRows = await context.WhereHows
                    .Where(w => w.WhereHowId == whereHowId)
                    .ExecuteDeleteAsync();

                await transaction.CommitAsync();

                var isSuccess = affectedRows > 0;
                _databaseStatusService.ReportSuccess();

                if (isSuccess)
                {
                    // Vorher wurde der Cache hier von Hand gepflegt, waehrend
                    // Insert neu laedt. Ein Weg reicht.
                    await GetAllDataAsync();
                    LoggerService.LogInformation(typeof(WhereHowService), $"Deleted WhereHow {whereHowId}.");
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

        /// <summary>
        /// Sucht den Eintrag zum Namen und legt ihn an, wenn es ihn nicht gibt.
        ///
        /// <paramref name="showDialog"/> traegt den Toggle-Wert aus dem
        /// Behandlungs-Dialog. Vorher fehlte der Parameter: der implizite
        /// Anlagepfad (speichern ohne Klick auf das Plus) nahm immer den
        /// Konstruktor-Default true und legte damit einen Eintrag an, dessen
        /// Viertel-Verhalten dem widersprach, was im Formular zu sehen war.
        /// </summary>
        public async Task<int> GetWhereHowIDByName(string name, bool showDialog = true)
        {
            var id =  _cachedWhereHows.Values.Any(x => x.WhereHowName.ToLower().Trim() == name.ToLower().Trim())
                ? _cachedWhereHows.Values.FirstOrDefault(x => x.WhereHowName.ToLower().Trim() == name.ToLower().Trim())
                    .WhereHowId
                : int.MinValue;

            if (id == int.MinValue)
            {
                var newWhereHow = new WhereHow()
                {
                    // Getrimmt gespeichert, weil der Vergleich oben ohnehin
                    // trimmt: sonst landet "IZ " als eigene Zeile in der Liste,
                    // die von "IZ" nicht zu unterscheiden ist.
                    WhereHowName = name.Trim(),
                    ShowDialog = showDialog
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
