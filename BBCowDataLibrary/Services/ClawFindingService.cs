using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services;

/// <summary>
/// Klauenbefunde. Aufgebaut wie <see cref="TreatmentReasonService"/>, mit dem
/// vollstaendigen Merge aus <see cref="MedicineService"/>: Singleton mit
/// prozessweitem Cache, Eintraege entstehen ueberwiegend nebenbei im
/// Klauenbehandlungs-Dialog, gepflegt wird ueber die Basisdaten-Seite.
///
/// Bis zur Migration AddClawFinding standen die Befunde als Freitext in vier
/// varchar(32)-Spalten auf Claw_Treatment, und die Vorschlagsliste entstand aus
/// dem Bestand (ClawTreatmentService.ClawFindingList). Diese Tabelle ersetzt
/// beides.
/// </summary>
public class ClawFindingService
{
    /// <summary>
    /// Ergebnis von <see cref="GetIdByNameAsync"/>, wenn der Befund nicht
    /// angelegt werden konnte. <c>null</c> ist dort KEIN Fehler, sondern der
    /// regulaere Fall "keine Eingabe" - deshalb braucht das Scheitern einen
    /// eigenen Wert, wie in TreatmentReasonService.
    /// </summary>
    public const int FailedId = int.MinValue;

    private ImmutableDictionary<int, ClawFinding> _cachedFindings =
        ImmutableDictionary<int, ClawFinding>.Empty;

    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;

    public ImmutableDictionary<int, ClawFinding> Findings => _cachedFindings;

    public List<string> FindingNames =>
        _cachedFindings.Values.Select(f => f.ClawFindingName).Distinct().ToList();

    public ClawFindingService(
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
            var findings = await context.ClawFindings.AsNoTracking().ToListAsync();
            _cachedFindings = findings.ToImmutableDictionary(f => f.ClawFindingId);
            _databaseStatusService.ReportSuccess();
            LoggerService.LogInformation(typeof(ClawFindingService),
                $"Loaded {_cachedFindings.Count} claw findings.");
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(ClawFindingService),
                "Failed to load claw findings, with {@Message}", ex, ex.Message);
        }
    }

    /// <summary>
    /// Anzeigename. Leerstring bei <c>null</c> UND bei unbekannter ID - nicht
    /// der Gedankenstrich der uebrigen Nachschlagedienste.
    ///
    /// Leer heisst in der ganzen Klauen-Anzeige "an dieser Klaue nichts
    /// erfasst": ClawFindingSummary, CowProfileBuilder und ClawSummary haengen
    /// daran. Ein Platzhalterzeichen hier wuerde als echter Befund gezaehlt und
    /// stuende als haeufigster Klauenbefund auf der Kachel.
    /// </summary>
    public string GetNameById(int? id)
    {
        if (id is not int value)
        {
            return string.Empty;
        }

        return _cachedFindings.TryGetValue(value, out var finding)
            ? finding.ClawFindingName
            : string.Empty;
    }

    /// <summary>
    /// Wie viele BEHANDLUNGEN jeden Befund benutzen. Derselbe Befund an zwei
    /// Klauen derselben Behandlung zaehlt einmal - die Basisdaten-Seite sagt
    /// "wird in 3 Behandlungen verwendet", und der Merge haengt genauso viele
    /// Zeilen um.
    ///
    /// Steuert auf der Pflegeseite, ob geloescht werden darf, und wird deshalb
    /// bewusst NICHT aus dem Cache gezaehlt: der ist ein Singleton und kann
    /// aelter sein als die Datenbank.
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

            // Ein GROUP BY je Spalte liesse sich serverseitig zaehlen, wuerde
            // aber eine Behandlung mit demselben Befund an zwei Klauen doppelt
            // zaehlen. Deshalb nur die vier Spalten holen und je Zeile
            // eindeutig machen.
            var rows = await context.ClawTreatments
                .AsNoTracking()
                .Select(t => new
                {
                    t.ClawFindingLVId,
                    t.ClawFindingRVId,
                    t.ClawFindingLHId,
                    t.ClawFindingRHId
                })
                .ToListAsync();

            var counts = new Dictionary<int, int>();
            foreach (var row in rows)
            {
                var seen = new HashSet<int>();
                var ids = new[]
                {
                    row.ClawFindingLVId, row.ClawFindingRVId,
                    row.ClawFindingLHId, row.ClawFindingRHId
                };

                foreach (var id in ids)
                {
                    if (id is int value && seen.Add(value))
                    {
                        counts[value] = counts.GetValueOrDefault(value) + 1;
                    }
                }
            }

            _databaseStatusService.ReportSuccess();
            return counts.ToImmutableDictionary();
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(ClawFindingService),
                "Failed to count claw finding usages, with {@Message}", ex, ex.Message);
            return null;
        }
    }

    public async Task<bool> InsertDataAsync(ClawFinding finding)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.ClawFindings.AddAsync(finding);
            var isSuccess = await context.SaveChangesAsync() > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(ClawFindingService),
                    $"Inserted claw finding {finding.ClawFindingName}.");
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(ClawFindingService),
                "Failed to insert claw finding, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    public async Task<bool> UpdateDataAsync(ClawFinding finding)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var affectedRows = await context.ClawFindings
                .Where(f => f.ClawFindingId == finding.ClawFindingId)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(f => f.ClawFindingName, finding.ClawFindingName));

            var isSuccess = affectedRows > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(ClawFindingService),
                    $"Updated claw finding {finding.ClawFindingName}.");
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(ClawFindingService),
                "Failed to update claw finding, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Haengt alle Klauen von <paramref name="sourceId"/> auf
    /// <paramref name="targetId"/> um und loescht die Quelle. Der Weg, um einen
    /// Tippfehler-Eintrag loszuwerden, der schon benutzt wird und deshalb nicht
    /// loeschbar ist.
    ///
    /// <paramref name="survivingName"/> setzt den Namen des Ziels - damit kann
    /// im Zusammenfuehren-Dialog die Schreibweise der Quelle gewinnen.
    /// </summary>
    public async Task<bool> MergeAsync(int sourceId, int targetId, string? survivingName = null)
    {
        // Ohne diesen Guard wuerde ein Merge auf sich selbst erst umhaengen und
        // dann genau das Ziel loeschen. Erreichbar ueber eine reine
        // Gross-/Kleinschreibungsaenderung ("mortellaro" -> "Mortellaro"), weil
        // der Namensvergleich case-insensitiv ist.
        if (sourceId == targetId)
        {
            return false;
        }

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            // Die Quelle muss es noch geben - sonst haengt der Merge nichts um
            // und loescht am Ende eine Zeile, die schon weg ist.
            if (!await context.ClawFindings.AnyAsync(f => f.ClawFindingId == sourceId))
            {
                await transaction.RollbackAsync();
                LoggerService.LogInformation(typeof(ClawFindingService),
                    $"Refused to merge claw finding {sourceId}: it no longer exists.");
                return false;
            }

            // Das Ziel muss es noch geben. Der Dialog nimmt es aus dem
            // Service-Cache, und der ist prozessweit - ohne diese Pruefung
            // wuerden die Klauen auf eine ID umgehaengt, die jemand
            // zwischenzeitlich geloescht hat. Fremdschluessel gibt es im Schema
            // keine, die Datenbank haelt das also nicht auf.
            if (!await context.ClawFindings.AnyAsync(f => f.ClawFindingId == targetId))
            {
                await transaction.RollbackAsync();
                LoggerService.LogInformation(typeof(ClawFindingService),
                    $"Refused to merge claw finding {sourceId}: target {targetId} no longer exists.");
                return false;
            }

            // Vier Spalten, vier Updates - eine Behandlung kann denselben
            // Befund an mehreren Klauen tragen und wird dann mehrfach
            // angefasst, was zum selben Ergebnis fuehrt.
            await context.ClawTreatments
                .Where(t => t.ClawFindingLVId == sourceId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ClawFindingLVId, targetId));

            await context.ClawTreatments
                .Where(t => t.ClawFindingRVId == sourceId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ClawFindingRVId, targetId));

            await context.ClawTreatments
                .Where(t => t.ClawFindingLHId == sourceId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ClawFindingLHId, targetId));

            await context.ClawTreatments
                .Where(t => t.ClawFindingRHId == sourceId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ClawFindingRHId, targetId));

            if (!string.IsNullOrWhiteSpace(survivingName))
            {
                var name = survivingName.Trim();
                if (name.Length > ClawFinding.NameMaxLength)
                {
                    name = name[..ClawFinding.NameMaxLength];
                }

                await context.ClawFindings
                    .Where(f => f.ClawFindingId == targetId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(f => f.ClawFindingName, name));
            }

            await context.ClawFindings
                .Where(f => f.ClawFindingId == sourceId)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();
            _databaseStatusService.ReportSuccess();

            await GetAllDataAsync();
            LoggerService.LogInformation(typeof(ClawFindingService),
                "Merged claw finding {@Source} into {@Target}.", sourceId, targetId);

            return true;
        }
        catch (Exception ex)
        {
            // Die Transaktion wird beim Dispose zurueckgerollt - es bleibt
            // nichts halb umgehaengt liegen.
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(ClawFindingService),
                "Failed to merge claw finding, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    public async Task<bool> RemoveByIdAsync(int findingId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            // Der Zaehler auf der Pflegeseite stammt aus dem Seitenaufbau, das
            // Loeschen passiert danach. Fremdschluessel gibt es im Schema
            // keine, die Datenbank haelt hier also nichts auf - deshalb zaehlt
            // der Service unmittelbar vor dem Delete selbst nach.
            var inUse = await context.ClawTreatments.AnyAsync(t =>
                t.ClawFindingLVId == findingId
                || t.ClawFindingRVId == findingId
                || t.ClawFindingLHId == findingId
                || t.ClawFindingRHId == findingId);

            if (inUse)
            {
                await transaction.RollbackAsync();
                LoggerService.LogInformation(typeof(ClawFindingService),
                    $"Refused to delete claw finding {findingId}: still referenced by treatments.");
                return false;
            }

            var affectedRows = await context.ClawFindings
                .Where(f => f.ClawFindingId == findingId)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();

            var isSuccess = affectedRows > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(ClawFindingService),
                    $"Deleted claw finding {findingId}.");
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(ClawFindingService),
                "Failed to delete claw finding, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Sucht den Befund zum Namen und legt ihn an, wenn es ihn nicht gibt.
    ///
    /// <c>null</c> bei leerer Eingabe - das ist der regulaere Fall "an dieser
    /// Klaue wurde nichts erfasst", kein Fehler. <see cref="FailedId"/>, wenn
    /// das Anlegen scheitert; der Aufrufer bricht dann ab, BEVOR er die
    /// Behandlung schreibt.
    ///
    /// Vergleich und Speicherung getrimmt: sonst landet "Mortellaro " als
    /// eigene, optisch nicht unterscheidbare Zeile in der Liste.
    /// </summary>
    public async Task<int?> GetIdByNameAsync(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > ClawFinding.NameMaxLength)
        {
            trimmed = trimmed[..ClawFinding.NameMaxLength];
        }

        var existing = Find(trimmed);
        if (existing is not null)
        {
            return existing.ClawFindingId;
        }

        if (!await InsertDataAsync(new ClawFinding(0, trimmed)))
        {
            return FailedId;
        }

        // InsertDataAsync hat den Cache neu geladen.
        return Find(trimmed)?.ClawFindingId ?? FailedId;
    }

    private ClawFinding? Find(string trimmedName)
        => _cachedFindings.Values.FirstOrDefault(f =>
            string.Equals(f.ClawFindingName.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Vorschlaege fuer das Autocomplete. Nach dem Muster von
    /// TreatmentReasonService.SearchAsync: eine Eingabe ohne Treffer liefert
    /// die Eingabe selbst zurueck, damit sie uebernommen und beim Speichern
    /// angelegt werden kann.
    /// </summary>
    public Task<IEnumerable<string>> SearchAsync(string value, CancellationToken token)
    {
        var names = FindingNames.OrderBy(n => n, StringComparer.CurrentCulture).ToList();

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
