using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services;


public class MedicineService
{
    private ImmutableDictionary<int, Medicine> _cachedMedicines = ImmutableDictionary<int, Medicine>.Empty;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;

    public ImmutableDictionary<int, Medicine> Medicines => _cachedMedicines;

    public MedicineService(IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
    {
        _contextFactory = contextFactory;
        _databaseStatusService = databaseStatusService;
    }

    public async Task GetAllDataAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var medicines = await context.Medicines.AsNoTracking().ToListAsync();
            _cachedMedicines = medicines.ToImmutableDictionary(m => m.MedicineId);
            _databaseStatusService.ReportSuccess();
            LoggerService.LogInformation(typeof(MedicineService), $"Loaded {_cachedMedicines.Count} medicines.");
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(MedicineService), "Failed to load medicines, with {@Message}", ex, ex.Message);
        }
    }

    /// <summary>
    /// Wie oft jedes Medikament in Cow_Treatment und Planned_Cow_Treatment
    /// benutzt wird. Steuert auf der Pflegeseite, ob geloescht werden darf, und
    /// wird deshalb bewusst NICHT aus dem Cache gezaehlt: der ist ein Singleton
    /// und kann aelter sein als die Datenbank.
    ///
    /// Null heisst "konnte nicht gezaehlt werden". Ein leeres Dictionary waere
    /// gefaehrlich - es saehe aus wie "nirgends benutzt" und gaebe den
    /// Papierkorb fuer jede Zeile frei. Anders als beim Behandlungsgrund ist
    /// Medicine_ID nicht nullbar, ein Where auf != null entfaellt also.
    /// </summary>
    public async Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var treated = await context.CowTreatments
                .GroupBy(t => t.MedicineId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync();

            var planned = await context.PlannedCowTreatments
                .GroupBy(t => t.MedicineId)
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
            LoggerService.LogError(typeof(MedicineService),
                "Failed to count medicine usages, with {@Message}", ex, ex.Message);
            return null;
        }
    }

    public async Task<bool> InsertDataAsync(Medicine medicine)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Medicines.AddAsync(medicine);
            var isSuccess = await context.SaveChangesAsync() > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(MedicineService), $"Inserted medicine {medicine.MedicineName}.");
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(MedicineService), "Failed to insert medicine, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    public async Task<bool> RemoveByIdAsync(int medicineId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            // Der Zaehler auf der Pflegeseite stammt aus dem Seitenaufbau, das
            // Loeschen passiert danach. Fremdschluessel gibt es im Schema
            // keine, die Datenbank haelt hier also nichts auf - deshalb zaehlt
            // der Service unmittelbar vor dem Delete selbst nach. Vorher fehlte
            // diese Pruefung ganz und ein benutztes Medikament liess sich
            // loeschen, was jede Behandlung auf eine verwaiste ID zeigen liess.
            var inUse = await context.CowTreatments.AnyAsync(t => t.MedicineId == medicineId)
                        || await context.PlannedCowTreatments.AnyAsync(t => t.MedicineId == medicineId);

            if (inUse)
            {
                await transaction.RollbackAsync();
                LoggerService.LogInformation(typeof(MedicineService),
                    $"Refused to delete medicine {medicineId}: still referenced by treatments.");
                return false;
            }


            var affectedRows = await context.Medicines
                .Where(m => m.MedicineId == medicineId)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();

            var isSuccess = affectedRows > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                // Vorher wurde der Cache hier von Hand gepflegt, waehrend
                // Insert neu laedt. Ein Weg reicht.
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(MedicineService), $"Deleted medicine {medicineId}.");
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(MedicineService), "Failed to delete medicine, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Aendert Name, Dosiereinheit und Standard-Verabreichungsart eines
    /// Medikaments.
    /// </summary>
    public async Task<bool> UpdateDataAsync(Medicine medicine)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var affectedRows = await context.Medicines
                .Where(m => m.MedicineId == medicine.MedicineId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.MedicineName, medicine.MedicineName)
                    .SetProperty(m => m.DosageUnit, medicine.DosageUnit)
                    .SetProperty(m => m.DefaultWhereHowId, medicine.DefaultWhereHowId));

            var isSuccess = affectedRows > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(MedicineService), $"Updated medicine {medicine.MedicineName}.");
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(MedicineService), "Failed to update medicine, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Haengt alle Behandlungen von <paramref name="sourceId"/> auf
    /// <paramref name="targetId"/> um und loescht die Quelle - der Weg, zwei
    /// Eintraege desselben Praeparats zusammenzufuehren oder einen
    /// Tippfehler-Eintrag loszuwerden, der schon benutzt wird und deshalb
    /// nicht loeschbar ist.
    /// </summary>
    /// <param name="survivingName">
    /// Optional der Name, den der Zieleintrag danach tragen soll. Beim
    /// Zusammenfuehren wird er gewaehlt: der kurze Stallname ist im
    /// Autocomplete brauchbarer, die ausgeschriebene Bezeichnung im Nachweis
    /// besser.
    /// </param>
    public async Task<bool> MergeAsync(int sourceId, int targetId, string? survivingName = null)
    {
        // Ohne diesen Guard wuerde ein Merge auf sich selbst erst umhaengen und
        // dann genau das Ziel loeschen.
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
            if (!await context.Medicines.AnyAsync(m => m.MedicineId == sourceId))
            {
                await transaction.RollbackAsync();
                LoggerService.LogInformation(typeof(MedicineService),
                    $"Refused to merge medicine {sourceId}: it no longer exists.");
                return false;
            }

            // Das Ziel muss es noch geben. Der Dialog nimmt es aus dem
            // Service-Cache, und der ist prozessweit - ohne diese Pruefung
            // wuerden die Behandlungen auf eine ID umgehaengt, die jemand
            // zwischenzeitlich geloescht hat. Fremdschluessel gibt es im
            // Schema keine, die Datenbank haelt das also nicht auf.
            if (!await context.Medicines.AnyAsync(m => m.MedicineId == targetId))
            {
                await transaction.RollbackAsync();
                LoggerService.LogInformation(typeof(MedicineService),
                    $"Refused to merge medicine {sourceId}: target {targetId} no longer exists.");
                return false;
            }

            await context.CowTreatments
                .Where(t => t.MedicineId == sourceId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.MedicineId, targetId));

            await context.PlannedCowTreatments
                .Where(t => t.MedicineId == sourceId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.MedicineId, targetId));

            if (!string.IsNullOrWhiteSpace(survivingName))
            {
                var name = survivingName.Trim();
                if (name.Length > Medicine.NameMaxLength)
                {
                    name = name[..Medicine.NameMaxLength];
                }

                await context.Medicines
                    .Where(m => m.MedicineId == targetId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.MedicineName, name));
            }

            await context.Medicines
                .Where(m => m.MedicineId == sourceId)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();
            _databaseStatusService.ReportSuccess();

            await GetAllDataAsync();
            LoggerService.LogInformation(typeof(MedicineService),
                "Merged medicine {@Source} into {@Target}.", sourceId, targetId);

            return true;
        }
        catch (Exception ex)
        {
            // Die Transaktion wird beim Dispose zurueckgerollt - es bleibt
            // nichts halb umgehaengt liegen.
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(MedicineService), "Failed to merge medicine, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    public Medicine? GetById(int medicineId)
    {
        return _cachedMedicines.GetValueOrDefault(medicineId);
    }

    public string GetMedicineNameById(int medicineId)
    {
        return _cachedMedicines.ContainsKey(medicineId) ? _cachedMedicines[medicineId].MedicineName : "--";
    }

    /// <summary>
    /// Einheit des Medikaments, oder <paramref name="fallback"/>, wenn keine
    /// hinterlegt ist. Der Rueckfallwert bleibt Sache des Aufrufers: die
    /// Tabellen haben bisher fest "ml" angezeigt, und das soll fuer Zeilen ohne
    /// Einheit unveraendert so bleiben.
    /// </summary>
    public string GetDosageUnit(int medicineId, string fallback)
    {
        var unit = GetById(medicineId)?.DosageUnit;
        return string.IsNullOrWhiteSpace(unit) ? fallback : unit;
    }

    public List<string> GetMedicineNames()
    {
        return _cachedMedicines.Values.Select(m => m.MedicineName).ToList();
    }

    public List<string> GetMedicineNamesByIds(List<int> medicineIds)
    {
        return _cachedMedicines.Where(m => medicineIds.Contains(m.Key)).Select(m => m.Value.MedicineName).ToList();
    }

    public async Task<int> GetMedicineIdByName(string medicineName)
    {
        if(medicineName == null)
        {
            return int.MinValue;
        }

        var id =  _cachedMedicines.Values.FirstOrDefault(m => m.MedicineName.Trim().ToLower() == medicineName.Trim().ToLower())?.MedicineId ?? -1;
        if(id  == -1)
        {
            var newMedicine = new Medicine(0, medicineName.Trim());
            if (!await InsertDataAsync(newMedicine))
            {
                // Vorher fiel der Code hier auf FirstOrDefault durch und gab
                // ueber den default-KeyValuePair 0 zurueck. Die Dialoge pruefen
                // auf int.MinValue, liessen die 0 also durch und schrieben eine
                // Behandlung mit Medicine_ID 0. Ein Name laenger als die Spalte
                // laesst das Insert scheitern und macht den Weg erreichbar.
                return int.MinValue;
            }

            id = _cachedMedicines.Values
                .FirstOrDefault(m => m.MedicineName.Trim().ToLower() == medicineName.Trim().ToLower())
                ?.MedicineId ?? int.MinValue;
        }

        return id;
    }
}
