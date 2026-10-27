# Horizon-Agent — Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein Windows-Dienst auf dem Lely-Horizon-PC liest die Tiere direkt aus der Horizon-SQL-Datenbank und schickt sie als vollständigen Snapshot an Meadow. Er ersetzt den HTML-Scraper `XLinkService` als Quelle des Kuh-Abgleichs. `Cow` bekommt dabei die stabile Lely-Animal-ID sowie **Ort**, **Gruppe** und **Beschreibung** aus Horizon.

**Architecture:**
- **Agent** (`Meadow.HorizonAgent`): liest nur und sendet nur.
- **Abgleichsregeln**: reine Funktion `HorizonSyncPlanner.Plan` in `Meadow.Shared`, dort getestet.
- **`HorizonSyncService`** (`Meadow.Data`): führt die Aktionen über die schmale Naht `IHorizonCowStore` aus, die `CowService` implementiert. `ICowService` und `HttpCowService` bleiben unverändert.
- **Quelle umschaltbar**: Die Umgebungsvariable `CowSyncSource` wählt zwischen `HorizonAgent` (Standard) und `XLink`. Die gemeinsame Naht heißt `ICowSyncService` und ersetzt `IXLinkService`.

**Tech Stack:**
- Server und Client: .NET 8, Blazor WebAssembly (`Meadow.Client`), gehostet von `Meadow.Api` (Minimal API), MudBlazor 7.15, EF Core 8 mit Pomelo/MariaDB 11.4 (`Meadow.Data`), xUnit 2.5.3 mit plain `Assert` (`Meadow.Data.Tests`).
- Agent: .NET 8 Worker Service mit `Microsoft.Data.SqlClient`, `Microsoft.Extensions.Hosting.WindowsServices`, EventLog.

**Spec:** `docs/superpowers/specs/2026-10-21-horizon-agent-design.md`. Der **Nachtrag vom 2026-10-22** am Ende geht den Abschnitten davor vor. Im Zweifel gilt die Spec.

**Schema-Referenz:** Artefakt „Lely Horizon DB-Referenz“, https://claude.ai/artifact/FTKEikjQF8AbSevLVucy6p

## Global Constraints

- **Ticket-Schnitt:** Jeder Task ist **ein Ticket** auf dem Meadow-Board (MDW, Board 3). Die Zeile „Board“ je Task nennt Titel, Abhängigkeiten und Abnahme.
- **Git:** Im Dispatcher-Lauf auf dem Ticket-Branch committen. Nicht pushen, nicht mergen.
- **Sprache:**
  - sichtbare Texte Deutsch **mit** Umlauten
  - Codekommentare Deutsch **ohne** Umlaute (`Abgleich`, `fuer`), wie im restlichen Code
  - Testnamen englisch mit Unterstrichen
- **Spaltennamen:** `Snake_Case` per `[Column]`, keine Fremdschlüssel.
- **`Meadow.Shared` bleibt paketfrei** (siehe `Meadow.Shared.csproj`). BCL-Typen wie `System.Security.Cryptography` sind erlaubt.
- **Zeit ist Parameter:** Logik in `Meadow.Shared` und `Meadow.Data` bekommt die Zeit über `TimeProvider` bzw. als Parameter, nie über `DateTime.Now`.
- **Horizon-DB nie schreiben:**
  - Jede Verbindung beginnt mit `EXECUTE AS USER = 'BenchmarkReadOnly' WITH NO REVERT;`.
  - `Pooling=false`.
  - Kein `NOLOCK`, kein `READ UNCOMMITTED`.
- **EF-Kommandos:** `dotnet ef migrations add <Name> --project Meadow.Data --startup-project Meadow.Data`.
  - Die Dev-DB muss laufen: `docker compose -f docker-compose.dev.yml up -d`, Container `4cows-dev-db`.
  - Migrationen laufen beim Start von `Meadow.Api` automatisch (`Program.cs:153`).
- **Laufende App sperrt `bin/`:** Scheitert `dotnet build` mit MSB3021/MSB3027, mit `-p:BaseOutputPath=<scratch>/` bauen. Fremde `Meadow.Api`- oder `4Cows-FE`-Prozesse **nie** beenden, nur eigene PIDs.
- **Tests:**
  - `dotnet test Meadow.Data.Tests --filter "FullyQualifiedName~<Klasse>"`
  - vor Abschluss jedes Tasks `dotnet test Meadow.Data.Tests` komplett grün
  - ab Task 8 zusätzlich `dotnet test Meadow.HorizonAgent.Tests`

---

## Abhängigkeiten der Tickets

```
1 ─┬─ 2 ─┐
   ├─ 3 ─┴─ 4 ── 5 ─┬─ 6 ─┐
   │                 └─ 7 ─┤
   └─ 8 ── 9 ──────────────┴─ 10 ── 11 (manuell)
```

Parallel: 2 ∥ 3 ∥ 8, danach 6 ∥ 7 ∥ 9.

---

## Task 1: Kuh-Felder, Migration und Verträge

**Board:** „Horizon-Agent 1/11: Kuh-Felder, Migration und Verträge“. Abhängig von: –.

**Abnahme:**
- `Cow` hat `LelyAnimalId`, `Location`, `Group` und `Description`.
- Die Migration `AddHorizonFieldsToCow` legt in der Dev-DB die Spalten `Lely_Animal_Id` varchar(64), `Location` varchar(100), `Group_Name` varchar(100) und `Description` varchar(500) an, alle NULL. Dazu den UNIQUE-Index `IX_Cow_Lely_Animal_Id` (`SHOW INDEX FROM Cow`: Non_unique 0).
- Ein zweiter Lauf der Migration-SQL scheitert nicht (idempotent).
- Die Verträge unter `Meadow.Shared/Models/Horizon/` sind vorhanden.
- `GET /api/cows` liefert die neuen Felder (null).
- `dotnet test Meadow.Data.Tests` ist grün.

**Files:**
- Modify: `Meadow.Shared/Models/Cow.cs` (Properties nach `IsGone`)
- Modify: `Meadow.Data/SQL/DatabaseContext.cs:56-59` (zweiter Index neben `IX_Cow_Ear_Tag_Number`)
- Create: `Meadow.Data/Migrations/*_AddHorizonFieldsToCow.cs` (generiert, dann idempotent umgeschrieben), Snapshot aktualisiert
- Create: `Meadow.Shared/Models/Horizon/HorizonContracts.cs`

**Interfaces (Produces):**

```csharp
// Meadow.Shared/Models/Cow.cs
// Die Lely-interne Animal-ID (HemAnimal.AniId) - der stabile Schluessel des Horizon-Abgleichs.
// Null bei Kuehen, die Horizon (noch) nicht zugeordnet sind.
[StringLength(64)]
[Column("Lely_Animal_Id")]
public string? LelyAnimalId { get; set; }

// Stallbereich aus Horizon (SytLocation.LocName). Nur der Horizon-Abgleich schreibt.
[StringLength(100)]
[Column("Location")]
public string? Location { get; set; }

// Gruppe aus Horizon (HemGroup.GroName). Spalte Group_Name, weil GROUP ein SQL-Schluesselwort ist.
[StringLength(100)]
[Column("Group_Name")]
public string? Group { get; set; }

// Beschreibung aus Horizon (HemAnimal.AniDescription).
[StringLength(500)]
[Column("Description")]
public string? Description { get; set; }
```

```csharp
// Meadow.Shared/Models/Horizon/HorizonContracts.cs
namespace Meadow.Shared.Models.Horizon;

/// <summary>Ein Tier aus Horizon, wie der Agent es liest.</summary>
public sealed class HorizonAnimal
{
    public string AnimalId { get; set; } = "";
    public int CollarNumber { get; set; }
    public string? EarTag { get; set; }
    public bool IsGone { get; set; }
    public string? Location { get; set; }
    public string? Group { get; set; }
    public string? Description { get; set; }
}

public sealed class HorizonSnapshot
{
    public string AgentVersion { get; set; } = "";
    public string MachineName { get; set; } = "";
    public DateTimeOffset ReadAtUtc { get; set; }
    public List<HorizonAnimal> Animals { get; set; } = new();
}

public sealed class AgentHeartbeat
{
    public string AgentVersion { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string? LastError { get; set; }
}

public sealed class AgentHeartbeatResponse
{
    public bool SyncRequested { get; set; }
}

/// <summary>Antwort auf POST /api/horizon/snapshot.</summary>
public sealed class HorizonSyncResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Linked { get; set; }
    public int Promoted { get; set; }
    public int Unchanged { get; set; }
    public int SkippedGone { get; set; }
    public List<string> Warnings { get; set; } = new();
    /// <summary>Gesetzt, wenn die Schutzregel den Snapshot abgelehnt hat; dann wurde nichts geschrieben.</summary>
    public string? RejectedReason { get; set; }
    public bool Accepted => RejectedReason is null;
}
```

- [ ] **Schritt 1: Modell.** Die vier Properties in `Cow.cs` nach `IsGone` einfügen, mit den Kommentaren von oben. Die Konstruktoren bleiben unverändert.
- [ ] **Schritt 2: Verträge.** `HorizonContracts.cs` wie oben anlegen.
- [ ] **Schritt 3: Index.** In `DatabaseContext.OnModelCreating` direkt unter dem Ohrmarken-Index ergänzen:

```csharp
        // Wie die Ohrmarke: eindeutig, aber mehrfach NULL erlaubt (MariaDB-UNIQUE),
        // damit alle noch nicht zugeordneten Kuehe nebeneinander stehen koennen.
        modelBuilder.Entity<Cow>()
            .HasIndex(c => c.LelyAnimalId)
            .IsUnique()
            .HasDatabaseName("IX_Cow_Lely_Animal_Id");
```

- [ ] **Schritt 4: Migration erzeugen.** `dotnet ef migrations add AddHorizonFieldsToCow --project Meadow.Data --startup-project Meadow.Data`. Erwartet: viermal `AddColumn` auf `Cow` und ein `CreateIndex`. Kommt etwas anderes heraus, die Migration entfernen und die Ursache klären.
- [ ] **Schritt 5: Idempotent machen.** MariaDB-DDL ist nicht transaktional (siehe Kopfkommentar von `20260816120000_AddCowIdAndIsCalv.cs`). Den generierten `Up`-Rumpf durch geschütztes SQL ersetzen. MariaDB 11.4 kann `IF NOT EXISTS`:

```csharp
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: MariaDB-DDL committet je Anweisung, ein Abbruch in der Mitte
            // liesse sonst einen Teilstand zurueck, an dem der naechste Start scheitert.
            migrationBuilder.Sql("ALTER TABLE `Cow` ADD COLUMN IF NOT EXISTS `Lely_Animal_Id` varchar(64) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `Cow` ADD COLUMN IF NOT EXISTS `Location` varchar(100) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `Cow` ADD COLUMN IF NOT EXISTS `Group_Name` varchar(100) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `Cow` ADD COLUMN IF NOT EXISTS `Description` varchar(500) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_Cow_Lely_Animal_Id` ON `Cow` (`Lely_Animal_Id`);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS `IX_Cow_Lely_Animal_Id` ON `Cow`;");
            migrationBuilder.Sql("ALTER TABLE `Cow` DROP COLUMN IF EXISTS `Description`;");
            migrationBuilder.Sql("ALTER TABLE `Cow` DROP COLUMN IF EXISTS `Group_Name`;");
            migrationBuilder.Sql("ALTER TABLE `Cow` DROP COLUMN IF EXISTS `Location`;");
            migrationBuilder.Sql("ALTER TABLE `Cow` DROP COLUMN IF EXISTS `Lely_Animal_Id`;");
        }
```

Die Collation an die übrigen `Cow`-Spalten anpassen. Dazu `SHOW FULL COLUMNS FROM Cow` in der Dev-DB ansehen. `Designer.cs` und `DatabaseContextModelSnapshot.cs` bleiben so, wie EF sie erzeugt hat.

- [ ] **Schritt 6: Prüfen.**
  - Build.
  - Eigene Instanz von `Meadow.Api` starten (eigene DB, z. B. `DB_DB=4cows_mdw_horizon`), damit die Migration läuft.
  - `docker exec 4cows-dev-db mariadb -uroot -p<pw> <db> -e "SHOW FULL COLUMNS FROM Cow; SHOW INDEX FROM Cow;"`. Erwartet: vier neue Spalten, Index `IX_Cow_Lely_Animal_Id` mit `Non_unique = 0`.
  - Den `Up`-SQL ein zweites Mal von Hand ausführen. Erwartet: keine Fehler.
  - `curl -s http://localhost:<port>/api/cows` liefert `lelyAnimalId`, `location`, `group` und `description`.
  - `dotnet test Meadow.Data.Tests` ist grün.
- [ ] **Schritt 7: Commit** – `git commit -m "Horizon-Agent: Kuh-Felder, Migration und Vertraege"`

---

## Task 2: Schreibnaht `IHorizonCowStore` im CowService

**Board:** „Horizon-Agent 2/11: Schreibnaht IHorizonCowStore im CowService“. Abhängig von: 1.

**Abnahme:**
- `CowService` implementiert `IHorizonCowStore`, und `ApplyHorizonAsync` schreibt in einem `ExecuteUpdate` und aktualisiert den Cache.
- `Program.cs` registriert `CowService` einmal und reicht dieselbe Instanz als `ICowService` und `IHorizonCowStore` aus.
- `CowLookups.GetByLelyAnimalId` ist mit Tests in `CowLookupsTests` abgedeckt.
- `ICowService` und `HttpCowService` sind unverändert.
- `dotnet test Meadow.Data.Tests` ist grün.

**Files:**
- Create: `Meadow.Data/Services/IHorizonCowStore.cs`
- Modify: `Meadow.Data/Services/CowService.cs` (Klasse implementiert zusätzlich `IHorizonCowStore`; neue Methode nach `PromoteCalfAsync`, Z. ~183-214)
- Modify: `Meadow.Shared/Lookups/CowLookups.cs` (neue Methode neben `GetByEarTagNumber`, Z. 48-55)
- Modify: `Meadow.Data.Tests/Lookups/CowLookupsTests.cs`
- Modify: `Meadow.Api/Program.cs:87`

**Interfaces (Produces):**

```csharp
// Meadow.Data/Services/IHorizonCowStore.cs
namespace Meadow.Data.Services;

/// <summary>
/// Die schmale Schreibnaht fuer den Horizon-Abgleich. Absichtlich NICHT auf ICowService:
/// die Naht teilt sich der Browser (HttpCowService), und dort gibt es keinen Abgleich.
/// </summary>
public interface IHorizonCowStore
{
    IReadOnlyDictionary<string, Cow> Cows { get; }
    Task GetAllDataAsync();
    Task<bool> InsertDataAsync(Cow cow);
    Task<bool> PromoteCalfAsync(string cowId, string earTagNumber);

    /// <summary>
    /// Uebernimmt die Horizon-Werte fuer eine Kuh in EINEM Update: Lely-ID, Halsband,
    /// IsGone, Ort, Gruppe, Beschreibung, und die Ohrmarke nur, wenn earTag nicht leer ist
    /// (eine Ohrmarke wird nie geloescht).
    /// </summary>
    Task<bool> ApplyHorizonAsync(string cowId, string lelyAnimalId, int collarNumber, string? earTag,
        bool isGone, string? location, string? group, string? description);
}
```

```csharp
// CowLookups
public static Cow GetByLelyAnimalId(IReadOnlyDictionary<string, Cow> cows, string lelyAnimalId)
```

- [ ] **Schritt 1: Vorbild lesen.** `CowService.UpdateIsGoneAsync` und `PromoteCalfAsync` lesen: `ExecuteUpdateAsync`, `_databaseStatusService.ReportSuccess/Failure`, Cache-Pflege mit `_cachedCows.SetItem`, `LoggerService`. Die neue Methode baut genau so.
- [ ] **Schritt 2: Lookup und Test zuerst.** In `CowLookupsTests` ergänzen:
  - `Lely_id_lookup_finds_the_cow_by_field_not_by_key`: Kuh mit `LelyAnimalId = "4711"`, Cow_ID = Ohrmarke, wird gefunden.
  - `Lely_id_lookup_returns_null_for_blank_or_unknown`

  Rot laufen lassen, dann implementieren:

```csharp
    // Sucht ueber das FELD Lely_Animal_Id (nicht ueber den Cow_ID-Schluessel). Ordinal, weil die
    // ID aus Horizon eine Zahl als Text ist und keine Schreibvarianten kennt.
    public static Cow GetByLelyAnimalId(IReadOnlyDictionary<string, Cow> cows, string lelyAnimalId)
    {
        if (string.IsNullOrWhiteSpace(lelyAnimalId))
        {
            return null;
        }
        return cows.Values.FirstOrDefault(c => string.Equals(c.LelyAnimalId, lelyAnimalId, StringComparison.Ordinal));
    }
```

- [ ] **Schritt 3: Naht.** `IHorizonCowStore.cs` wie oben anlegen.
- [ ] **Schritt 4: CowService.** `public class CowService : ICowService, IHorizonCowStore`, dazu `IReadOnlyDictionary<string, Cow> IHorizonCowStore.Cows => _cachedCows;` explizit. `ApplyHorizonAsync` nach dem Muster von `PromoteCalfAsync`:
  - Ist `earTag` leer, bleibt `EarTagNumber` unverändert.
  - Bekommt ein Kalb (`IsCalv`) eine Ohrmarke, wird `IsCalv = false` gesetzt.

  Der Cache bekommt dieselben Werte. Im Fehlerfall `ReportFailure` und `false`.

```csharp
                var hasEarTag = !string.IsNullOrWhiteSpace(earTag);
                var affectedRows = await context.Cows
                    .Where(c => c.CowId == cowId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.LelyAnimalId, lelyAnimalId)
                        .SetProperty(c => c.CollarNumber, collarNumber)
                        .SetProperty(c => c.EarTagNumber, c => hasEarTag ? earTag : c.EarTagNumber)
                        .SetProperty(c => c.IsCalv, c => hasEarTag ? false : c.IsCalv)
                        .SetProperty(c => c.IsGone, isGone)
                        .SetProperty(c => c.Location, location)
                        .SetProperty(c => c.Group, group)
                        .SetProperty(c => c.Description, description));
```

- [ ] **Schritt 5: Registrierung.** In `Meadow.Api/Program.cs:87` `AddSingleton<ICowService, CowService>()` ersetzen, damit es nur **eine** Instanz und damit einen Cache gibt:

```csharp
// Eine Instanz, zwei Nahten: ICowService fuer alle Endpunkte, IHorizonCowStore fuer den
// Horizon-Abgleich. Zwei AddSingleton<..., CowService> ergaeben zwei Caches.
builder.Services.AddSingleton<CowService>();
builder.Services.AddSingleton<ICowService>(sp => sp.GetRequiredService<CowService>());
builder.Services.AddSingleton<IHorizonCowStore>(sp => sp.GetRequiredService<CowService>());
```

- [ ] **Schritt 6: Prüfen.** `dotnet build Meadow.sln` ohne Fehler, `dotnet test Meadow.Data.Tests` grün. App starten, `GET /api/cows` antwortet weiter.
- [ ] **Schritt 7: Commit** – `git commit -m "Horizon-Agent: Schreibnaht IHorizonCowStore"`

---

## Task 3: Abgleichslogik `HorizonSyncPlanner`

**Board:** „Horizon-Agent 3/11: Abgleichslogik HorizonSyncPlanner mit Tests“. Abhängig von: 1.

**Abnahme:**
- `HorizonSyncPlanner.Plan` in `Meadow.Shared/Lookups/HorizonSyncPlanner.cs` setzt die Regeln 1–5 und die Schutzregel der Spec um, mit Ort, Gruppe und Beschreibung.
- `HorizonSyncPlannerTests` decken alle unten genannten Fälle ab und sind grün.
- `dotnet test Meadow.Data.Tests` ist komplett grün.

**Files:**
- Create: `Meadow.Shared/Lookups/HorizonSyncPlanner.cs`
- Create: `Meadow.Data.Tests/Lookups/HorizonSyncPlannerTests.cs`

**Interfaces (Produces):**

```csharp
namespace Meadow.Shared.Lookups;

public enum HorizonActionKind { Update, Link, Promote, Create }

/// <param name="CowId">Bestehende Cow_ID (Update/Link/Promote) bzw. neue Cow_ID (Create).</param>
/// <param name="NewCow">Nur bei Create: die anzulegende Kuh, alle Felder gesetzt.</param>
public sealed record HorizonCowAction(HorizonActionKind Kind, string CowId, HorizonAnimal Animal, Cow? NewCow);

public sealed record HorizonSyncPlan(
    string? RejectedReason,
    IReadOnlyList<HorizonCowAction> Actions,
    int Unchanged,
    int SkippedGone,
    IReadOnlyList<string> Warnings);

public static class HorizonSyncPlanner
{
    public const int MassGoneMinActiveCows = 10;

    /// <param name="newCowId">Erzeugt die Cow_ID fuer neue Tiere ohne Ohrmarke (Standard: neue GUID).</param>
    public static HorizonSyncPlan Plan(IReadOnlyCollection<Cow> cows, HorizonSnapshot snapshot, Func<string>? newCowId = null);
}
```

**Regeln** (Reihenfolge je Tier; Spec, Abschnitt „Abgleich“):
- **Normalisierung:** `AnimalId`, `EarTag`, `Location`, `Group` und `Description` per `Trim()`, leer wird `null`. Vergleiche `StringComparison.Ordinal`. Die Ohrmarke wird zusätzlich `OrdinalIgnoreCase` gegen `EarTagNumber` geprüft (wie im `XLinkService`-HashSet).
- **Ablehnung vor allem anderen** (`RejectedReason` gesetzt, `Actions` leer):
  - Snapshot leer: „Der Snapshot enthält keine Tiere.“
  - Eine `AnimalId` leer: „Ein Tier ohne AnimalId.“
  - Eine `AnimalId` doppelt: „AnimalId {id} kommt mehrfach vor.“
  - **Massenabgang:** `active` = Meadow-Kühe mit `!IsGone`. `goingGone` = Aktionen, die eine heute aktive Kuh auf `IsGone = true` setzen. Abgelehnt wird, wenn `active >= 10 && goingGone * 2 > active`, mit dem Text „{goingGone} von {active} aktiven Kühen würden abgehen – abgelehnt.“ Geprüft wird **nach** dem Planen der Aktionen.
- **Regel 1:** Kuh mit `LelyAnimalId == AnimalId` → `Update`, wenn sich ein Feld unterscheidet, sonst `Unchanged++`. Verglichen werden `CollarNumber`, `IsGone`, `Location`, `Group`, `Description` und die Ohrmarke, Letztere nur wenn Horizon eine liefert.
- **Regel 2:** Sonst, wenn `EarTag` gesetzt: Kuh **ohne** Lely-ID mit dieser Ohrmarke → `Link`.
- **Regel 3:** Sonst, nur bei `!IsGone && CollarNumber > 0`: Kalb (`IsCalv && !IsGone`, Ohrmarke leer, ohne Lely-ID, gleiche Halsbandnummer).
  - Mit `EarTag` → `Promote`.
  - Ohne `EarTag` → `Link`: Das Kalb bleibt Kalb und bekommt nur die ID.
- **Regel 4:** Sonst, nur bei `!IsGone` → `Create`. `NewCow`:
  - mit Ohrmarke: `new Cow(earTag, collar, false)`
  - ohne Ohrmarke: `new Cow(newCowId(), null, collar, isCalv: true, isGone: false)`

  Jeweils mit `LelyAnimalId`, `Location`, `Group` und `Description`.
- **Regel 5:** Sonst `SkippedGone++`.
- **Kollisionen:**
  - Jede Meadow-Kuh wird höchstens einmal getroffen. Bereits vergebene Cow_IDs merken und bei Regel 2 und 3 überspringen.
  - Eine Ohrmarke, die schon eine **andere** Kuh trägt (auch eine in diesem Lauf neu angelegte), wird nicht übernommen. Es gibt eine Warnung „Ohrmarke {x} von Tier {id} ist bereits vergeben – nicht übernommen.“ Bei `Create` mit Ohrmarke, deren Cow_ID schon existiert, wird ohne Ohrmarke als Kalb angelegt, ebenfalls mit Warnung.

- [ ] **Schritt 1: Tests zuerst** in `HorizonSyncPlannerTests` (Hilfsfabriken wie in `CowLookupsTests`, `newCowId: () => "guid-1"`):
  1. `Empty_snapshot_is_rejected`
  2. `Duplicate_animal_id_is_rejected`
  3. `Blank_animal_id_is_rejected`
  4. `Known_lely_id_updates_changed_fields_only`: geändertes Halsband und geänderter Ort ergeben `Update`. Identische Werte ergeben `Unchanged = 1` und keine Aktion.
  5. `Known_lely_id_wins_even_after_ear_tag_and_collar_changed`
  6. `Cow_without_lely_id_is_linked_by_ear_tag_once`
  7. `Link_by_ear_tag_ignores_cows_that_already_have_a_lely_id`
  8. `Active_animal_promotes_calf_by_collar_number`
  9. `Calf_is_linked_but_not_promoted_when_horizon_has_no_ear_tag`
  10. `Gone_animal_never_promotes_a_calf`
  11. `New_active_animal_with_ear_tag_is_created_with_ear_tag_as_cow_id`
  12. `New_active_animal_without_ear_tag_is_created_as_calf_with_guid`
  13. `Gone_animal_without_counterpart_is_skipped`
  14. `IsGone_is_taken_over_in_both_directions`
  15. `Meadow_cow_missing_from_snapshot_gets_no_action`
  16. `Location_group_description_are_taken_over_and_null_clears_them`
  17. `Missing_ear_tag_in_horizon_does_not_clear_meadow_ear_tag`
  18. `Mass_gone_above_half_of_at_least_ten_active_cows_is_rejected`
  19. `Mass_gone_below_ten_active_cows_is_allowed`
  20. `Ear_tag_already_used_by_other_cow_is_not_taken_over_and_warned`
  21. `Two_animals_never_match_the_same_meadow_cow`

  Rot laufen lassen: `dotnet test Meadow.Data.Tests --filter "FullyQualifiedName~HorizonSyncPlannerTests"`.
- [ ] **Schritt 2: Implementieren.** Reine Funktion, kein I/O, keine Zeit. Die Herde vorab indizieren: Dictionary Lely-ID → Kuh, Ohrmarke → Kuh (OrdinalIgnoreCase), Kälber je Halsband. Dann die Tiere in Snapshot-Reihenfolge durchgehen.
- [ ] **Schritt 3: Grün.** Gefilterter Lauf grün, danach `dotnet test Meadow.Data.Tests` komplett.
- [ ] **Schritt 4: Commit** – `git commit -m "Horizon-Agent: Abgleichslogik HorizonSyncPlanner"`

---

## Task 4: `HorizonSyncService`

**Board:** „Horizon-Agent 4/11: HorizonSyncService führt den Abgleich aus“. Abhängig von: 2, 3.

**Abnahme:**
- `HorizonSyncService` in `Meadow.Data/Services` wendet einen Snapshot über `IHorizonCowStore` an und liefert ein `HorizonSyncResult` mit den Zählern.
- Bei einer Ablehnung schreibt er nichts und setzt `LastSyncSucceeded = false` mit Begründung.
- Er hält den Status des Agenten (Version, Rechner, letztes Lebenszeichen, Tierzahl, Fehler) und das Flag „Abgleich angefordert“. Das Flag läuft nach 3 Minuten mit Fehlermeldung ab.
- `HorizonSyncServiceTests` mit Fake-Store und Fake-Zeit sind grün, `dotnet test Meadow.Data.Tests` ist komplett grün.

**Files:**
- Create: `Meadow.Data/Services/HorizonSyncService.cs`
- Create: `Meadow.Data.Tests/BaseData/HorizonSyncServiceTests.cs` (mit `FakeHorizonCowStore` und `ManualTimeProvider : TimeProvider` im selben File)
- Modify: `Meadow.Api/Program.cs` (Registrierung `AddSingleton<HorizonSyncService>()` neben `XLinkService` Z. ~100, plus `AddSingleton(TimeProvider.System)`, falls noch nicht vorhanden)

**Interfaces (Produces):**

```csharp
public sealed class HorizonSyncService
{
    public static readonly TimeSpan SyncRequestTimeout = TimeSpan.FromMinutes(3);
    public const string AgentNoAnswer = "Der Horizon-Agent hat nicht geantwortet.";

    public HorizonSyncService(IHorizonCowStore store, TimeProvider time);

    public DateTimeOffset? LastSyncUtc { get; }
    public bool LastSyncSucceeded { get; }
    public string? LastSyncError { get; }
    public string? AgentVersion { get; }
    public string? AgentMachineName { get; }
    public DateTimeOffset? LastHeartbeatUtc { get; }
    public int? LastSnapshotAnimalCount { get; }
    public string? AgentLastError { get; }

    /// <summary>Wirft nur bei technischen Fehlern; Schutzregel -> Result mit RejectedReason.</summary>
    public Task<HorizonSyncResult> ApplySnapshotAsync(HorizonSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Merkt das Lebenszeichen und liefert (und verbraucht) das Flag "angefordert".</summary>
    public AgentHeartbeatResponse RecordHeartbeat(AgentHeartbeat heartbeat);

    public void RequestSync();

    /// <summary>true, solange eine Anforderung noch nicht durch einen Snapshot beantwortet ist (max. 3 min).</summary>
    public bool IsSyncPending { get; }
}
```

**Verhalten:**
- `ApplySnapshotAsync`:
  - Läuft unter `SemaphoreSlim(1,1)`, damit zwei Snapshots nicht auf demselben Cache arbeiten.
  - Zuerst `store.GetAllDataAsync()`, dann `HorizonSyncPlanner.Plan(store.Cows.Values.ToList(), snapshot)`.
  - Bei Ablehnung: `LastSyncUtc = now`, `LastSyncSucceeded = false`, `LastSyncError = reason`, Result mit `RejectedReason` zurück, **kein** Store-Aufruf.
  - Sonst je Aktion:
    - `Update` und `Link`: `ApplyHorizonAsync`
    - `Promote`: `PromoteCalfAsync`, dann `ApplyHorizonAsync`
    - `Create`: `InsertDataAsync(NewCow)`
  - Liefert ein Store-Aufruf `false`, wird das als Warnung „Kuh {id}: Schreiben fehlgeschlagen.“ vermerkt. Der Lauf geht weiter, `LastSyncSucceeded` ist am Ende `false` mit „{n} Kühe konnten nicht geschrieben werden.“
  - Danach: `LastSnapshotAnimalCount`, Erfolg, Anforderung zurücksetzen (`_syncRequestedAt = null`).
  - Bei einer Exception wie im `XLinkService`: vermerken, dann weiterwerfen.
- `RecordHeartbeat`: Felder setzen (`AgentLastError = heartbeat.LastError`), `SyncRequested = _syncRequestedAt is not null && !_requestDelivered`, danach `_requestDelivered = true`. Mehrere Heartbeats melden die Anforderung also nur einmal.
- `RequestSync`: `_syncRequestedAt = now`, `_requestDelivered = false`.
- `IsSyncPending`: läuft die Anforderung ab (`now - _syncRequestedAt > SyncRequestTimeout`), wird sie gelöscht. Dann gilt `LastSyncUtc = now`, `LastSyncSucceeded = false`, `LastSyncError = AgentNoAnswer`, und die Eigenschaft ist `false`.

- [ ] **Schritt 1: Tests zuerst** (`HorizonSyncServiceTests`):
  - `Accepted_snapshot_writes_through_the_store_and_counts`: Create, Update, Link und Promote je einmal, Zähler stimmen, Store-Aufrufe protokolliert.
  - `Rejected_snapshot_writes_nothing_and_records_the_reason`
  - `Heartbeat_delivers_a_requested_sync_exactly_once`
  - `Snapshot_clears_the_pending_request`
  - `Pending_request_expires_after_three_minutes_with_error`
  - `Heartbeat_records_agent_fields`
  - `Failed_store_write_marks_sync_unsuccessful_but_continues`

  Die Zeit kommt aus `ManualTimeProvider` (eigene Unterklasse von `TimeProvider`, die `GetUtcNow()` überschreibt), damit kein Paket nötig ist.
- [ ] **Schritt 2: Implementieren** wie oben, Kommentare im Stil des `XLinkService`.
- [ ] **Schritt 3: Registrieren** in `Program.cs`: `builder.Services.AddSingleton(TimeProvider.System);` und `builder.Services.AddSingleton<HorizonSyncService>();`.
- [ ] **Schritt 4: Prüfen.** Gefilterter Test grün, dann `dotnet test Meadow.Data.Tests` komplett, Build der Sln.
- [ ] **Schritt 5: Commit** – `git commit -m "Horizon-Agent: HorizonSyncService"`

---

## Task 5: Umschaltung `ICowSyncService` und `/api/cowsync`

**Board:** „Horizon-Agent 5/11: Umschaltung ICowSyncService und /api/cowsync“. Abhängig von: 4.

**Abnahme:**
- `IXLinkService` heißt `ICowSyncService` (mit `Source`, Agent-Feldern und `LoadStatusAsync`). Es gibt keine Referenz auf `IXLinkService` mehr (`grep -r IXLinkService` leer, außer in Doku).
- `CowSyncSource` steuert die Quelle: Standard `HorizonAgent`, ungültiger Wert ergibt eine Warnung im Log und den Standard.
- Bei `XLink` verhält sich alles wie bisher, nur unter `/api/cowsync/*`.
- Bei `HorizonAgent`:
  - Es läuft kein `CowSyncBackgroundService`.
  - `POST /api/cowsync/refresh` antwortet mit 202 und setzt das Flag.
  - `GET /api/cowsync/status` meldet `source`, `running = IsSyncPending` und die Agent-Felder.
- `/api/xlink/*` ist weg (404).
- `/api/system/info` enthält `CowSyncSource`.
- Der Client baut und nutzt `HttpCowSyncService`.
- Nachweis per curl mit beiden Werten von `CowSyncSource`. `dotnet test Meadow.Data.Tests` ist grün.

**Files:**
- Rename/Modify: `Meadow.Shared/Services/IXLinkService.cs` → `ICowSyncService.cs`
- Modify: `Meadow.Data/Services/XLinkService.cs` (implementiert `ICowSyncService`)
- Modify: `Meadow.Data/Services/HorizonSyncService.cs` (implementiert `ICowSyncService`)
- Rename/Modify: `Meadow.Api/Endpoints/XLinkEndpoints.cs` → `CowSyncEndpoints.cs`
- Modify: `Meadow.Api/Infrastructure/XLinkRunner.cs` (nimmt `XLinkService` konkret statt `IXLinkService`)
- Modify: `Meadow.Api/BackgroundServices/CowSyncBackgroundService.cs` (nimmt `XLinkService` konkret)
- Create: `Meadow.Api/Infrastructure/CowSyncOptions.cs`
- Modify: `Meadow.Api/Program.cs` (Z. 100-104 Registrierung, Z. 128-135 HostedService, Z. 208 Mapping)
- Modify: `Meadow.Api/Endpoints/InfrastructureEndpoints.cs:84-100` (`/system/info` um `CowSyncSource` ergänzen)
- Rename/Modify: `Meadow.Client/Services/HttpXLinkService.cs` → `HttpCowSyncService.cs`
- Modify: `Meadow.Client/Services/ApiContracts.cs:77-86` (`XLinkStatusResponse` → `CowSyncStatusResponse`)
- Modify: `Meadow.Client/Program.cs:168`
- Modify: `Meadow.Client/Components/Cows/BaseData/BaseDataCow.razor:5` und `DatabaseInfoDialog.razor:7` (nur `@inject ICowSyncService CowSync` und Aufrufe umbenennen; Layout bleibt für Task 7)

**Interfaces (Produces):**

```csharp
// Meadow.Shared/Services/ICowSyncService.cs
namespace Meadow.Shared.Services;

public enum CowSyncSource { XLink, HorizonAgent }

/// <summary>Die Naht fuer den Kuh-Abgleich, egal aus welcher Quelle (vormals IXLinkService).</summary>
public interface ICowSyncService
{
    CowSyncSource Source { get; }
    DateTimeOffset? LastSyncUtc { get; }
    bool LastSyncSucceeded { get; }
    string? LastSyncError { get; }

    // Nur bei HorizonAgent belegt, sonst null.
    string? AgentVersion { get; }
    string? AgentMachineName { get; }
    DateTimeOffset? LastHeartbeatUtc { get; }
    int? LastSnapshotAnimalCount { get; }
    string? AgentLastError { get; }

    /// <summary>XLink: Lauf starten. HorizonAgent: Abgleich beim Agenten anfordern.</summary>
    Task RefreshCowsAsync(CancellationToken cancellationToken = default);

    /// <summary>Server: nichts zu tun. Browser: /api/cowsync/status lesen und die Felder setzen.</summary>
    Task LoadStatusAsync(CancellationToken cancellationToken = default);
}
```

```csharp
// Meadow.Api/Infrastructure/CowSyncOptions.cs
public sealed record CowSyncOptions(CowSyncSource Source);
```

`GET /api/cowsync/status` liefert ein camelCase-JSON mit `source` (als String), `lastSyncUtc`, `lastSyncSucceeded`, `lastSyncError`, `running`, `enabled`, `intervalHours` (nur XLink, sonst null), `url` (nur XLink), `agentVersion`, `agentMachineName`, `lastHeartbeatUtc`, `lastSnapshotAnimalCount` und `agentLastError`.

- [ ] **Schritt 1: Naht umbenennen.** Mit `git mv` von `IXLinkService.cs` nach `ICowSyncService.cs`, Inhalt wie oben. Der Kopfkommentar bleibt sinngemäß erhalten.
- [ ] **Schritt 2: Implementierungen.**
  - `XLinkService : ICowSyncService`: `Source => CowSyncSource.XLink`, Agent-Felder `null`, `LoadStatusAsync => Task.CompletedTask`.
  - `HorizonSyncService : ICowSyncService`: `RefreshCowsAsync` ruft `RequestSync()` und gibt `Task.CompletedTask` zurück, `LoadStatusAsync` ebenso.
- [ ] **Schritt 3: Program.cs.**
  - `CowSyncSource` aus `builder.Configuration["CowSyncSource"]` lesen: `Enum.TryParse(..., ignoreCase: true)`, Standard `HorizonAgent`, ungültiger Wert ergibt `LoggerService.LogWarning` (Stil wie bei `Demo:Enabled`).
  - `builder.Services.AddSingleton(new CowSyncOptions(source))`.
  - `XLinkService` und `HorizonSyncService` immer als konkrete Singletons registrieren.
  - `builder.Services.AddSingleton<ICowSyncService>(sp => source == CowSyncSource.XLink ? sp.GetRequiredService<XLinkService>() : sp.GetRequiredService<HorizonSyncService>());`
  - HostedService: Demo ergibt `DemoResetBackgroundService`. Sonst nur bei `XLink` den `CowSyncBackgroundService`. Den bestehenden Kommentarblock um den Horizon-Fall ergänzen: Der Agent liefert von selbst, ein Takt auf dem Server wäre leer.
- [ ] **Schritt 4: Endpunkte.** Mit `git mv` von `XLinkEndpoints.cs` nach `CowSyncEndpoints.cs`, `MapCowSyncEndpoints`, Routen `/cowsync/refresh` und `/cowsync/status`.
  - **refresh:**
    - Demo ergibt 403 (bestehender Text, „XLink“ durch „Kuh-Abgleich“ ersetzt).
    - `HorizonAgent` ergibt `horizon.RequestSync()` und `Results.Accepted(value: new { started = true })`.
    - `XLink` ergibt den bisherigen Runner-Pfad mit 409.
  - **status:** wie oben beschrieben. `running` ist `runner.IsRunning` bzw. `horizon.IsSyncPending`.

  Die bestehenden Kommentare (warum 202, warum kein `BumpsOnWrite`) mitnehmen.
- [ ] **Schritt 5: Runner und BackgroundService** nehmen `XLinkService` statt `IXLinkService` entgegen. Logik unverändert.
- [ ] **Schritt 6: `/system/info`** um `["CowSyncSource"] = options.Source.ToString()` ergänzen. Dafür `CowSyncOptions` als Parameter.
- [ ] **Schritt 7: Client.**
  - Mit `git mv` von `HttpXLinkService.cs` nach `HttpCowSyncService.cs`, implementiert `ICowSyncService`.
  - Routen `api/cowsync/refresh` und `api/cowsync/status`.
  - `CowSyncStatusResponse` mit allen Feldern, `Source` als `CowSyncSource` (der Enum kommt als String an, dafür `JsonStringEnumConverter` im Record-Attribut oder in `Json`-Optionen prüfen).
  - `LoadStatusAsync` liest den Status einmal und setzt alle Properties.
  - Die Fehlermeldungen von „XLink-Abgleich“ auf „Kuh-Abgleich“ umstellen.
  - `Program.cs:168` auf `AddSingleton<ICowSyncService, HttpCowSyncService>()`.
  - In `BaseDataCow.razor` und `DatabaseInfoDialog.razor` nur Inject und Bezeichner umbenennen, damit der Build grün ist.
- [ ] **Schritt 8: Prüfen.**
  - `dotnet build Meadow.sln` ohne Fehler.
  - Eigene Instanz mit `CowSyncSource=XLink`:
    - `curl -s localhost:<port>/api/cowsync/status` liefert `source: "XLink"`.
    - `curl -s -o /dev/null -w "%{http_code}" -X POST localhost:<port>/api/xlink/refresh` liefert 404.
  - Neustart mit `CowSyncSource` ungesetzt:
    - Der Status liefert `source: "HorizonAgent"`.
    - `POST /api/cowsync/refresh` liefert 202, danach meldet der Status `running: true`.
    - Im Log startet kein XLink-Lauf.
  - `dotnet test Meadow.Data.Tests` ist grün.
- [ ] **Schritt 9: Commit** – `git commit -m "Horizon-Agent: Umschaltung ICowSyncService und /api/cowsync"`

---

## Task 6: Endpunkte `/api/horizon/snapshot` und `/api/horizon/heartbeat`

**Board:** „Horizon-Agent 6/11: Endpunkte /api/horizon/snapshot und /heartbeat“. Abhängig von: 5.

**Abnahme (per curl gegen die eigene Instanz nachgewiesen):**
- Ohne `HorizonAgent__ApiKey` antworten beide Endpunkte mit 404, im Demo-Modus mit 403, ohne oder mit falschem `X-Agent-Key` mit 401.
- Bei `CowSyncSource=XLink` antwortet der Snapshot mit 409.
- Ein gültiger Snapshot ergibt 200 mit `HorizonSyncResult`, die Kühe stehen danach mit Ort, Gruppe und Beschreibung in `GET /api/cows`, und der `Cows`-Scope unter `/api/version` ist erhöht.
- Ein leerer Snapshot ergibt 422 mit Begründung, nichts wird geschrieben.
- Der Heartbeat liefert `syncRequested: true` genau einmal nach `POST /api/cowsync/refresh`.
- `HorizonAgentKeyTests` sind grün.

**Files:**
- Create: `Meadow.Data/Services/HorizonAgentKey.cs` (Vergleich in konstanter Zeit, testbar)
- Create: `Meadow.Data.Tests/BaseData/HorizonAgentKeyTests.cs`
- Create: `Meadow.Api/Endpoints/HorizonEndpoints.cs`
- Create: `Meadow.Api/Infrastructure/HorizonAgentOptions.cs`
- Modify: `Meadow.Api/Program.cs` (Options aus `builder.Configuration["HorizonAgent:ApiKey"]` registrieren, `api.MapHorizonEndpoints()` neben `MapCowSyncEndpoints`)

**Interfaces (Produces):**

```csharp
public static class HorizonAgentKey
{
    /// <summary>Konstante Laufzeit (CryptographicOperations.FixedTimeEquals ueber UTF-8).</summary>
    public static bool Matches(string? configured, string? presented);
}

public sealed record HorizonAgentOptions(string? ApiKey)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
```

Routen (alle unter der Gruppe `/api`):
- `POST /horizon/snapshot`, Body `HorizonSnapshot`. Antwortet mit 200 `HorizonSyncResult`, 401, 403, 404, 409 oder 422 (ProblemDetails, `detail` = Begründung).
- `POST /horizon/heartbeat`, Body `AgentHeartbeat`. Antwortet mit 200 `AgentHeartbeatResponse`, 401, 403 oder 404.

- [ ] **Schritt 1: Tests zuerst** für `HorizonAgentKey.Matches`:
  - Gleicher Key ergibt `true`.
  - Anderer Key gleicher Länge ergibt `false`.
  - Andere Länge ergibt `false`.
  - `null` oder leer auf einer der beiden Seiten ergibt `false`.

  Dann implementieren mit `CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b))`.
- [ ] **Schritt 2: Prüfreihenfolge** in einer privaten Hilfsmethode `Guard(HttpRequest, HorizonAgentOptions, DemoOptions)`, die `IResult?` liefert:
  1. Kein Key konfiguriert ergibt `Results.NotFound()`. So verrät die Instanz nicht, dass es den Endpunkt gibt.
  2. Demo ergibt 403 mit der Begründung aus `CowSyncEndpoints`.
  3. Header `X-Agent-Key` fehlt oder passt nicht ergibt `Results.Unauthorized()`.
- [ ] **Schritt 3: Snapshot.**
  - Erst `Guard`.
  - Bei `options.Source != CowSyncSource.HorizonAgent` ergibt sich 409 „Kuh-Abgleich steht auf XLink – der Snapshot wird nicht übernommen.“ Nie schreiben beide Quellen.
  - Sonst `await svc.ApplySnapshotAsync(body)`.
    - Abgelehnt ergibt `Results.Problem(statusCode: 422, title: "Snapshot abgelehnt", detail: result.RejectedReason)`.
    - Sonst `version.Bump(DataScope.Cows)`, sofern `Created + Updated + Linked + Promoted > 0`, und `Results.Ok(result)`.
  - Exceptions ergeben 500 über die normale Pipeline.
  - Kommentar: kein `BumpsOnWrite`, weil nur bei tatsächlichen Änderungen gebumpt wird.
- [ ] **Schritt 4: Heartbeat.** `Guard`, dann `Results.Ok(svc.RecordHeartbeat(body))`. Bei `Source == XLink` trotzdem annehmen (nur Status), aber `SyncRequested` bleibt `false`, weil `RequestSync` nur bei Horizon aufgerufen wird.
- [ ] **Schritt 5: Program.cs.** `builder.Services.AddSingleton(new HorizonAgentOptions(builder.Configuration["HorizonAgent:ApiKey"]));` und `api.MapHorizonEndpoints();`.
- [ ] **Schritt 6: Prüfen.** Eigene Instanz mit `HorizonAgent__ApiKey=test-key`. Die Datei `Meadow.Api/Meadow.Api.http` um Beispiele ergänzen. Per curl:

```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST localhost:<port>/api/horizon/heartbeat -H "Content-Type: application/json" -d "{}"            # 401
curl -s -X POST localhost:<port>/api/horizon/snapshot -H "X-Agent-Key: test-key" -H "Content-Type: application/json" -d "{\"agentVersion\":\"dev\",\"machineName\":\"test\",\"readAtUtc\":\"2026-10-22T08:00:00Z\",\"animals\":[{\"animalId\":\"9001\",\"collarNumber\":901,\"earTag\":\"DE0999000901\",\"isGone\":false,\"location\":\"Box 3\",\"group\":\"Melkgruppe\",\"description\":\"Test\"}]}"   # 200, created 1
curl -s localhost:<port>/api/cows | grep 9001                                     # location Box 3
curl -s -w "%{http_code}\n" -X POST localhost:<port>/api/horizon/snapshot -H "X-Agent-Key: test-key" -H "Content-Type: application/json" -d "{\"animals\":[]}"   # 422
curl -s -X POST localhost:<port>/api/cowsync/refresh; curl -s -X POST localhost:<port>/api/horizon/heartbeat -H "X-Agent-Key: test-key" -H "Content-Type: application/json" -d "{\"agentVersion\":\"dev\",\"machineName\":\"test\"}"   # syncRequested true, beim zweiten Mal false
```

Danach ohne Key neu starten: 404. Mit `Demo__Enabled=true`: 403. Die Testkuh danach in der eigenen DB wieder löschen.

- [ ] **Schritt 7: Commit** – `git commit -m "Horizon-Agent: Endpunkte snapshot und heartbeat"`

---

## Task 7: Client – Ort, Gruppe, Beschreibung und Agent-Status

**Board:** „Horizon-Agent 7/11: Client – Ort, Gruppe, Beschreibung und Agent-Status“. Abhängig von: 5.

**Abnahme:**
- Die Basisdaten-Tabelle der Kühe zeigt die Spalten **Ort**, **Gruppe** und **Beschreibung**. Die Beschreibung ist auf 60 Zeichen gekürzt, der volle Text steht im `title`. Leere Werte zeigen „—“.
- Die Suche findet auch über Ort und Gruppe.
- Der Knopf heißt je Quelle „Von Horizon aktualisieren“ oder „Von XLink aktualisieren“. Die Erfolgsmeldung nennt ebenfalls die Quelle.
- Der Datenbank-Dialog zeigt bei `HorizonAgent` die Zeile „Horizon-Agent“ mit:
  - „aktiv · zuletzt {Zeit} · {n} Tiere“
  - oder „Agent offline“, wenn das letzte Lebenszeichen mehr als 5 Minuten alt ist bzw. fehlt
  - oder „Fehler · …“

  Darunter Agent-Version, Rechnername und den letzten Fehler. Bei XLink bleibt die bisherige Zeile.
- Die Sichtprüfung ist erledigt: Desktop und 375 px, hell und dunkel.
- `dotnet test Meadow.Data.Tests` ist grün.

**Files:**
- Modify: `Meadow.Client/Components/Cows/BaseData/BaseDataCow.razor` (Spalten in `BuildColumns` nach „Status“ Z. ~70-76, `ApplyFilter` Z. ~113, Knopf Z. 18-22, `RefreshFromXLink` wird zu `RefreshCows`)
- Modify: `Meadow.Client/Components/Cows/Dialogs/DatabaseInfoDialog.razor`
- Optional Modify: CSS der Tabelle, falls die Beschreibung umbricht (`mw-table-wrap--dense`); vorhandene Klassen bevorzugen

- [ ] **Schritt 1: Quelle laden.** In `BaseDataCow.OnInitializedAsync` und `DatabaseInfoDialog.OnInitializedAsync` zusätzlich `await CowSync.LoadStatusAsync();`, in try/catch. Offline bleibt die Quelle `HorizonAgent` als Standard, der Knopf funktioniert trotzdem.
- [ ] **Schritt 2: Spalten.** Drei `MeadowColumn<Cow>` nach „Ohrmarkennummer“:

```csharp
        new()
        {
            Title = "Ort",
            Text = c => string.IsNullOrWhiteSpace(c.Location) ? "—" : c.Location,
            SortBy = c => c.Location ?? string.Empty
        },
        new()
        {
            Title = "Gruppe",
            Text = c => string.IsNullOrWhiteSpace(c.Group) ? "—" : c.Group,
            SortBy = c => c.Group ?? string.Empty
        },
        new()
        {
            Title = "Beschreibung",
            Template = cow => string.IsNullOrWhiteSpace(cow.Description)
                ? @<span class="mw-dim">—</span>
                : @<span title="@cow.Description">@Shorten(cow.Description, 60)</span>,
            SortBy = c => c.Description ?? string.Empty
        },
```

`Shorten` als private statische Methode (`…` anhängen). Prüfen, ob `MeadowColumn` eine Eigenschaft zum Ausblenden auf schmalen Bildschirmen hat. Wenn ja, die Beschreibung unter 768 px ausblenden.
- [ ] **Schritt 3: Suche.** `ApplyFilter`: zusätzlich zu `CowService.FilterFuncCow` trifft ein Term, den `Location` oder `Group` enthält (`OrdinalIgnoreCase`). **Nicht** in `CowLookups.FilterFuncCow` einbauen: Die Kuh-Auswahl in den Behandlungsdialogen nutzt sie und soll nicht nach Ort suchen.
- [ ] **Schritt 4: Knopf.** Text `_isRefreshing ? "Aktualisiere…" : $"Von {SourceLabel} aktualisieren"` mit `SourceLabel => CowSync.Source == CowSyncSource.XLink ? "XLink" : "Horizon"`. Die Erfolgsmeldung lautet „Kühe wurden von {SourceLabel} aktualisiert.“. Bei Horizon dauert der Lauf bis zu einer Minute (Lebenszeichen-Takt des Agenten): Während `_isRefreshing` steht im Knopf „Warte auf Horizon-Agent…“.
- [ ] **Schritt 5: Dialog.** Abhängig von `CowSync.Source`:
  - **XLink:** bisheriger Block, unverändert.
  - **HorizonAgent:** `<div class="mw-kv"><span>Horizon-Agent</span><span class="@AgentClass">@AgentStatus</span></div>` und darunter `mw-kv__sub`-Zeilen „Version {v} · {Rechner}“ und, falls gesetzt, „Letzter Fehler: {AgentLastError ?? LastSyncError}“.
  - `AgentStatus`:
    - Demo ergibt „im Demo-Modus deaktiviert“.
    - `LastHeartbeatUtc` null oder älter als 5 min ergibt „Agent offline“ (Klasse `mw-kv__bad`).
    - `LastSyncUtc` null ergibt „verbunden · noch kein Snapshot“.
    - Erfolg ergibt „aktiv · zuletzt dd.MM. HH:mm · {n} Tiere“.
    - Sonst „Fehler · zuletzt dd.MM. HH:mm“.
  - Die Zeit kommt über `DateTimeOffset.UtcNow` (Razor ist Aufrufer).
- [ ] **Schritt 6: Sichtprüfung.**
  - Instanz aus Task 6 mit Testsnapshot starten.
  - Seite Basisdaten → Kühe im Browser öffnen: Spalten sichtbar, Suche „Box“ trifft, Sortierung geht.
  - Bei 375 px gibt es keinen horizontalen Seiten-Scroll außerhalb der Tabelle.
  - Dialog „Datenbank Informationen“: nach einem Heartbeat per curl „aktiv …“, ohne Heartbeat „Agent offline“.
  - Hell und dunkel ansehen.
- [ ] **Schritt 7: Commit** – `git commit -m "Horizon-Agent: Ort, Gruppe, Beschreibung und Agent-Status im Client"`

---

## Task 8: Agent-Projekt und `HorizonReader`

**Board:** „Horizon-Agent 8/11: Agent-Projekt und HorizonReader“. Abhängig von: 1.

**Abnahme:**
- `Meadow.HorizonAgent` (Worker, net8.0) und `Meadow.HorizonAgent.Tests` (xUnit) stehen in `Meadow.sln`, und `dotnet build Meadow.sln` läuft ohne Fehler.
- Der `HorizonReader` startet jede Verbindung mit `EXECUTE AS USER = 'BenchmarkReadOnly' WITH NO REVERT` und bricht ab, wenn `USER_NAME()` etwas anderes liefert.
- `HorizonAnimalMapper.Map` meldet fehlende Pflichtspalten namentlich, setzt fehlende optionale Spalten auf `null`, `CollarNumber` NULL wird 0, leere Strings werden `null`.
- `--dry-run` liest einmal, gibt Tierzahl und die ersten 20 Tiere aus und beendet sich mit Exit-Code 0, bei einem Fehler mit Exit-Code 1 und verständlicher Meldung. Lokal ohne Horizon-DB ist das nachgewiesen.
- `dotnet publish -p:PublishProfile=win-x64` erzeugt eine einzelne `Meadow.HorizonAgent.exe`.
- `dotnet test Meadow.HorizonAgent.Tests` ist grün.

**Files:**
- Create: `Meadow.HorizonAgent/Meadow.HorizonAgent.csproj`, `Program.cs`, `appsettings.json`, `Options/HorizonOptions.cs`, `Options/MeadowOptions.cs`, `Reading/IHorizonReader.cs`, `Reading/HorizonReader.cs`, `Reading/HorizonAnimalMapper.cs`, `Properties/PublishProfiles/win-x64.pubxml`
- Create: `Meadow.HorizonAgent.Tests/Meadow.HorizonAgent.Tests.csproj`, `HorizonAnimalMapperTests.cs`
- Modify: `Meadow.sln` (über `dotnet sln add`)

**Interfaces (Produces):**

```csharp
public sealed class HorizonOptions
{
    public string ConnectionString { get; set; } = "";
    public string AnimalQuery { get; set; } = "";
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class MeadowOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int SnapshotIntervalMinutes { get; set; } = 15;
    public int HeartbeatSeconds { get; set; } = 60;
}

public interface IHorizonReader
{
    Task<List<HorizonAnimal>> ReadAnimalsAsync(CancellationToken cancellationToken);
}

public static class HorizonAnimalMapper
{
    public static readonly string[] RequiredColumns = { "AnimalId", "CollarNumber", "EarTag", "IsGone" };
    public static List<HorizonAnimal> Map(IDataReader reader);
}
```

- [ ] **Schritt 1: Projekte anlegen.**
  - `dotnet new worker -n Meadow.HorizonAgent -f net8.0` und `dotnet new xunit -n Meadow.HorizonAgent.Tests -f net8.0`. Die xUnit-Versionen auf den Stand von `Meadow.Data.Tests.csproj` angleichen (xunit 2.5.3, runner 2.5.3, Test.Sdk 17.8.0).
  - `dotnet sln Meadow.sln add Meadow.HorizonAgent/Meadow.HorizonAgent.csproj Meadow.HorizonAgent.Tests/Meadow.HorizonAgent.Tests.csproj`
  - Pakete im Agent:
    - `Microsoft.Extensions.Hosting` 8.0.1
    - `Microsoft.Extensions.Hosting.WindowsServices` 8.0.1
    - `Microsoft.Extensions.Logging.EventLog` 8.0.1
    - `Microsoft.Data.SqlClient` 5.2.2
  - Projektverweise: Agent auf `Meadow.Shared` (Verträge), Tests auf Agent.
  - Der Docker-Build ist nicht betroffen: Er restored nur `Meadow.Api`.
- [ ] **Schritt 2: Mapper-Tests zuerst** gegen `DataTable.CreateDataReader()`:
  - `Maps_all_columns_and_trims`
  - `Missing_required_column_names_it`: fehlt `EarTag`, enthält die Meldung „EarTag“.
  - `Missing_optional_columns_become_null`
  - `Null_collar_becomes_zero_and_bigint_collar_is_converted`
  - `Empty_strings_become_null`
  - `Blank_animal_id_throws_with_row_number`
  - `Column_names_are_matched_case_insensitive`
- [ ] **Schritt 3: Mapper implementieren.** Spaltenindex per Schleife über `reader.GetName(i)` mit `OrdinalIgnoreCase`. Die Werte über `Convert.ToInt32` und `Convert.ToBoolean` holen, `DBNull` beachten.
- [ ] **Schritt 4: Reader.**

```csharp
public sealed class HorizonReader : IHorizonReader
{
    public const string ReadOnlyUser = "BenchmarkReadOnly";

    public async Task<List<HorizonAnimal>> ReadAnimalsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Jeder lokale Windows-Benutzer ist auf diesem Server db_owner (guest). Deshalb als
        // ERSTES auf den Nur-Lese-Benutzer wechseln, ohne Rueckweg. Pooling=false ist dafuer
        // Pflicht: eine Verbindung mit NO REVERT laesst sich nicht in den Pool zuruecksetzen.
        await using (var impersonate = new SqlCommand($"EXECUTE AS USER = '{ReadOnlyUser}' WITH NO REVERT;", connection))
        {
            await impersonate.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var who = new SqlCommand("SELECT USER_NAME();", connection))
        {
            var user = (string?)await who.ExecuteScalarAsync(cancellationToken);
            if (!string.Equals(user, ReadOnlyUser, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Nur-Lese-Absicherung greift nicht (USER_NAME() = '{user}'). Abbruch.");
            }
        }

        await using var command = new SqlCommand(_options.AnimalQuery, connection) { CommandTimeout = _options.CommandTimeoutSeconds };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return HorizonAnimalMapper.Map(reader);
    }
}
```

Beim Start prüfen, dass der Connection-String `Pooling=false` enthält (`SqlConnectionStringBuilder`). Sonst einen Fehler mit Begründung werfen.
- [ ] **Schritt 5: `appsettings.json`.** Den Connection-String und die `AnimalQuery` wörtlich aus dem Spec-Nachtrag übernehmen. Dazu `"Meadow": { "BaseUrl": "https://<meadow-host>/", "ApiKey": "", "SnapshotIntervalMinutes": 15, "HeartbeatSeconds": 60 }` und Logging mit `EventLog: { SourceName: "Meadow Horizon-Agent", LogName: "Application" }`.
- [ ] **Schritt 6: `Program.cs`.**
  - `Host.CreateApplicationBuilder(args)`.
  - `builder.Services.AddWindowsService(o => o.ServiceName = "MeadowHorizonAgent")`.
  - Options binden, `IHorizonReader` als Singleton.
  - Bei `--dry-run` in `args`: Reader aus dem DI holen, einmal lesen, `Console.WriteLine($"{n} Tiere gelesen")` und eine Tabelle der ersten 20 (AnimalId, Halsband, Ohrmarke, Abgang, Ort, Gruppe, Beschreibung gekürzt) ausgeben, dann `return 0`. Fehler als `Console.Error.WriteLine` mit `return 1`.
  - Ohne `--dry-run`: `host.Run()`. Den Worker registriert erst Task 9, den Vorlagen-`Worker.cs` löschen.
- [ ] **Schritt 7: Publish-Profil** `win-x64.pubxml`: `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, `PublishDir=bin\publish\win-x64\`.
- [ ] **Schritt 8: Prüfen.**
  - `dotnet test Meadow.HorizonAgent.Tests` ist grün.
  - `dotnet build Meadow.sln` ohne Fehler.
  - `dotnet run --project Meadow.HorizonAgent -- --dry-run` endet ohne Horizon-DB mit Exit 1 und einer Meldung zur Verbindung, ohne Stacktrace-Flut.
  - `dotnet publish Meadow.HorizonAgent -p:PublishProfile=win-x64` erzeugt `Meadow.HorizonAgent.exe` und `appsettings.json` im Ausgabeordner.
- [ ] **Schritt 9: Commit** – `git commit -m "Horizon-Agent: Agent-Projekt und HorizonReader"`

---

## Task 9: Agent – `MeadowClient` und `AgentWorker`

**Board:** „Horizon-Agent 9/11: Agent – MeadowClient und AgentWorker“. Abhängig von: 8.

**Abnahme:**
- Der Agent schickt alle `HeartbeatSeconds` ein Lebenszeichen mit dem letzten Fehler und alle `SnapshotIntervalMinutes` einen Snapshot. Liefert das Lebenszeichen `syncRequested`, schickt er sofort einen Snapshot.
- Ist der Server nicht erreichbar, versucht er es mit Backoff 1, 2, 4 … höchstens 5 Minuten erneut. Nach einem Erfolg kehrt er zum normalen Takt zurück.
- Lese- und Sendefehler sowie 401/404/409/422 werden ins Ereignisprotokoll geschrieben und beim nächsten Lebenszeichen mitgeschickt.
- `AgentLoopTests` (Fake-Reader, Fake-Client, Fake-Zeit) decken diese Fälle ab und sind grün.
- Ein lokaler Lauf gegen die eigene `Meadow.Api` mit Fake-Reader (Umgebung `Development`) zeigt Heartbeat und Snapshot im API-Log.

**Files:**
- Create: `Meadow.HorizonAgent/Sending/IMeadowClient.cs`, `Sending/MeadowClient.cs`, `Sending/MeadowRejectedException.cs`
- Create: `Meadow.HorizonAgent/Worker/AgentLoop.cs`, `Worker/AgentWorker.cs`
- Create: `Meadow.HorizonAgent/Reading/FakeHorizonReader.cs` (nur in Development registriert: drei feste Tiere)
- Modify: `Meadow.HorizonAgent/Program.cs` (`AddHttpClient<IMeadowClient, MeadowClient>`, `AddSingleton<AgentLoop>`, `AddHostedService<AgentWorker>`, `TimeProvider.System`)
- Create: `Meadow.HorizonAgent.Tests/AgentLoopTests.cs`

**Interfaces (Produces):**

```csharp
public interface IMeadowClient
{
    Task<AgentHeartbeatResponse> SendHeartbeatAsync(AgentHeartbeat heartbeat, CancellationToken cancellationToken);
    /// <summary>Wirft MeadowRejectedException bei 401/403/404/409/422 (Server erreichbar, Anfrage abgewiesen),
    /// HttpRequestException/TaskCanceledException wenn der Server nicht erreichbar ist.</summary>
    Task<HorizonSyncResult> SendSnapshotAsync(HorizonSnapshot snapshot, CancellationToken cancellationToken);
}

/// <summary>Ein Takt der Agent-Schleife, ohne Warten - damit testbar.</summary>
public sealed class AgentLoop
{
    public AgentLoop(IHorizonReader reader, IMeadowClient client, IOptions<MeadowOptions> options, TimeProvider time, ILogger<AgentLoop> logger);
    /// <summary>Fuehrt faellige Arbeit aus und liefert die Wartezeit bis zum naechsten Takt (mind. 1 s).</summary>
    public Task<TimeSpan> TickAsync(CancellationToken cancellationToken);
    public string? LastError { get; }
}
```

**Verhalten von `TickAsync`** (Zeit `now` aus `TimeProvider`):
1. Ist das Lebenszeichen fällig (`now >= _nextHeartbeat`), sendet der Agent es mit `LastError`.
   - **Erfolg:** `_nextHeartbeat = now + HeartbeatSeconds`, Fehlerzähler 0. Bei `SyncRequested` wird `_nextSnapshot = now`.
   - **Nicht erreichbar:** Backoff (siehe unten) für `_nextHeartbeat` und `_nextSnapshot`, `LastError` setzen, sofort mit der Wartezeit zurück.
   - **`MeadowRejectedException`:** loggen, `LastError` setzen, normaler Takt.
2. Ist der Snapshot fällig, liest der Agent die Tiere.
   - **Lesefehler:** `LastError = "Lesen: …"`, `_nextSnapshot = now + Intervall`, es wird nichts gesendet.
   - Sonst wird gesendet.
     - **Erfolg:** `LastError = null`, `_nextSnapshot = now + Intervall`.
     - **`MeadowRejectedException`** (z. B. 422): `LastError = "Server: …"`, normales Intervall.
     - **Nicht erreichbar:** Backoff.
3. **Backoff:** `delay = min(2^(fails-1) Minuten, 5 Minuten)`, also 1, 2, 4, 5, 5 … Ein Erfolg setzt `fails = 0`.
4. Rückgabe: `max(1 s, min(_nextHeartbeat, _nextSnapshot) - now)`.

Der erste Takt nach dem Start sendet sofort ein Lebenszeichen und einen Snapshot.

`AgentWorker : BackgroundService`: Schleife `var wait = await loop.TickAsync(ct); await Task.Delay(wait, time, ct);`. Unerwartete Exceptions werden geloggt, die Schleife läuft weiter (30 s warten).

`MeadowClient`:
- `BaseAddress` aus `MeadowOptions.BaseUrl`, Header `X-Agent-Key`, Timeout 60 s.
- JSON mit `JsonSerializerDefaults.Web`.
- Ist der Statuscode einer der genannten, wird `MeadowRejectedException(statusCode, detail aus ProblemDetails)` geworfen.
- Agent-Version aus `AssemblyInformationalVersionAttribute`, Rechner aus `Environment.MachineName`.

- [ ] **Schritt 1: Tests zuerst** (`AgentLoopTests`, eigene `ManualTimeProvider`-Klasse wie in Task 4):
  - `First_tick_sends_heartbeat_and_snapshot`
  - `Snapshot_follows_interval_heartbeat_follows_its_own_interval`
  - `Sync_requested_triggers_immediate_snapshot`
  - `Unreachable_server_backs_off_1_2_4_5_5_minutes_and_resets_after_success`
  - `Read_error_is_sent_with_next_heartbeat_and_no_snapshot_is_sent`
  - `Rejected_snapshot_keeps_normal_interval_and_reports_error`
  - `Successful_snapshot_clears_last_error`
- [ ] **Schritt 2: `AgentLoop` implementieren**, Tests grün.
- [ ] **Schritt 3: `MeadowClient` und `AgentWorker`**, Registrierung in `Program.cs`. In `Development` den `FakeHorizonReader` statt des echten registrieren (`builder.Environment.IsDevelopment()`).
- [ ] **Schritt 4: Lokaler Lauf.**
  - Eigene `Meadow.Api` mit `HorizonAgent__ApiKey=test-key` starten.
  - Den Agenten mit `DOTNET_ENVIRONMENT=Development`, `Meadow__BaseUrl=http://localhost:<port>/` und `Meadow__ApiKey=test-key` per `dotnet run` starten.
  - Erwartet: Im API-Log erscheinen Heartbeat und Snapshot, `GET /api/cows` enthält die drei Fake-Tiere.
  - API stoppen: Der Agent loggt einen Backoff.
  - API wieder starten: Der Agent ist binnen höchstens 5 min wieder im Takt (für den Test `HeartbeatSeconds` klein setzen).
- [ ] **Schritt 5: Prüfen.** `dotnet test Meadow.HorizonAgent.Tests` und `dotnet test Meadow.Data.Tests` grün, Build der Sln. Die Fake-Tiere aus der eigenen DB entfernen.
- [ ] **Schritt 6: Commit** – `git commit -m "Horizon-Agent: MeadowClient und AgentWorker"`

---

## Task 10: Installation, Deploy und Doku

**Board:** „Horizon-Agent 10/11: Installation, Deploy und Doku“. Abhängig von: 6, 7, 9.

**Abnahme:**
- `Meadow.HorizonAgent/install.ps1` und `uninstall.ps1` lassen sich fehlerfrei parsen (`[System.Management.Automation.Language.Parser]::ParseFile` ohne Errors).
- `install.ps1` legt bei Bedarf das Konto `MeadowAgent` (Gruppe `Benutzer`, Recht „Als Dienst anmelden“) an, richtet den Dienst `MeadowHorizonAgent` ein (automatisch verzögert, Neustart nach 1 min), registriert die Ereignisquelle und setzt die ACL auf `appsettings.json`. Ein SQL-Login wird nicht angelegt.
- `uninstall.ps1` macht das rückgängig, das Konto nur mit `-RemoveUser`.
- `Meadow.HorizonAgent/README.md` beschreibt Einrichtung, `--dry-run`, Update und Fehlersuche.
- `deploy/docker-compose.yml` und `deploy/README.md` enthalten `CowSyncSource` und `HorizonAgent__ApiKey`.
- Die Oberflächentexte in `MeadowLanding.razor` und `Add_Calf_Dialog.razor` nennen Horizon statt nur XLink.
- Der Build ist grün.
- Die echte Installation ist nicht Teil dieses Tickets, sie gehört zu Ticket 11.

**Files:**
- Create: `Meadow.HorizonAgent/install.ps1`, `Meadow.HorizonAgent/uninstall.ps1`, `Meadow.HorizonAgent/README.md`
- Modify: `Meadow.HorizonAgent/Meadow.HorizonAgent.csproj` (Skripte und README beim Publish mitkopieren: `<None Update="install.ps1" CopyToPublishDirectory="PreserveNewest" />` …)
- Modify: `deploy/docker-compose.yml:29-41`, `deploy/README.md`, `README.md` (Abschnitt XLink)
- Modify: `Meadow.Client/Components/Ui/MeadowLanding.razor:385, 426-455`, `Meadow.Client/Components/Cows/Dialogs/Add_Calf_Dialog.razor:18`

- [ ] **Schritt 1: `install.ps1`.** `#Requires -RunAsAdministrator`. Parameter:
  - `-InstallDir` (Standard Ordner des Skripts)
  - `-ServiceAccount` (Standard `MeadowAgent`)
  - `-Password` (`SecureString`, Abfrage, wenn nicht angegeben)

  Ablauf, jeder Schritt idempotent:
  1. Konto `MeadowAgent` anlegen, falls es fehlt (`New-LocalUser -PasswordNeverExpires -UserMayNotChangePassword`). Mitglied der Gruppe **per SID** `S-1-5-32-545` (Benutzer/Users, sprachunabhängig).
  2. Recht `SeServiceLogonRight` vergeben: `secedit /export`, die Zeile `SeServiceLogonRight` um `*<SID des Kontos>` ergänzen, `secedit /configure /db secedit.sdb /cfg … /areas USER_RIGHTS`.
  3. Ereignisquelle: `New-EventLog -LogName Application -Source "Meadow Horizon-Agent"`, falls sie fehlt.
  4. ACL: `icacls appsettings.json /inheritance:r /grant:r "*S-1-5-32-544:(F)" "*S-1-5-18:(F)" "${ServiceAccount}:(R)"`.
  5. Dienst: `sc.exe create MeadowHorizonAgent binPath= "\"$InstallDir\Meadow.HorizonAgent.exe\"" start= delayed-auto obj= ".\$ServiceAccount" password= <klar> DisplayName= "Meadow Horizon-Agent"`, existiert er schon, `sc.exe config`. Dazu `sc.exe failure MeadowHorizonAgent reset= 86400 actions= restart/60000/restart/60000/restart/60000` und `sc.exe description …`.
  6. Hinweis ausgeben: zuerst `runas /user:MeadowAgent "Meadow.HorizonAgent.exe --dry-run"` testen, dann `Start-Service MeadowHorizonAgent`.

  Das Klartext-Passwort nur im Speicher halten, nie loggen.
- [ ] **Schritt 2: `uninstall.ps1`.** Dienst stoppen und mit `sc.exe delete` entfernen, Ereignisquelle entfernen. Mit `-RemoveUser` auch das Konto und das Dienst-Anmelderecht.
- [ ] **Schritt 3: README des Agenten.**
  - Voraussetzungen: Horizon-PC `CELSIUS_M740`, Instanz `.\T4C3`, DB `Lely`.
  - Sicherheitsmodell: `EXECUTE AS BenchmarkReadOnly`, Link auf das Artefakt.
  - Erstinstallation: Publish-Ordner kopieren, `appsettings.json` ausfüllen (`Meadow:BaseUrl`, `Meadow:ApiKey` = Wert von `HorizonAgent__ApiKey` auf dem Server), `--dry-run`, `install.ps1`.
  - Update: Dienst stoppen, Dateien außer `appsettings.json` tauschen, starten.
  - Schemaänderung nach einem Horizon-Update: `AnimalQuery` anpassen und mit `--dry-run` prüfen.
  - Fehlersuche: Ereignisanzeige → Anwendung → Quelle „Meadow Horizon-Agent“, Datenbank-Dialog in Meadow.
- [ ] **Schritt 4: Deploy.** In `deploy/docker-compose.yml` unter `meadow.environment`:

```yaml
      # HorizonAgent (Vorgabe) oder XLink. Nie schreiben beide: bei HorizonAgent laeuft
      # kein XLink-Takt, bei XLink nimmt /api/horizon/snapshot nichts an.
      CowSyncSource: ${COW_SYNC_SOURCE:-HorizonAgent}
      # Gemeinsames Geheimnis mit dem Agenten (appsettings.json -> Meadow:ApiKey).
      # Leer = /api/horizon/* antwortet 404.
      HorizonAgent__ApiKey: ${HORIZON_AGENT_KEY:-}
```

In `deploy/README.md`: Key mit `openssl rand -hex 32` erzeugen und in `.env` als `HORIZON_AGENT_KEY` eintragen. **Wichtig:** Solange der Agent nicht installiert ist, `COW_SYNC_SOURCE=XLink` setzen, sonst findet kein Abgleich statt.
- [ ] **Schritt 5: Texte.** `MeadowLanding.razor` und `Add_Calf_Dialog.razor` sprechen von „Lely Horizon (über den Horizon-Agenten oder XLink)“. Root-`README.md`: kurzer Abschnitt „Kuh-Abgleich“ mit Verweis auf `Meadow.HorizonAgent/README.md`.
- [ ] **Schritt 6: Prüfen.**
  - In PowerShell: `[System.Management.Automation.Language.Parser]::ParseFile("Meadow.HorizonAgent/install.ps1", [ref]$null, [ref]$errors); $errors.Count` muss 0 sein, ebenso für `uninstall.ps1`.
  - `dotnet publish Meadow.HorizonAgent -p:PublishProfile=win-x64`: Skripte und README liegen im Ausgabeordner.
  - `docker compose -f deploy/docker-compose.yml config` ohne Fehler (mit Dummy-`.env`-Werten).
  - Build der Sln.
- [ ] **Schritt 7: Commit** – `git commit -m "Horizon-Agent: Installation, Deploy und Doku"`

---

## Task 11: Abnahme auf dem Horizon-PC (manuell)

**Board:** „Horizon-Agent 11/11: Abnahme auf dem Horizon-PC“. Abhängig von: 10. Label `manuell`: Der Dispatcher übernimmt dieses Ticket nicht.

**Abnahme:**
- Auf `CELSIUS_M740` liefert `Meadow.HorizonAgent.exe --dry-run` (als `MeadowAgent`) die aktiven Tiere (am 20.10.2026 waren es 229), mit Ort, Gruppe und Beschreibung.
- Die Halsbandnummer (`AniUserNumber`) stimmt für fünf Stichproben mit der XLink-Tabelle überein.
- Ein Snapshot gegen einen Testserver wird angenommen. Danach läuft der Dienst gegen den Hof-Server, und der Datenbank-Dialog zeigt „aktiv“ mit Tierzahl.
- „Von Horizon aktualisieren“ liefert binnen etwa einer Minute.

- [ ] **Schritt 1:** Publish-Ordner auf den Horizon-PC kopieren, `appsettings.json` ausfüllen.
- [ ] **Schritt 2:** `--dry-run` als Admin und als `MeadowAgent`. Stimmt `AniUserNumber` nicht mit dem Halsband aus XLink überein, die `AnimalQuery` anpassen (z. B. `AniResponderNumber`) und den Spec-Nachtrag korrigieren.
- [ ] **Schritt 3:** Testserver mit `HorizonAgent__ApiKey`, dann `install.ps1`, Dienst starten, Ereignisanzeige prüfen.
- [ ] **Schritt 4:** Auf den Hof-Server umstellen (`COW_SYNC_SOURCE=HorizonAgent`), Dialog und Basisdaten prüfen, „Von Horizon aktualisieren“ testen.
- [ ] **Schritt 5:** Ergebnis als Kommentar am Ticket, danach `harness board accept`.
