# Optionaler Behandlungsgrund — Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kuh- und geplante Kuhbehandlungen bekommen einen optionalen Behandlungsgrund aus einer eigenen Nachschlagetabelle — erfassbar, anzeigbar, filterbar und pflegbar.

**Architecture:** Neue Tabelle `Treatment_Reason` plus je eine nullable FK-Spalte an `Cow_Treatment` und `Planned_Cow_Treatment`. Ein `TreatmentReasonService` (Singleton, prozessweiter Cache) nach dem Vorbild von `WhereHowService` trägt Anlage, Pflege, Merge und Autocomplete-Suche. Die UI folgt durchgehend bestehenden Mustern: Autocomplete im Dialog wie beim Medikament, Spalte und Mehrfachauswahl-Filter wie bei den vorhandenen Tabellen, Basisdaten-Seite wie `BaseDataWhereHow`.

**Tech Stack:** .NET 8 (SDK 10.0.301 installiert), Blazor Server, MudBlazor 7.15.0, EF Core 8.0.6 mit Pomelo.EntityFrameworkCore.MySql 8.0.2, MariaDB, xUnit 2.5.3.

**Spec:** `docs/superpowers/specs/2026-09-09-behandlungsgrund-design.md` — bei jedem Zweifel gilt der Spec.

## Global Constraints

- **Git: nichts committen.** Auf Entscheidung des Nutzers (2026-09-09) läuft die Umsetzung direkt auf `main` und **ohne Commits** — alle Änderungen bleiben im Working Tree. **Task 0 entfällt, und der Commit-Schritt am Ende jedes Tasks wird übersprungen.** Der Nutzer committet selbst, wenn er darübergeschaut hat.
- **Laufende App blockiert den Build.** Solange `4Cows-FE` im Rider-Debugger läuft, scheitert `dotnet build` mit `MSB3027` / `MSB3021`: die Datei `4Cows-FE/bin/Debug/net8.0/BBCowDataLibrary.dll` ist gesperrt. Vor jedem Solution-Build die laufende Instanz beenden. Der Fehler ist **kein** Codefehler.
- **EF-Kommandos immer mit der Library als Startup-Projekt:** `--project BBCowDataLibrary --startup-project BBCowDataLibrary`. `4Cows-FE` referenziert `Microsoft.EntityFrameworkCore.Design` nicht (im Library-csproj steht `PrivateAssets=all`), mit `--startup-project 4Cows-FE` bricht jedes EF-Kommando ab.
- **Die Dev-Datenbank muss laufen**, weil `DatabaseContextFactory` `ServerVersion.AutoDetect` aufruft: `docker compose -f docker-compose.dev.yml up -d`. Container heißt `4cows-dev-db`, Port 3306, root/admin, DB `4cows_v2`.
- **Keine automatisierten Tests.** `BBCowDataLibrary.Tests` enthält ausschließlich reine Logik-Tests (`Kpi/`) und keinerlei DB-Infrastruktur; `TableFilters.cs` liegt in `4Cows-FE`, worauf das Testprojekt nicht verweist. Der Spec schließt beides bewusst aus. **Keine Testprojekt-Referenzen hinzufügen, keine Tests erfinden.** Jeder Task schließt stattdessen mit einem Build ab, Task 8 mit einem Durchlauf in der App.
- **Sprache.** Alle sichtbaren Texte und alle Codekommentare auf Deutsch, ohne Umlaute in Kommentaren (Projektkonvention: `waehlt`, `Gruende`), mit Umlauten in UI-Texten.
- **Namenskonvention der Spalten:** Tabelle `Treatment_Reason`, Spalten `Treatment_Reason_ID` und `Treatment_Reason_Name`, FK-Spalte in beiden Behandlungstabellen ebenfalls `Treatment_Reason_ID`.
- **Klauenbehandlungen bleiben unberührt.** `ClawTreatment` und `PlannedClawTreatment` werden in keinem Task angefasst.

---

## Dateiübersicht

| Datei | Rolle |
| --- | --- |
| `BBCowDataLibrary/Models/TreatmentReason.cs` | **neu** — die Entität, gebaut wie `Medicine` |
| `BBCowDataLibrary/Models/Treatment_Cow.cs` | Property `TreatmentReasonId` + optionaler Ctor-Parameter |
| `BBCowDataLibrary/Models/Planned_Treatment_Cow.cs` | dito |
| `BBCowDataLibrary/SQL/DatabaseContext.cs` | `DbSet<TreatmentReason>` |
| `BBCowDataLibrary/Migrations/*_AddTreatmentReason.*` | **generiert** — Tabelle + zwei Spalten |
| `BBCowDataLibrary/Services/TreatmentReasonService.cs` | **neu** — Cache, CRUD, Merge, Suche |
| `4Cows-FE/Program.cs` | Singleton-Registrierung |
| `4Cows-FE/Components/Services/MeadowDataLoader.cs` | Cache im Lookup-Pfad laden |
| `4Cows-FE/Components/4CowsComponent/Dialogs/Add_Cow_Treatment_Dialog.razor` | Eingabefeld + Speichern |
| `4Cows-FE/Components/4CowsComponent/Dialogs/Add_Planned_Cow_Treatment_Dialog.razor` | dito |
| `4Cows-FE/Components/Pages/Tables/Cow_Table.razor` | Spalte, Mobil-Karte, Filter |
| `4Cows-FE/Components/Pages/Tables/Planned_Cow_Table.razor` | Spalte, Mobil-Karte, Filter, Grund-Übernahme beim Abschließen |
| `4Cows-FE/Components/Meadow/TableFilters.cs` | `ReasonFilter` + `Reasons` in beiden Filterklassen |
| `4Cows-FE/Components/4CowsComponent/BaseData/BaseDataTreatmentReason.razor` | **neu** — Pflegeseite |
| `4Cows-FE/Components/4CowsComponent/Dialogs/BaseData/EditTreatmentReasonDialog.razor` | **neu** — Anlegen / Umbenennen / Merge |
| `4Cows-FE/Components/Pages/Settings.razor` | neuer Basisdaten-Reiter |
| `BBCowDataLibrary/SQL/DemoDataSeeder.cs` | Beispielgründe für die Demo |

---

## Task 0: Branch anlegen

**Files:** keine

- [ ] **Schritt 1: Sauberen Ausgangszustand prüfen**

```bash
git -C "C:/Users/benjamin.biber/RiderProjects/4Cows" status --short
```

Erwartet: nur untrackte Einträge (`?? .claude/`, `?? docs/`). Sind Quelldateien geändert, erst mit dem Nutzer klären.

- [ ] **Schritt 2: Branch erstellen**

```bash
git -C "C:/Users/benjamin.biber/RiderProjects/4Cows" checkout -b feature/behandlungsgrund
```

- [ ] **Schritt 3: Spec mitcommitten**

```bash
git add docs/superpowers/specs/2026-09-09-behandlungsgrund-design.md docs/superpowers/plans/2026-09-09-behandlungsgrund.md
git commit -m "docs: Spec und Plan fuer optionalen Behandlungsgrund"
```

---

## Task 1: Modell, DbSet und Migration

**Files:**
- Create: `BBCowDataLibrary/Models/TreatmentReason.cs`
- Modify: `BBCowDataLibrary/Models/Treatment_Cow.cs`
- Modify: `BBCowDataLibrary/Models/Planned_Treatment_Cow.cs`
- Modify: `BBCowDataLibrary/SQL/DatabaseContext.cs:20`
- Generated: `BBCowDataLibrary/Migrations/<timestamp>_AddTreatmentReason.cs`, `.Designer.cs`, `DatabaseContextModelSnapshot.cs`

**Interfaces:**
- Produces: `BB_Cow.Class.TreatmentReason` mit `int TreatmentReasonId`, `string TreatmentReasonName`, Ctor `(int, string)` und parameterlos.
- Produces: `CowTreatment.TreatmentReasonId` und `PlannedCowTreatment.TreatmentReasonId`, beide `int?`.
- Produces: `DatabaseContext.TreatmentReasons` als `DbSet<TreatmentReason>`.

- [ ] **Schritt 1: Entität anlegen**

Datei `BBCowDataLibrary/Models/TreatmentReason.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BB_Cow.Class;

/// <summary>
/// Behandlungsgrund einer Kuh- oder geplanten Kuhbehandlung.
///
/// Aufgebaut wie <see cref="Medicine"/>: eine reine Nachschlagetabelle, deren
/// Eintraege ueberwiegend nebenbei im Behandlungs-Dialog entstehen.
/// varchar(64) wie Medicine_Name - ohne StringLength macht Pomelo daraus
/// longtext, was WhereHow_Name bis heute mitschleppt.
/// </summary>
[Table("Treatment_Reason")]
public class TreatmentReason
{
    [Key]
    [Column("Treatment_Reason_ID")]
    public int TreatmentReasonId { get; set; }

    [Required]
    [StringLength(64)]
    [Column("Treatment_Reason_Name")]
    public string TreatmentReasonName { get; set; }

    public TreatmentReason() : this(0, string.Empty) { }

    public TreatmentReason(int treatmentReasonId, string treatmentReasonName)
    {
        TreatmentReasonId = treatmentReasonId;
        TreatmentReasonName = treatmentReasonName;
    }
}
```

- [ ] **Schritt 2: `CowTreatment` erweitern**

In `BBCowDataLibrary/Models/Treatment_Cow.cs` nach der Property `UdderId` einfügen:

```csharp
        /// <summary>
        /// Behandlungsgrund, optional. NULL heisst "kein Grund angegeben".
        ///
        /// Bewusst nullable und nicht der int.MinValue-Sentinel von UdderId:
        /// dort bedeutet der Wert "noch nicht gewaehlt" und Save besteht je
        /// nach Wie/Wo darauf, hier ist "kein Grund" ein regulaerer
        /// Dauerzustand - und alle Bestandszeilen bekommen ihn durch die
        /// Migration von selbst.
        /// </summary>
        [Column("Treatment_Reason_ID")]
        public int? TreatmentReasonId { get; set; }
```

Danach den Konstruktor um einen **optionalen** Parameter am Ende erweitern — optional, damit `DemoDataSeeder` und der parameterlose Ctor unveraendert weiter kompilieren:

```csharp
        public CowTreatment() : this(0, string.Empty, 0, DateTime.MinValue, 0.0f, int.MinValue, int.MinValue) { }

        public CowTreatment(int cowTreatmentId, string earTagNumber, int medicineId, DateTime administrationDate, float medicineDosage, int whereHowId, int udderId, int? treatmentReasonId = null)
        {
            CowTreatmentId = cowTreatmentId;
            EarTagNumber = earTagNumber;
            MedicineId = medicineId;
            AdministrationDate = administrationDate;
            MedicineDosage = medicineDosage;
            WhereHowId = whereHowId;
            UdderId = udderId;
            TreatmentReasonId = treatmentReasonId;
        }
```

- [ ] **Schritt 3: `PlannedCowTreatment` erweitern**

In `BBCowDataLibrary/Models/Planned_Treatment_Cow.cs` nach der Property `UdderId` einfügen:

```csharp
        /// <summary>
        /// Behandlungsgrund, optional. NULL heisst "kein Grund angegeben" -
        /// siehe den Kommentar an CowTreatment.TreatmentReasonId.
        /// </summary>
        [Column("Treatment_Reason_ID")]
        public int? TreatmentReasonId { get; set; }
```

Und den Konstruktor:

```csharp
        public PlannedCowTreatment() : this(0, string.Empty, 0, DateTime.MinValue, 0.0f, int.MinValue, false, false, int.MinValue) { }

        public PlannedCowTreatment(int plannedCowTreatmentId, string earTagNumber, int medicineId, DateTime administrationDate, float medicineDosage, int whereHowId, bool isFound, bool isTreatet, int udderId, int? treatmentReasonId = null)
        {
            PlannedCowTreatmentId = plannedCowTreatmentId;
            EarTagNumber = earTagNumber;
            MedicineId = medicineId;
            AdministrationDate = administrationDate;
            MedicineDosage = medicineDosage;
            WhereHowId = whereHowId;
            IsFound = isFound;
            IsTreatet = isTreatet;
            UdderId = udderId;
            TreatmentReasonId = treatmentReasonId;
        }
```

- [ ] **Schritt 4: DbSet ergänzen**

In `BBCowDataLibrary/SQL/DatabaseContext.cs` nach der Zeile mit `WhereHows`:

```csharp
    public DbSet<TreatmentReason> TreatmentReasons => Set<TreatmentReason>();
```

- [ ] **Schritt 5: Bauen**

```bash
dotnet build BBCowDataLibrary/BBCowDataLibrary.csproj --nologo -v q
```

Erwartet: `Build succeeded`, nur die bekannten CS8618/CS8603-Warnungen aus `Services/`. Keine Fehler.

- [ ] **Schritt 6: Datenbank hochfahren, falls sie nicht läuft**

```bash
docker compose -f docker-compose.dev.yml up -d
```

Erwartet: `4cows-dev-db` läuft. Prüfen mit `docker ps --filter name=4cows-dev-db`.

- [ ] **Schritt 7: Migration erzeugen**

```bash
dotnet ef migrations add AddTreatmentReason --project BBCowDataLibrary --startup-project BBCowDataLibrary
```

Erwartet: `Build succeeded.` und `Done. To undo this action, use 'ef migrations remove'`. Es entstehen drei geänderte/neue Dateien unter `BBCowDataLibrary/Migrations/`.

- [ ] **Schritt 8: Migration prüfen**

```bash
cat BBCowDataLibrary/Migrations/*_AddTreatmentReason.cs
```

Erwartet, dass `Up` **genau** drei Dinge tut:
1. `CreateTable(name: "Treatment_Reason", ...)` mit `Treatment_Reason_ID` als Identity-PK und `Treatment_Reason_Name` als `varchar(64)`, `nullable: false`;
2. `AddColumn<int>(name: "Treatment_Reason_ID", table: "Cow_Treatment", nullable: true)`;
3. `AddColumn<int>(name: "Treatment_Reason_ID", table: "Planned_Cow_Treatment", nullable: true)`.

**Abbruchbedingung:** Steht dort irgendein `DropColumn`, `AlterColumn` oder eine Änderung an einer anderen Tabelle, ist der Modell-Snapshot aus dem Tritt. Dann `dotnet ef migrations remove --project BBCowDataLibrary --startup-project BBCowDataLibrary`, Ursache klären, nicht einfach weitermachen.

- [ ] **Schritt 9: Migration anwenden und Schema kontrollieren**

```bash
dotnet ef database update --project BBCowDataLibrary --startup-project BBCowDataLibrary
```

Erwartet: `Applying migration '<timestamp>_AddTreatmentReason'.` und `Done.`

```bash
docker exec 4cows-dev-db mariadb -uroot -padmin 4cows_v2 -e "DESCRIBE Treatment_Reason; SHOW COLUMNS FROM Cow_Treatment LIKE 'Treatment_Reason_ID'; SHOW COLUMNS FROM Planned_Cow_Treatment LIKE 'Treatment_Reason_ID';"
```

Erwartet: die Tabelle existiert, und beide Spalten stehen mit `Null = YES` da.

- [ ] **Schritt 10: Commit**

```bash
git add BBCowDataLibrary/Models/TreatmentReason.cs BBCowDataLibrary/Models/Treatment_Cow.cs BBCowDataLibrary/Models/Planned_Treatment_Cow.cs BBCowDataLibrary/SQL/DatabaseContext.cs BBCowDataLibrary/Migrations/
git commit -m "feat: Tabelle Treatment_Reason und nullable FK an den Kuhbehandlungen"
```

---

## Task 2: TreatmentReasonService und Verdrahtung

**Files:**
- Create: `BBCowDataLibrary/Services/TreatmentReasonService.cs`
- Modify: `4Cows-FE/Program.cs:65` (nach der `WhereHowService`-Registrierung)
- Modify: `4Cows-FE/Components/Services/MeadowDataLoader.cs`

**Interfaces:**
- Consumes: `TreatmentReason`, `DatabaseContext.TreatmentReasons`, `CowTreatment.TreatmentReasonId`, `PlannedCowTreatment.TreatmentReasonId` aus Task 1.
- Produces: `BB_Cow.Services.TreatmentReasonService` mit
  - `const string NoReasonText = "–"`
  - `ImmutableDictionary<int, TreatmentReason> Reasons { get; }`
  - `List<string> ReasonNames { get; }`
  - `Task GetAllDataAsync()`
  - `Task<ImmutableDictionary<int, int>?> GetUsageCountsAsync()`
  - `Task<bool> InsertDataAsync(TreatmentReason reason)`
  - `Task<bool> UpdateDataAsync(TreatmentReason reason)`
  - `Task<bool> MergeAsync(int sourceId, int targetId)`
  - `Task<bool> RemoveByIdAsync(int reasonId)`
  - `string GetNameById(int? id)`
  - `Task<int> GetIdByNameAsync(string? name)` — liefert `int.MinValue` bei Fehlschlag
  - `Task<IEnumerable<string>> SearchAsync(string value, CancellationToken token)`

- [ ] **Schritt 1: Service anlegen**

Datei `BBCowDataLibrary/Services/TreatmentReasonService.cs`:

```csharp
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
```

- [ ] **Schritt 2: Als Singleton registrieren**

In `4Cows-FE/Program.cs` direkt hinter `builder.Services.AddSingleton<WhereHowService>();`:

```csharp
builder.Services.AddSingleton<TreatmentReasonService>();
```

- [ ] **Schritt 3: Cache im Lookup-Pfad laden**

In `4Cows-FE/Components/Services/MeadowDataLoader.cs` drei Stellen ergänzen.

Feld, hinter `_whereHows`:

```csharp
    private readonly TreatmentReasonService _reasons;
```

Konstruktor — Parameter hinter `WhereHowService whereHows` einfügen und zuweisen:

```csharp
        WhereHowService whereHows,
        TreatmentReasonService reasons,
```

```csharp
        _whereHows = whereHows;
        _reasons = reasons;
```

Und in `EnsureLookupsAsync()` hinter `await _whereHows.GetAllDataAsync();`:

```csharp
        await _reasons.GetAllDataAsync();
```

Der XML-Kommentar über `EnsureLookupsAsync` wird mitgeführt:

```csharp
    /// <summary>Kuehe, Medikamente, Wie/Wo, Behandlungsgruende, Euterviertel - alles, was Spalten aufloest.</summary>
```

- [ ] **Schritt 4: Bauen**

Vorher die laufende App im Rider-Debugger beenden, sonst scheitert der Kopierschritt.

```bash
dotnet build BB_Cow.sln --nologo -v q
```

Erwartet: `Build succeeded`, keine Fehler. Nur die bekannten Nullable-Warnungen.

- [ ] **Schritt 5: Commit**

```bash
git add BBCowDataLibrary/Services/TreatmentReasonService.cs 4Cows-FE/Program.cs 4Cows-FE/Components/Services/MeadowDataLoader.cs
git commit -m "feat: TreatmentReasonService mit Cache, Merge und Autocomplete-Suche"
```

---

## Task 3: Behandlungsgrund in den Eingabedialogen

**Files:**
- Modify: `4Cows-FE/Components/4CowsComponent/Dialogs/Add_Cow_Treatment_Dialog.razor`
- Modify: `4Cows-FE/Components/4CowsComponent/Dialogs/Add_Planned_Cow_Treatment_Dialog.razor`
- Modify: `4Cows-FE/Components/Pages/Tables/Planned_Cow_Table.razor` (Methode `Complete`)

**Interfaces:**
- Consumes: `TreatmentReasonService.SearchAsync`, `.GetIdByNameAsync`, `.Reasons` aus Task 2; `CowTreatment.TreatmentReasonId`, `PlannedCowTreatment.TreatmentReasonId` aus Task 1.
- Produces: gespeicherte Behandlungen mit gesetztem oder bewusst leerem `TreatmentReasonId`.

- [ ] **Schritt 1: `Add_Cow_Treatment_Dialog` — Service injizieren**

Hinter `@inject UdderService UdderService`:

```razor
@inject TreatmentReasonService TreatmentReasonService
```

- [ ] **Schritt 2: `Add_Cow_Treatment_Dialog` — Feld einfügen**

Direkt **nach** dem `<div class="mw-field-row">`, das „Wie / Wo" und „Menge" enthält, und **vor** dem schließenden `</ChildContent>`:

```razor
        <div>
            <label class="mw-label" for="mw-reason">Behandlungsgrund</label>
            <div class="mw-field">
                @* Optional: leer lassen ist erlaubt und loest in Save keine
                   Meldung aus. CoerceValue wie beim Medikament - ein
                   unbekannter Grund wird beim Speichern angelegt. *@
                <MudAutocomplete T="string" InputId="mw-reason" Variant="Variant.Text"
                                 SelectValueOnTab="true" CoerceValue="true"
                                 ResetValueOnEmptyText="true"
                                 @bind-Value="ReasonName"
                                 SearchFunc="TreatmentReasonService.SearchAsync"
                                 Placeholder="optional, z.B. Mastitis" />
            </div>
        </div>
```

- [ ] **Schritt 3: `Add_Cow_Treatment_Dialog` — Feldvariable und Vorbelegung**

Im `@code`-Block hinter `private string? MedicineName;`:

```csharp
    private string? ReasonName;
```

In `OnInitialized()` ans Ende, hinter dem `if (Cow_Treatment.WhereHowId != int.MinValue)`-Block:

```csharp
        // Der Abschluss-Dialog einer geplanten Behandlung bekommt den Grund
        // vorbelegt. Bewusst ueber den Cache und nicht ueber GetNameById: das
        // liefert bei unbekannter ID "–", und dieses Zeichen stuende dann im
        // Eingabefeld - und wuerde beim Speichern als Grund angelegt.
        if (Cow_Treatment.TreatmentReasonId is int reasonId
            && TreatmentReasonService.Reasons.TryGetValue(reasonId, out var reason))
        {
            ReasonName = reason.TreatmentReasonName;
        }
```

- [ ] **Schritt 4: `Add_Cow_Treatment_Dialog` — Speichern**

In `Save()` **nach** dem Block, der `udderId` bestimmt, und **vor** `var saved = 0;`:

```csharp
        // Optional: ein leeres Feld ist gueltig und ergibt null. Aufgeloest
        // wird VOR der Einfuege-Schleife - wie Medikament und Wie/Wo, damit
        // ein Fehlschlag keine halb gespeicherte Serie zuruecklaesst.
        int? reasonId = null;
        if (!string.IsNullOrWhiteSpace(ReasonName))
        {
            var resolved = await TreatmentReasonService.GetIdByNameAsync(ReasonName);
            if (resolved == int.MinValue)
            {
                Snackbar.Add("Behandlungsgrund konnte nicht gespeichert werden.", Severity.Error);
                return;
            }

            reasonId = resolved;
        }
```

Und im Objektinitialisierer der Schleife hinter `UdderId = udderId`:

```csharp
                UdderId = udderId,
                TreatmentReasonId = reasonId
```

- [ ] **Schritt 5: `Add_Planned_Cow_Treatment_Dialog` — Service injizieren**

Hinter `@inject UdderService UdderService`:

```razor
@inject TreatmentReasonService TreatmentReasonService
```

- [ ] **Schritt 6: `Add_Planned_Cow_Treatment_Dialog` — Feld einfügen**

Nach dem `<div class="mw-field-row">` mit „Wie / Wo" und „Menge", vor `</ChildContent>`:

```razor
        <div>
            <label class="mw-label" for="mw-planned-reason">Behandlungsgrund</label>
            <div class="mw-field">
                @* Optional: leer lassen ist erlaubt und loest in Save keine
                   Meldung aus. CoerceValue wie beim Medikament - ein
                   unbekannter Grund wird beim Planen angelegt. *@
                <MudAutocomplete T="string" InputId="mw-planned-reason" Variant="Variant.Text"
                                 SelectValueOnTab="true" CoerceValue="true"
                                 ResetValueOnEmptyText="true"
                                 @bind-Value="_reasonName"
                                 SearchFunc="TreatmentReasonService.SearchAsync"
                                 Placeholder="optional, z.B. Mastitis" />
            </div>
        </div>
```

- [ ] **Schritt 7: `Add_Planned_Cow_Treatment_Dialog` — Feldvariable und Speichern**

Im `@code`-Block hinter `private float _dosage;`:

```csharp
    private string? _reasonName;
```

In `Save()` nach der `udderId`-Bestimmung, vor `var saved = 0;`:

```csharp
        // Optional: ein leeres Feld ist gueltig und ergibt null. Aufgeloest
        // wird VOR der Einfuege-Schleife - wie Medikament und Wie/Wo, damit
        // ein Fehlschlag keine halb gespeicherte Serie zuruecklaesst.
        int? reasonId = null;
        if (!string.IsNullOrWhiteSpace(_reasonName))
        {
            var resolved = await TreatmentReasonService.GetIdByNameAsync(_reasonName);
            if (resolved == int.MinValue)
            {
                Snackbar.Add("Behandlungsgrund konnte nicht gespeichert werden.", Severity.Error);
                return;
            }

            reasonId = resolved;
        }
```

Und im Objektinitialisierer hinter `UdderId = udderId`:

```csharp
                UdderId = udderId,
                TreatmentReasonId = reasonId
```

- [ ] **Schritt 8: Grund beim Abschließen übernehmen**

In `4Cows-FE/Components/Pages/Tables/Planned_Cow_Table.razor`, Methode `Complete`, im `new CowTreatment { ... }` hinter `UdderId = planned.UdderId`:

```csharp
            UdderId = planned.UdderId,
            TreatmentReasonId = planned.TreatmentReasonId
```

- [ ] **Schritt 9: Bauen**

```bash
dotnet build BB_Cow.sln --nologo -v q
```

Erwartet: `Build succeeded`, keine Fehler.

- [ ] **Schritt 10: Commit**

```bash
git add 4Cows-FE/Components/4CowsComponent/Dialogs/Add_Cow_Treatment_Dialog.razor 4Cows-FE/Components/4CowsComponent/Dialogs/Add_Planned_Cow_Treatment_Dialog.razor 4Cows-FE/Components/Pages/Tables/Planned_Cow_Table.razor
git commit -m "feat: Behandlungsgrund in beiden Kuh-Dialogen, Uebernahme beim Abschliessen"
```

---

## Task 4: Spalte „Grund" in beiden Tabellen

**Files:**
- Modify: `4Cows-FE/Components/Pages/Tables/Cow_Table.razor`
- Modify: `4Cows-FE/Components/Pages/Tables/Planned_Cow_Table.razor`

**Interfaces:**
- Consumes: `TreatmentReasonService.GetNameById(int?)` und `.Reasons` aus Task 2.
- Produces: in beiden Tabellen die privaten Helfer `string Reason(...)` (Anzeige, `–` wenn leer) und `string? ReasonKey(...)` (Filter, `null` wenn leer). Task 5 baut auf beiden auf.

- [ ] **Schritt 1: `Cow_Table` — Service injizieren**

Hinter `@inject UdderService UdderService`:

```razor
@inject TreatmentReasonService TreatmentReasonService
```

- [ ] **Schritt 2: `Cow_Table` — Anzeige-Helfer**

Im `@code`-Block hinter der Methode `Dosage`:

```csharp
    // Fuer Spalte und Karte: "–", wenn nichts gesetzt ist.
    private string Reason(CowTreatment r) => TreatmentReasonService.GetNameById(r.TreatmentReasonId);

    // Fuer Filter und Optionsliste: null, wenn nichts gesetzt ist. Das "–" der
    // Anzeige darf hier NICHT auftauchen, sonst stuende es als waehlbarer
    // "Grund" in der Filterliste. Eine gesetzte, aber unbekannte ID gilt
    // ebenfalls als "ohne Grund" - anzeigen laesst sie sich ohnehin nicht.
    private string? ReasonKey(CowTreatment r)
        => r.TreatmentReasonId is int id
           && TreatmentReasonService.Reasons.TryGetValue(id, out var reason)
            ? reason.TreatmentReasonName
            : null;
```

- [ ] **Schritt 3: `Cow_Table` — Spalte anhängen**

In `BuildColumns()` als **letzten** Eintrag der Liste, hinter der Spalte „Menge":

```csharp
        ,
        new MeadowColumn<CowTreatment>
        {
            Title = "Grund",
            Text = Reason,
            SortBy = Reason
        }
```

- [ ] **Schritt 4: `Cow_Table` — Mobil-Karte**

Im `<MobileCard>`-Template, innerhalb von `<div class="mw-row-card__fields">` hinter dem Feld „Menge":

```razor
                @* Ohne Grund entfaellt die Angabe ganz - ein "–" kostet auf der
                   Karte nur Platz, den die drei belegten Felder brauchen. *@
                @if (row.TreatmentReasonId is not null)
                {
                    <div class="mw-row-card__field">
                        <span class="mw-row-card__label">Grund</span>
                        <span class="mw-row-card__value">@Reason(row)</span>
                    </div>
                }
```

- [ ] **Schritt 5: `Planned_Cow_Table` — Service injizieren**

Hinter `@inject UdderService UdderService`:

```razor
@inject TreatmentReasonService TreatmentReasonService
```

- [ ] **Schritt 6: `Planned_Cow_Table` — Anzeige-Helfer**

Im `@code`-Block hinter der Methode `Dosage`:

```csharp
    // Fuer Spalte und Karte: "–", wenn nichts gesetzt ist.
    private string Reason(PlannedCowTreatment r) => TreatmentReasonService.GetNameById(r.TreatmentReasonId);

    // Fuer Filter und Optionsliste: null, wenn nichts gesetzt ist. Siehe den
    // gleichnamigen Helfer in Cow_Table.
    private string? ReasonKey(PlannedCowTreatment r)
        => r.TreatmentReasonId is int id
           && TreatmentReasonService.Reasons.TryGetValue(id, out var reason)
            ? reason.TreatmentReasonName
            : null;
```

- [ ] **Schritt 7: `Planned_Cow_Table` — Spalte einfügen**

In `BuildColumns()` **vor** dem `columns.Add(...)` mit `IsActions = true`:

```csharp
        columns.Add(new MeadowColumn<PlannedCowTreatment>
        {
            Title = "Grund",
            Text = Reason,
            SortBy = Reason
        });
```

- [ ] **Schritt 8: `Planned_Cow_Table` — Mobil-Karte**

Im `<MobileCard>`-Template, innerhalb von `<div class="mw-row-card__meta">` hinter `<span>@Dosage(row)</span>`:

```razor
                @if (row.TreatmentReasonId is not null)
                {
                    <span>@Reason(row)</span>
                }
```

- [ ] **Schritt 9: Bauen**

```bash
dotnet build BB_Cow.sln --nologo -v q
```

Erwartet: `Build succeeded`, keine Fehler.

- [ ] **Schritt 10: Commit**

```bash
git add 4Cows-FE/Components/Pages/Tables/Cow_Table.razor 4Cows-FE/Components/Pages/Tables/Planned_Cow_Table.razor
git commit -m "feat: Spalte Grund in beiden Kuh-Tabellen, Desktop und mobil"
```

---

## Task 5: Filter „Grund" inklusive „Ohne Grund"

**Files:**
- Modify: `4Cows-FE/Components/Meadow/TableFilters.cs`
- Modify: `4Cows-FE/Components/Pages/Tables/Cow_Table.razor`
- Modify: `4Cows-FE/Components/Pages/Tables/Planned_Cow_Table.razor`

**Interfaces:**
- Consumes: `ReasonKey(...)` aus Task 4.
- Produces: `_4Cows_FE.Components.Meadow.ReasonFilter` mit
  - `const string NoneOption = "Ohne Grund"`
  - `static IReadOnlyList<string> Options(IEnumerable<string?> reasonNames)`
  - `static bool Matches(HashSet<string> selected, string? reasonName)`
- Produces: `CowTableFilter.Reasons` und `PlannedCowTableFilter.Reasons`, beide `HashSet<string>` mit `OrdinalIgnoreCase`.

- [ ] **Schritt 1: `ReasonFilter` anlegen**

In `4Cows-FE/Components/Meadow/TableFilters.cs`, direkt **vor** `/// <summary>Suchtext und Filter der Kuh-Tabelle.</summary>`:

```csharp
/// <summary>
/// Die Grund-Auswahl der beiden Kuh-Tabellen.
///
/// "Ohne Grund" ist ausschliesslich eine Anzeigeoption: sie steht in derselben
/// Auswahlmenge wie die echten Gruende, entsteht aber nie in der Datenbank und
/// wird nie an TreatmentReasonService.GetIdByNameAsync weitergereicht. Die
/// Konstante liegt hier, damit Filterleiste und Zeilenpruefung nicht mit zwei
/// getrennten Literalen auseinanderdriften.
/// </summary>
public static class ReasonFilter
{
    public const string NoneOption = "Ohne Grund";

    /// <summary>
    /// Die Optionsliste aus den tatsaechlich vorkommenden Gruenden - nicht aus
    /// dem gesamten Stammdatenbestand, gleiche Regel wie bei den Medikamenten.
    /// "Ohne Grund" steht vorn, aber nur, wenn es ueberhaupt eine Zeile ohne
    /// Grund gibt: sonst stuende dort eine Option, die garantiert nichts findet.
    /// </summary>
    public static IReadOnlyList<string> Options(IEnumerable<string?> reasonNames)
    {
        var names = reasonNames.ToList();

        var known = names
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCulture)
            .ToList();

        if (!names.Any(string.IsNullOrEmpty))
        {
            return known;
        }

        var withNone = new List<string>(known.Count + 1) { NoneOption };
        withNone.AddRange(known);
        return withNone;
    }

    /// <summary>
    /// Leere Auswahl = kein Filter. Sonst ODER innerhalb der Gruppe: die Zeile
    /// passt, wenn ihr Grund gewaehlt ist - oder wenn sie keinen hat und
    /// "Ohne Grund" gewaehlt ist.
    /// </summary>
    public static bool Matches(HashSet<string> selected, string? reasonName)
    {
        if (selected.Count == 0)
        {
            return true;
        }

        return string.IsNullOrEmpty(reasonName)
            ? selected.Contains(NoneOption)
            : selected.Contains(reasonName);
    }
}
```

- [ ] **Schritt 2: `CowTableFilter` erweitern**

In `CowTableFilter` hinter der Property `Cows`:

```csharp
    /// <summary>
    /// Behandlungsgruende. Leer heisst "Alle". Kann neben echten Gruenden den
    /// Sentinel <see cref="ReasonFilter.NoneOption"/> enthalten, der fuer
    /// Zeilen ohne Grund steht.
    /// </summary>
    public HashSet<string> Reasons { get; } = new(StringComparer.OrdinalIgnoreCase);
```

`ActiveCount` erweitern:

```csharp
    public int ActiveCount
        => (Medicines.Count > 0 ? 1 : 0)
           + (Cows.Count > 0 ? 1 : 0)
           + (Reasons.Count > 0 ? 1 : 0)
           + (Range != DateRange.All ? 1 : 0);
```

`Reset()` erweitern:

```csharp
    public void Reset()
    {
        Medicines.Clear();
        Cows.Clear();
        Reasons.Clear();
        Range = DateRange.All;
    }
```

- [ ] **Schritt 3: `PlannedCowTableFilter` erweitern**

Hinter der Property `Medicines`:

```csharp
    /// <summary>Behandlungsgruende - siehe <see cref="CowTableFilter.Reasons"/>.</summary>
    public HashSet<string> Reasons { get; } = new(StringComparer.OrdinalIgnoreCase);
```

`ActiveCount`:

```csharp
    public int ActiveCount
        => (Medicines.Count > 0 ? 1 : 0)
           + (Reasons.Count > 0 ? 1 : 0)
           + (Found != FlagStates.Any ? 1 : 0)
           + (Treated != FlagStates.Any ? 1 : 0)
           + (Range != PlannedDateRange.All ? 1 : 0);
```

`Reset()`:

```csharp
    public void Reset()
    {
        Medicines.Clear();
        Reasons.Clear();
        Found = FlagStates.Any;
        Treated = FlagStates.Any;
        Range = PlannedDateRange.All;
    }
```

- [ ] **Schritt 4: `Cow_Table` — Optionsliste**

Im `@code`-Block hinter der Property `CowOptions`:

```csharp
    /// <summary>
    /// Vorkommende Gruende plus ggf. "Ohne Grund". Sortierung und
    /// Sentinel-Behandlung liegen in ReasonFilter.Options.
    /// </summary>
    private IReadOnlyList<string> ReasonOptions => ReasonFilter.Options(_all.Select(ReasonKey));
```

- [ ] **Schritt 5: `Cow_Table` — Filter anwenden**

In `Apply()` **vor** der Zeile `query = query.Where(r => DateRanges.Matches(...));`:

```csharp
        // Leere Auswahl laesst alles durch, der Aufruf darf deshalb
        // unbedingt stehen.
        query = query.Where(r => ReasonFilter.Matches(_filter.Reasons, ReasonKey(r)));
```

- [ ] **Schritt 6: `Cow_Table` — Auswahl in der Filterleiste**

Im `<MeadowFilterPopover>`-`ChildContent`, hinter dem `MeadowMultiSelect` „Kuh (Halsband)":

```razor
                <MeadowMultiSelect Label="Grund" Options="ReasonOptions"
                                   Selected="_filter.Reasons"
                                   SelectionChanged="Apply" />
```

- [ ] **Schritt 7: `Planned_Cow_Table` — Optionsliste**

Im `@code`-Block hinter der Property `MedicineOptions`:

```csharp
    /// <summary>Vorkommende Gruende plus ggf. "Ohne Grund" - wie in Cow_Table.</summary>
    private IReadOnlyList<string> ReasonOptions => ReasonFilter.Options(_all.Select(ReasonKey));
```

- [ ] **Schritt 8: `Planned_Cow_Table` — Filter anwenden**

In `Apply()` die Kette am Ende erweitern:

```csharp
        // Getrennte Gruppen, also UND: "gefunden, aber noch nicht behandelt".
        query = query
            .Where(r => ReasonFilter.Matches(_filter.Reasons, ReasonKey(r)))
            .Where(r => FlagStates.Matches(_filter.Found, r.IsFound, KpiFlags.Found))
            .Where(r => FlagStates.Matches(_filter.Treated, r.IsTreatet, KpiFlags.Treated))
            .Where(r => PlannedDateRanges.Matches(_filter.Range, r.AdministrationDate));
```

- [ ] **Schritt 9: `Planned_Cow_Table` — Auswahl in der Filterleiste**

Im `<MeadowFilterPopover>`-`ChildContent`, hinter dem `MeadowMultiSelect` „Medikament":

```razor
                <MeadowMultiSelect Label="Grund" Options="ReasonOptions"
                                   Selected="_filter.Reasons"
                                   SelectionChanged="Apply" />
```

- [ ] **Schritt 10: Bauen**

```bash
dotnet build BB_Cow.sln --nologo -v q
```

Erwartet: `Build succeeded`, keine Fehler.

- [ ] **Schritt 11: Commit**

```bash
git add 4Cows-FE/Components/Meadow/TableFilters.cs 4Cows-FE/Components/Pages/Tables/Cow_Table.razor 4Cows-FE/Components/Pages/Tables/Planned_Cow_Table.razor
git commit -m "feat: Grund-Filter mit Sentinel-Option Ohne Grund in beiden Kuh-Tabellen"
```

---

## Task 6: Basisdaten-Seite und Bearbeiten-Dialog

**Files:**
- Create: `4Cows-FE/Components/4CowsComponent/BaseData/BaseDataTreatmentReason.razor`
- Create: `4Cows-FE/Components/4CowsComponent/Dialogs/BaseData/EditTreatmentReasonDialog.razor`
- Modify: `4Cows-FE/Components/Pages/Settings.razor`

**Interfaces:**
- Consumes: `TreatmentReasonService` (`GetAllDataAsync`, `GetUsageCountsAsync`, `Reasons`, `InsertDataAsync`, `UpdateDataAsync`, `MergeAsync`, `RemoveByIdAsync`) aus Task 2.
- Produces: Komponente `EditTreatmentReasonDialog` mit den Parametern `TreatmentReason Entry`, `bool IsNew`, `int UsageCount`; schließt mit `DialogResult.Ok(true)` bei jeder erfolgreichen Änderung.

- [ ] **Schritt 1: Bearbeiten-Dialog anlegen**

Datei `4Cows-FE/Components/4CowsComponent/Dialogs/BaseData/EditTreatmentReasonDialog.razor`:

```razor
@using _4Cows_FE.Components.Meadow

@inject TreatmentReasonService TreatmentReasonService
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<MeadowDialog Title="@(IsNew ? "Behandlungsgrund anlegen" : "Behandlungsgrund ändern")" Size="MeadowDialogSize.Info">
    <ChildContent>
        @* MudForm wie im Wie/Wo-Dialog: die Required-Validierung samt
           RequiredError haengt daran, und meadow-mud.css toent die Fehlerzeile
           schon. *@
        <MudForm @ref="_form" @bind-IsValid="@_success">
            <label class="mw-label" for="mw-reason-name">Bezeichnung</label>
            <div class="mw-field">
                <MudTextField T="string" InputId="mw-reason-name" Variant="Variant.Text"
                              Required="true" @bind-Value="_name"
                              RequiredError="Bezeichnung muss ausgefüllt werden!" />
            </div>
        </MudForm>
        <p class="mw-hint">
            Gründe entstehen sonst nebenbei beim Erfassen einer Behandlung.
            Umbenennen auf einen bestehenden Grund führt beide zusammen.
        </p>
    </ChildContent>
    <Footer>
        <button type="button" class="mw-btn mw-btn--plain mw-btn--wide" @onclick="Cancel">
            Abbrechen
        </button>
        <button type="button" class="mw-btn mw-btn--primary mw-btn--wide" @onclick="Save">
            Speichern
        </button>
    </Footer>
</MeadowDialog>

@code {
    [CascadingParameter] private MudDialogInstance MudDialog { get; set; } = default!;

    [Parameter] public TreatmentReason Entry { get; set; } = new();
    [Parameter] public bool IsNew { get; set; }

    /// <summary>
    /// Verwendungen des bearbeiteten Eintrags - steht nur im Text der
    /// Merge-Rueckfrage, damit dort steht, wie viele Behandlungen umgehaengt
    /// werden.
    /// </summary>
    [Parameter] public int UsageCount { get; set; }

    private MudForm? _form;
    private bool _success;

    private string _name = "";

    protected override void OnInitialized()
    {
        // Auf einer Kopie arbeiten, nicht auf Entry: das ist die Instanz aus
        // dem Service-Cache. Direktes Binden wuerde den Cache schon beim Tippen
        // aendern, und ein Abbrechen liesse den halben Namen in der Tabelle
        // stehen, bis irgendwer neu laedt.
        _name = Entry.TreatmentReasonName;
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task Save()
    {
        if (_form is not null)
        {
            await _form.Validate();
        }

        if (!_success)
        {
            return;
        }

        var name = _name.Trim();
        if (name.Length == 0)
        {
            Snackbar.Add("Bezeichnung ist leer.", Severity.Error);
            return;
        }

        // Sich selbst ausnehmen: sonst meldet eine reine
        // Gross-/Kleinschreibungsaenderung ("mastitis" -> "Mastitis") eine
        // Kollision mit dem Eintrag, der gerade bearbeitet wird.
        var clash = TreatmentReasonService.Reasons.Values.FirstOrDefault(r =>
            r.TreatmentReasonId != Entry.TreatmentReasonId
            && string.Equals(r.TreatmentReasonName.Trim(), name, StringComparison.OrdinalIgnoreCase));

        if (clash is not null)
        {
            await SaveIntoExisting(clash);
            return;
        }

        await SaveOwn(name);
    }

    /// <summary>
    /// Der Name ist schon vergeben. Beim Bearbeiten wird zusammengefuehrt -
    /// beim Anlegen gibt es nichts zusammenzufuehren, das ist schlicht ein
    /// Eingabefehler.
    /// </summary>
    private async Task SaveIntoExisting(TreatmentReason clash)
    {
        if (IsNew)
        {
            Snackbar.Add($"„{clash.TreatmentReasonName}\" gibt es bereits.", Severity.Error);
            return;
        }

        var description = UsageCount == 0
            ? $"„{Entry.TreatmentReasonName}\" mit „{clash.TreatmentReasonName}\" zusammenführen? "
              + "Der Eintrag verschwindet danach."
            : $"„{Entry.TreatmentReasonName}\" wird in {UsageCount} Behandlungen verwendet. "
              + $"Mit „{clash.TreatmentReasonName}\" zusammenführen? Die Behandlungen werden "
              + "umgehängt, der Eintrag verschwindet danach.";

        if (!await Confirm("Zusammenführen", description, "Zusammenführen", Color.Warning))
        {
            return;
        }

        if (!await TreatmentReasonService.MergeAsync(Entry.TreatmentReasonId, clash.TreatmentReasonId))
        {
            Snackbar.Add("Zusammenführen fehlgeschlagen.", Severity.Error);
            return;
        }

        Snackbar.Add($"Mit „{clash.TreatmentReasonName}\" zusammengeführt.", Severity.Success);
        MudDialog.Close(DialogResult.Ok(true));
    }

    private async Task SaveOwn(string name)
    {
        var draft = new TreatmentReason(Entry.TreatmentReasonId, name);

        if (IsNew)
        {
            if (!await Confirm("Anlegen", $"„{name}\" als Behandlungsgrund anlegen?", "Anlegen", Color.Warning))
            {
                return;
            }

            if (!await TreatmentReasonService.InsertDataAsync(draft))
            {
                Snackbar.Add("Anlegen fehlgeschlagen.", Severity.Error);
                return;
            }

            Snackbar.Add("Behandlungsgrund angelegt.", Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
            return;
        }

        // Nichts geaendert: keine Rueckfrage, kein Schreibvorgang, kein Reload
        // auf der Seite dahinter.
        if (name == Entry.TreatmentReasonName)
        {
            MudDialog.Cancel();
            return;
        }

        if (!await TreatmentReasonService.UpdateDataAsync(draft))
        {
            Snackbar.Add("Ändern fehlgeschlagen.", Severity.Error);
            return;
        }

        Snackbar.Add("Behandlungsgrund geändert.", Severity.Success);
        MudDialog.Close(DialogResult.Ok(true));
    }

    private async Task<bool> Confirm(string header, string description, string accept, Color scheme)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var parameters = new DialogParameters<GenericDialog>
        {
            { x => x.HeaderText, header },
            { x => x.Description, description },
            { x => x.AcceptButtonText, accept },
            { x => x.CancelButtonText, "Abbrechen" },
            { x => x.ColorScheme, scheme }
        };

        var dialog = await DialogService.ShowAsync<GenericDialog>(string.Empty, parameters, options);
        var result = await dialog.Result;
        return result is not null && !result.Canceled;
    }
}
```

Der Dialog verwendet `GenericDialog` aus `_4Cows_FE.Components._4CowsComponent.Dialogs`. Falls der Compiler den Typ nicht findet, oben ergänzen:

```razor
@using _4Cows_FE.Components._4CowsComponent.Dialogs
```

- [ ] **Schritt 2: Pflegeseite anlegen**

Datei `4Cows-FE/Components/4CowsComponent/BaseData/BaseDataTreatmentReason.razor`:

```razor
@using _4Cows_FE.Components._4CowsComponent.Dialogs
@using _4Cows_FE.Components._4CowsComponent.Dialogs.BaseData
@using _4Cows_FE.Components.Meadow

@inject TreatmentReasonService TreatmentReasonService
@inject CowTreatmentService CowTreatmentService
@inject PCowTreatmentService PCowTreatmentService
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<MeadowToolbar>
    <Left>
        <MeadowSearchBox Value="@_search" ValueChanged="OnSearchChanged"
                         AriaLabel="Behandlungsgründe durchsuchen" />
    </Left>
</MeadowToolbar>

<MeadowTable TItem="Row" Items="_rows" Columns="_columns"
             RowKey="@(r => r.Entry.TreatmentReasonId)"
             Class="mw-table-wrap--dense" ShowMobileCards="false"
             DefaultSortTitle="Bezeichnung"
             PagerLabel="Einträge pro Seite"
             EmptyText="Keine Behandlungsgründe gefunden." />

<div class="mw-table-foot">
    <button type="button" class="mw-btn mw-btn--primary" @onclick="OpenNewDialog">
        <MeadowIcon Name="MeadowIconName.Plus" Size="20" />
        <span>Behandlungsgrund hinzufügen</span>
    </button>
</div>

@code {
    /// <summary>
    /// Eintrag plus abgeleitete Werte. Der Zaehler liegt am Item statt in einem
    /// Dictionary daneben, weil MeadowColumn.SortBy sonst bei jedem
    /// Sortiervergleich nachschlagen muesste.
    /// </summary>
    private sealed record Row(TreatmentReason Entry, int? UsageCount, bool IsDuplicate);

    private List<Row> _all = new();
    private List<Row> _rows = new();
    private string _search = "";

    private IReadOnlyList<MeadowColumn<Row>> _columns = Array.Empty<MeadowColumn<Row>>();

    protected override async Task OnInitializedAsync()
    {
        BuildColumns();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await TreatmentReasonService.GetAllDataAsync();

        // Null heisst "nicht zaehlbar" (Datenbank weg). Dann bleibt der
        // Papierkorb gesperrt, statt jede Zeile als unbenutzt auszuweisen.
        var counts = await TreatmentReasonService.GetUsageCountsAsync();

        var duplicates = TreatmentReasonService.Reasons.Values
            .GroupBy(r => r.TreatmentReasonName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _all = TreatmentReasonService.Reasons.Values
            .Select(r => new Row(
                r,
                counts?.GetValueOrDefault(r.TreatmentReasonId) ?? (int?)null,
                duplicates.Contains(r.TreatmentReasonName.Trim())))
            .ToList();

        ApplyFilter();
    }

    private void BuildColumns() => _columns = new List<MeadowColumn<Row>>
    {
        new()
        {
            Title = "ID",
            Text = r => r.Entry.TreatmentReasonId.ToString(),
            SortBy = r => r.Entry.TreatmentReasonId,
            Style = MeadowCellStyle.Mono,
            Width = "70px"
        },
        new()
        {
            Title = "Bezeichnung",
            SortBy = r => r.Entry.TreatmentReasonName,
            Style = MeadowCellStyle.Bold,
            Template = row =>
                @<text>
                    <span>@row.Entry.TreatmentReasonName</span>
                    @if (row.IsDuplicate)
                    {
                        <span class="mw-pill mw-pill--overdue"
                              title="Diese Bezeichnung gibt es mehrfach. Umbenennen auf den anderen Eintrag führt beide zusammen.">
                            doppelt
                        </span>
                    }
                </text>
        },
        new()
        {
            Title = "Verwendungen",
            Text = r => r.UsageCount?.ToString() ?? "–",
            SortBy = r => r.UsageCount ?? -1,
            Align = MeadowAlign.Right,
            Width = "140px"
        },
        new()
        {
            IsActions = true,
            Width = "104px",
            Template = row =>
                @<text>
                    <MeadowIconButton Icon="MeadowIconName.Edit" Title="Behandlungsgrund ändern"
                                      OnClick="() => OpenEditDialog(row)" />
                    <MeadowIconButton Icon="MeadowIconName.Trash" Title="@DeleteTitle(row)"
                                      Danger="true" Disabled="@(row.UsageCount is not 0)"
                                      OnClick="() => Delete(row)" />
                </text>
        }
    };

    private static string DeleteTitle(Row row) => row.UsageCount switch
    {
        null => "Verwendungen unbekannt — Löschen gesperrt",
        0 => "Behandlungsgrund löschen",
        1 => "Wird in 1 Behandlung verwendet",
        var n => $"Wird in {n} Behandlungen verwendet"
    };

    private void OnSearchChanged(string value)
    {
        _search = value;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var term = _search.Trim();
        _rows = term.Length == 0
            ? _all.ToList()
            : _all.Where(r => r.Entry.TreatmentReasonName
                    .Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }

    private Task OpenNewDialog() => OpenDialog(new TreatmentReason(), isNew: true, usageCount: 0);

    private Task OpenEditDialog(Row row)
        => OpenDialog(row.Entry, isNew: false, usageCount: row.UsageCount ?? 0);

    private async Task OpenDialog(TreatmentReason entry, bool isNew, int usageCount)
    {
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            BackdropClick = false
        };
        var parameters = new DialogParameters<EditTreatmentReasonDialog>
        {
            { x => x.Entry, entry },
            { x => x.IsNew, isNew },
            { x => x.UsageCount, usageCount }
        };

        var dialog = await DialogService.ShowAsync<EditTreatmentReasonDialog>(
            string.Empty, parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        // Ein Merge haengt Behandlungen um. Ohne dieses Nachladen zeigen die
        // Behandlungstabellen im selben Circuit noch die verschwundene ID und
        // damit ein leeres Feld.
        await CowTreatmentService.GetAllDataAsync();
        await PCowTreatmentService.GetAllDataAsync();

        await LoadAsync();
    }

    private async Task Delete(Row row)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var parameters = new DialogParameters<GenericDialog>
        {
            { x => x.HeaderText, "Löschen" },
            { x => x.Description, $"Soll „{row.Entry.TreatmentReasonName}\" wirklich gelöscht werden?" },
            { x => x.AcceptButtonText, "Löschen" },
            { x => x.CancelButtonText, "Abbrechen" },
            { x => x.ColorScheme, Color.Error }
        };

        var dialog = await DialogService.ShowAsync<GenericDialog>(string.Empty, parameters, options);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        if (!await TreatmentReasonService.RemoveByIdAsync(row.Entry.TreatmentReasonId))
        {
            // Der Service zaehlt vor dem Delete selbst nach. Ein false heisst
            // hier fast immer: zwischen Seitenaufbau und Klick ist eine
            // Behandlung dazugekommen.
            Snackbar.Add(
                $"„{row.Entry.TreatmentReasonName}\" wurde nicht gelöscht — der Eintrag wird inzwischen verwendet.",
                Severity.Error);
            await LoadAsync();
            return;
        }

        Snackbar.Add("Behandlungsgrund gelöscht.", Severity.Success);
        await LoadAsync();
    }
}
```

- [ ] **Schritt 3: Reiter in `Settings.razor` eintragen**

`BaseDataTabs` erweitern:

```csharp
    private static readonly string[] BaseDataTabs =
    {
        "Kuh Behandlung", "Klauen Behandlung", "Kühe", "Medikamente", "Wie / Wo", "Behandlungsgrund"
    };
```

Und im `@switch (_baseTab)` einen Fall **vor** `default:` ergänzen:

```csharp
        case "Behandlungsgrund":
            <BaseDataTreatmentReason />
            break;
```

- [ ] **Schritt 4: Bauen**

```bash
dotnet build BB_Cow.sln --nologo -v q
```

Erwartet: `Build succeeded`, keine Fehler. Häufigster Fehler hier: fehlendes `@using` für `GenericDialog` oder `MeadowAlign` — dann die `@using`-Zeilen mit `BaseDataWhereHow.razor` abgleichen.

- [ ] **Schritt 5: Commit**

```bash
git add 4Cows-FE/Components/4CowsComponent/BaseData/BaseDataTreatmentReason.razor 4Cows-FE/Components/4CowsComponent/Dialogs/BaseData/EditTreatmentReasonDialog.razor 4Cows-FE/Components/Pages/Settings.razor
git commit -m "feat: Basisdaten-Pflege fuer Behandlungsgruende mit Merge und Loeschsperre"
```

---

## Task 7: Demo-Daten

**Files:**
- Modify: `BBCowDataLibrary/SQL/DemoDataSeeder.cs`

**Interfaces:**
- Consumes: `TreatmentReason`, `DatabaseContext.TreatmentReasons`, die optionalen Ctor-Parameter aus Task 1.
- Produces: Demo-Bestand, in dem ein Teil der Kuh- und geplanten Kuhbehandlungen einen Grund trägt und ein Teil bewusst keinen.

- [ ] **Schritt 1: Namensliste ergänzen**

In `BBCowDataLibrary/SQL/DemoDataSeeder.cs` hinter dem Array `WhereHowNames`:

```csharp
    /// <summary>
    /// Beispielgruende NUR fuer die Demo. Produktive Installationen starten mit
    /// leerer Liste - DataSeeder legt hier bewusst nichts an, damit kein
    /// Betrieb fremde Begriffe wegraeumen muss.
    /// </summary>
    private static readonly string[] TreatmentReasonNames =
    {
        "Mastitis", "Lahmheit", "Fieber", "Nachgeburtsverhaltung",
        "Trockenstellen", "Impfung"
    };
```

- [ ] **Schritt 2: Lade-/Anlegemethode ergänzen**

Direkt hinter `EnsureWhereHowsAsync` einfügen:

```csharp
    private static async Task<List<int>> EnsureTreatmentReasonsAsync(DatabaseContext context)
    {
        if (!await context.TreatmentReasons.AnyAsync())
        {
            await context.TreatmentReasons.AddRangeAsync(
                TreatmentReasonNames.Select(n => new TreatmentReason(0, n)));
            await context.SaveChangesAsync();
        }

        return await context.TreatmentReasons.AsNoTracking()
            .Select(r => r.TreatmentReasonId)
            .ToListAsync();
    }
```

- [ ] **Schritt 3: In `SeedAsync` einhängen**

```csharp
    public static async Task SeedAsync(DatabaseContext context)
    {
        // Reihenfolge zaehlt: die Behandlungen brauchen die IDs der
        // Nachschlagetabellen.
        var udders = await EnsureUddersAsync(context);
        var medicineIds = await EnsureMedicinesAsync(context);
        var whereHows = await EnsureWhereHowsAsync(context);
        var reasonIds = await EnsureTreatmentReasonsAsync(context);

        await SeedHerdAndTreatmentsAsync(context, udders, medicineIds, whereHows, reasonIds);
    }
```

- [ ] **Schritt 4: Signatur von `SeedHerdAndTreatmentsAsync` erweitern**

```csharp
    private static async Task SeedHerdAndTreatmentsAsync(
        DatabaseContext context,
        IReadOnlyDictionary<(bool, bool, bool, bool), int> udders,
        IReadOnlyList<int> medicineIds,
        IReadOnlyList<WhereHow> whereHows,
        IReadOnlyList<int> reasonIds)
```

Und die beiden Aufrufe darin:

```csharp
        await context.CowTreatments.AddRangeAsync(
            BuildCowTreatments(random, today, active, medicineIds, whereHows, udders, reasonIds));
```

```csharp
        await context.PlannedCowTreatments.AddRangeAsync(
            BuildPlannedCowTreatments(random, today, active, medicineIds, whereHows, udders, reasonIds));
```

- [ ] **Schritt 5: `BuildCowTreatments` erweitern**

Signatur um einen Parameter ergänzen:

```csharp
    private static List<CowTreatment> BuildCowTreatments(
        Random random,
        DateTime today,
        IReadOnlyList<string> cowIds,
        IReadOnlyList<int> medicineIds,
        IReadOnlyList<WhereHow> whereHows,
        IReadOnlyDictionary<(bool, bool, bool, bool), int> udders,
        IReadOnlyList<int> reasonIds)
```

Innerhalb der `foreach`-Schleife vor dem `treatments.Add(...)`:

```csharp
            // Rund 70 Prozent mit Grund. Der Rest bleibt bewusst leer: nur so
            // zeigt die Demo das "–" in der Spalte und die Filteroption
            // "Ohne Grund" findet ueberhaupt etwas.
            int? reasonId = random.Next(100) < 70
                ? reasonIds[random.Next(reasonIds.Count)]
                : null;
```

Und der `Add`-Aufruf mit dem zusätzlichen Argument am Ende:

```csharp
            treatments.Add(new CowTreatment(
                0,
                cowIds[random.Next(cowIds.Count)],
                medicineIds[random.Next(medicineIds.Count)],
                date,
                Dosage(random),
                whereHow.WhereHowId,
                udderId,
                reasonId));
```

- [ ] **Schritt 6: `BuildPlannedCowTreatments` erweitern**

Signatur:

```csharp
    private static List<PlannedCowTreatment> BuildPlannedCowTreatments(
        Random random,
        DateTime today,
        IReadOnlyList<string> cowIds,
        IReadOnlyList<int> medicineIds,
        IReadOnlyList<WhereHow> whereHows,
        IReadOnlyDictionary<(bool, bool, bool, bool), int> udders,
        IReadOnlyList<int> reasonIds)
```

In der `for`-Schleife vor `planned.Add(...)`:

```csharp
            // Rund 60 Prozent mit Grund - siehe BuildCowTreatments.
            int? reasonId = random.Next(100) < 60
                ? reasonIds[random.Next(reasonIds.Count)]
                : null;
```

Und der `Add`-Aufruf:

```csharp
            planned.Add(new PlannedCowTreatment(
                0,
                cowIds[random.Next(cowIds.Count)],
                medicineIds[random.Next(medicineIds.Count)],
                today.AddDays(random.Next(-6, 15)),
                Dosage(random),
                whereHow.WhereHowId,
                isFound,
                isFound && random.Next(100) < 50,
                whereHow.ShowDialog ? quarterIds[random.Next(quarterIds.Count)] : noQuarter,
                reasonId));
```

- [ ] **Schritt 7: Bauen**

```bash
dotnet build BB_Cow.sln --nologo -v q
```

Erwartet: `Build succeeded`, keine Fehler.

- [ ] **Schritt 8: Commit**

```bash
git add BBCowDataLibrary/SQL/DemoDataSeeder.cs
git commit -m "feat: Beispielgruende in den Demo-Daten, ein Teil bewusst ohne Grund"
```

---

## Task 8: Durchlauf in der laufenden App

**Files:** keine — reine Verifikation.

Dieser Task ersetzt die automatisierten Tests, die es hier nicht geben kann. Jeder Punkt wird tatsächlich geklickt; ein „sieht plausibel aus" zählt nicht.

- [ ] **Schritt 1: Frische Demo-Datenbank**

```bash
docker compose -f docker-compose.dev.yml down -v
docker compose -f docker-compose.dev.yml up -d
```

Warten, bis `docker ps --filter name=4cows-dev-db` `(healthy)` meldet.

- [ ] **Schritt 2: App starten**

```bash
dotnet run --project 4Cows-FE --launch-profile http-demo
```

Erwartet: die Anwendung startet, wendet die Migration an und legt die Demo-Daten an. Im Log darf kein Fehler zu `Treatment_Reason` stehen.

- [ ] **Schritt 3: Bestand prüfen**

Auf `/Kuh_Daten`: die Spalte „Grund" existiert, ein Teil der Zeilen zeigt einen Grund, ein Teil zeigt `–`. Sortieren nach „Grund" funktioniert.

- [ ] **Schritt 4: Speichern ohne Grund**

„Neue Behandlung" → alle Pflichtfelder füllen, Behandlungsgrund **leer** lassen → Speichern.
Erwartet: keine Fehlermeldung zum Grund, die neue Zeile erscheint mit `–`.

- [ ] **Schritt 5: Speichern mit neuem Grund**

„Neue Behandlung" → als Grund einen Text eingeben, den es noch nicht gibt (z.B. „Klauenrehe") → Speichern.
Erwartet: die Zeile zeigt „Klauenrehe"; beim nächsten Öffnen des Dialogs steht der Grund im Autocomplete zur Auswahl.

- [ ] **Schritt 6: Planen und Abschließen**

Auf `/geplante_Kuh_Daten` eine Behandlung mit Grund planen, danach „Behandlung abschließen".
Erwartet: der Abschluss-Dialog hat den Grund **vorbelegt**; nach dem Speichern steht er in der Kuh-Tabelle.

- [ ] **Schritt 7: Filter**

Im Filter-Popover beider Tabellen:
- ein Grund gewählt → nur passende Zeilen, Badge zeigt eine aktive Gruppe mehr;
- zwei Gründe gewählt → Vereinigung beider Mengen;
- **„Ohne Grund"** gewählt → nur Zeilen, die `–` zeigen;
- „Ohne Grund" plus ein echter Grund → beide Mengen zusammen;
- „Zurücksetzen" leert auch die Grund-Auswahl.

- [ ] **Schritt 8: Basisdaten**

Unter `/Settings` → „Basisdaten" → „Behandlungsgrund":
- ein unbenutzter Grund lässt sich löschen;
- bei einem benutzten Grund ist der Papierkorb deaktiviert und der Tooltip nennt die Anzahl;
- Umbenennen funktioniert;
- Umbenennen auf einen bestehenden Namen fragt nach und führt zusammen; danach zeigen die Behandlungen des zusammengeführten Grundes den Zielnamen.

- [ ] **Schritt 9: Mobil**

Browserfenster auf Mobilbreite (375px) stellen und beide Tabellen prüfen: Karten mit Grund zeigen ihn, Karten ohne Grund lassen die Angabe weg statt ein `–` zu zeigen.

- [ ] **Schritt 10: Migration gegen Bestandsdaten**

Sicherstellen, dass die Migration auf eine **bestehende** Datenbank ohne Datenverlust läuft: Datenbank vor dem Umstieg sichern oder eine Kopie des Produktivstands verwenden, App starten, danach stichprobenartig prüfen, dass alte Behandlungen vollständig sind und `Treatment_Reason_ID` bei ihnen `NULL` ist.

```bash
docker exec 4cows-dev-db mariadb -uroot -padmin 4cows_v2 -e "SELECT COUNT(*) AS gesamt, COUNT(Treatment_Reason_ID) AS mit_grund FROM Cow_Treatment;"
```

- [ ] **Schritt 11: Abschluss**

Ergebnisse der Schritte 3–10 dem Nutzer berichten — mit dem, was tatsächlich beobachtet wurde, nicht mit „alles in Ordnung". Fehlgeschlagene Punkte benennen, bevor der Branch angeboten wird.

---

## Selbstprüfung des Plans

**Spec-Abdeckung**

| Spec-Abschnitt | Task |
| --- | --- |
| Neue Tabelle `Treatment_Reason` | 1 |
| Nullable FK-Spalten an beiden Behandlungstabellen | 1 |
| Migration, läuft über `MigrateAsync` | 1 |
| `TreatmentReasonService` samt Merge und Löschsperre | 2 |
| Registrierung, `MeadowDataLoader` | 2 |
| Autocomplete in beiden Dialogen, leer erlaubt | 3 |
| Vorbelegung beim Abschließen, ein Grund je Dialog | 3 |
| Spalte „Grund", Mobil-Karten | 4 |
| Filter mit „Ohne Grund", `ActiveCount`, `Reset` | 5 |
| Basisdaten-Seite, Edit-Dialog, Reiter | 6 |
| Demo-Daten, `DataSeeder` unverändert | 7 |
| Verifikation über Build und Durchlauf | 8 |
| Suchfeld unverändert | in keinem Task angefasst — so gewollt |
| Klauenbehandlungen unberührt | in keinem Task angefasst — so gewollt |

**Typkonsistenz**

- `TreatmentReasonId` ist überall `int?`; `GetNameById` nimmt `int?`, `GetIdByNameAsync` gibt `int` mit `int.MinValue` als Fehlerwert.
- `ReasonKey(...)` liefert `string?`, `Reason(...)` liefert `string` — Task 4 definiert beide, Task 5 verwendet nur `ReasonKey`.
- `ReasonFilter.Matches(HashSet<string>, string?)` passt zu `_filter.Reasons` (`HashSet<string>`) und `ReasonKey` (`string?`).
- `EditTreatmentReasonDialog` erwartet `TreatmentReason Entry`, `bool IsNew`, `int UsageCount` — genau die drei Parameter setzt `BaseDataTreatmentReason.OpenDialog`.
