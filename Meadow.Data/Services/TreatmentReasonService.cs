using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services;

/// <summary>
/// Behandlungsgruende. Aufgebaut wie <see cref="WhereHowService"/>: Singleton
/// mit prozessweitem Cache, Eintraege entstehen ueberwiegend nebenbei im
/// Behandlungs-Dialog, gepflegt wird ueber die Basisdaten-Seite.
/// </summary>
public class TreatmentReasonService
{
    /// <summary>
    /// Anzeigetext, wenn kein Grund gesetzt oder die ID unbekannt ist. Steht
    /// hier und nicht in den Tabellen, damit beide dasselbe Zeichen zeigen.
    /// </summary>
    public const string NoReasonText = "–";

    private ImmutableDictionary<int, TreatmentReason> _cachedReasons =
        ImmutableDictionary<int, TreatmentReason>.Empty;

    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;

    public ImmutableDictionary<int, TreatmentReason> Reasons => _cachedReasons;

    public List<string> ReasonNames =>
        _cachedReasons.Values.Select(r => r.TreatmentReasonName).Distinct().ToList();

    public TreatmentReasonService(
        IDbContextFactory<DatabaseContext> contextFactory,
        DatabaseStatusService databaseStatusService)
    {
        _contextFactory = contextFactory;
        _databaseStatusService = databaseStatusService;
    }

    public async Task GetAllDataAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var reasons = await context.TreatmentReasons.AsNoTracking().ToListAsync();
            _cachedReasons = reasons.ToImmutableDictionary(r => r.TreatmentReasonId);
            _databaseStatusService.ReportSuccess();
            LoggerService.LogInformation(typeof(TreatmentReasonService),
                $"Loaded {_cachedReasons.Count} treatment reasons.");
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(TreatmentReasonService),
                "Failed to load treatment reasons, with {@Message}", ex, ex.Message);
        }
    }

    /// <summary>
    /// Wie oft jeder Grund in Cow_Treatment und Planned_Cow_Treatment benutzt
    /// wird. Steuert auf der Pflegeseite, ob geloescht werden darf, und wird
    /// deshalb bewusst NICHT aus dem Cache gezaehlt: der ist ein Singleton und
    /// kann aelter sein als die Datenbank.
    ///
    /// Null heisst "konnte nicht gezaehlt werden". Ein leeres Dictionary waere
    /// hier gefaehrlich - es saehe aus wie "nirgends benutzt" und gaebe den
    /// Papierkorb fuer jede Zeile frei.
    /// </summary>
    public async Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var treated = await context.CowTreatments
                .Where(t => t.TreatmentReasonId != null)
                .GroupBy(t => t.TreatmentReasonId!.Value)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync();

            var planned = await context.PlannedCowTreatments
                .Where(t => t.TreatmentReasonId != null)
                .GroupBy(t => t.TreatmentReasonId!.Value)
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
            LoggerService.LogError(typeof(TreatmentReasonService),
                "Failed to count treatment reason usages, with {@Message}", ex, ex.Message);
            return null;
        }
    }

    public async Task<bool> InsertDataAsync(TreatmentReason reason)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.TreatmentReasons.AddAsync(reason);
            var isSuccess = await context.SaveChangesAsync() > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(TreatmentReasonService),
                    $"Inserted treatment reason {reason.TreatmentReasonName}.");
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(TreatmentReasonService),
                "Failed to insert treatment reason, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    public async Task<bool> UpdateDataAsync(TreatmentReason reason)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var affectedRows = await context.TreatmentReasons
                .Where(r => r.TreatmentReasonId == reason.TreatmentReasonId)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(r => r.TreatmentReasonName, reason.TreatmentReasonName));

            var isSuccess = affectedRows > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(TreatmentReasonService),
                    $"Updated treatment reason {reason.TreatmentReasonName}.");
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(TreatmentReasonService),
                "Failed to update treatment reason, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Haengt alle Behandlungen von <paramref name="sourceId"/> auf
    /// <paramref name="targetId"/> um und loescht die Quelle. Der Weg, um einen
    /// Tippfehler-Eintrag loszuwerden, der schon benutzt wird und deshalb nicht
    /// loeschbar ist.
    /// </summary>
    public async Task<bool> MergeAsync(int sourceId, int targetId)
    {
        // Ohne diesen Guard wuerde ein Merge auf sich selbst erst umhaengen und
        // dann genau das Ziel loeschen. Erreichbar ueber eine reine
        // Gross-/Kleinschreibungsaenderung ("mastitis" -> "Mastitis"), weil der
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
                .Where(t => t.TreatmentReasonId == sourceId)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(t => t.TreatmentReasonId, targetId));

            await context.PlannedCowTreatments
                .Where(t => t.TreatmentReasonId == sourceId)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(t => t.TreatmentReasonId, targetId));

            await context.TreatmentReasons
                .Where(r => r.TreatmentReasonId == sourceId)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();
            _databaseStatusService.ReportSuccess();

            await GetAllDataAsync();
            LoggerService.LogInformation(typeof(TreatmentReasonService),
                "Merged treatment reason {@Source} into {@Target}.", sourceId, targetId);

            return true;
        }
        catch (Exception ex)
        {
            // Die Transaktion wird beim Dispose zurueckgerollt - es bleibt
            // nichts halb umgehaengt liegen.
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(TreatmentReasonService),
                "Failed to merge treatment reason, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    public async Task<bool> RemoveByIdAsync(int reasonId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            // Der Zaehler auf der Pflegeseite stammt aus dem Seitenaufbau, das
            // Loeschen passiert danach. Fremdschluessel gibt es im Schema
            // keine, die Datenbank haelt hier also nichts auf - deshalb zaehlt
            // der Service unmittelbar vor dem Delete selbst nach.
            var inUse = await context.CowTreatments.AnyAsync(t => t.TreatmentReasonId == reasonId)
                        || await context.PlannedCowTreatments.AnyAsync(t => t.TreatmentReasonId == reasonId);

            if (inUse)
            {
                await transaction.RollbackAsync();
                LoggerService.LogInformation(typeof(TreatmentReasonService),
                    $"Refused to delete treatment reason {reasonId}: still referenced by treatments.");
                return false;
            }

            var affectedRows = await context.TreatmentReasons
                .Where(r => r.TreatmentReasonId == reasonId)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();

            var isSuccess = affectedRows > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(TreatmentReasonService),
                    $"Deleted treatment reason {reasonId}.");
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(TreatmentReasonService),
                "Failed to delete treatment reason, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    /// <summary>Anzeigename. NoReasonText bei null und bei unbekannter ID.</summary>
    public string GetNameById(int? id)
    {
        if (id is not int value)
        {
            return NoReasonText;
        }

        return _cachedReasons.TryGetValue(value, out var reason)
            ? reason.TreatmentReasonName
            : NoReasonText;
    }

    /// <summary>
    /// Sucht den Grund zum Namen und legt ihn an, wenn es ihn nicht gibt.
    /// Liefert int.MinValue, wenn nichts uebrig bleibt - der Aufrufer bricht
    /// dann ab, BEVOR er die erste Behandlung schreibt.
    ///
    /// Vergleich und Speicherung getrimmt: sonst landet "Mastitis " als
    /// eigene, optisch nicht unterscheidbare Zeile in der Liste.
    /// </summary>
    public async Task<int> GetIdByNameAsync(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return int.MinValue;
        }

        var existing = _cachedReasons.Values.FirstOrDefault(r =>
            string.Equals(r.TreatmentReasonName.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return existing.TreatmentReasonId;
        }

        if (!await InsertDataAsync(new TreatmentReason(0, trimmed)))
        {
            return int.MinValue;
        }

        // InsertDataAsync hat den Cache neu geladen.
        return _cachedReasons.Values.FirstOrDefault(r =>
            string.Equals(r.TreatmentReasonName.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            ?.TreatmentReasonId ?? int.MinValue;
    }

    /// <summary>
    /// Vorschlaege fuer das Autocomplete. Nach dem Muster von
    /// CowTreatmentService.SearchCowTreatmentWhereHow: eine Eingabe ohne
    /// Treffer liefert die Eingabe selbst zurueck, damit sie uebernommen und
    /// beim Speichern angelegt werden kann.
    /// </summary>
    public Task<IEnumerable<string>> SearchAsync(string value, CancellationToken token)
    {
        var names = ReasonNames.OrderBy(n => n, StringComparer.CurrentCulture).ToList();

        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.FromResult<IEnumerable<string>>(names);
        }

        var hits = names
            .Where(n => n.Contains(value, StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        return Task.FromResult<IEnumerable<string>>(
            hits.Count > 0 ? hits : new List<string> { value.Trim() });
    }
}
