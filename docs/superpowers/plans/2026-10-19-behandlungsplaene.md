# Behandlungspläne — Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pro Medikament einen wiederkehrenden Behandlungsplan definieren („N Gaben innerhalb von D Tagen" / „N Gaben alle D Tage"), pro Kuh den Fortschritt („2/3") auf Startseite und Kuh-Detailseite zeigen und bei fälligen Gaben per Hinweistafel und Web-Push erinnern.

**Architecture:** Die Plan-Definition liegt als nullable Spalten an `Medicine`. Laufende Durchläufe werden **nicht gespeichert**, sondern von der reinen Funktion `TreatmentPlanRuns.Build` in `Meadow.Shared` aus den vorhandenen `CowTreatment`-Zeilen berechnet. Gespeichert werden nur manuelle Abbrüche (`Treatment_Plan_Cancellation`, online-only) und die Entdopplung der täglichen Push (`Treatment_Plan_Push_Log`). Client (Dashboard, Kuh-Seite, Hinweistafel) und Server (Push-Scheduler) rufen dieselbe Funktion auf.

**Tech Stack:** .NET 8, Blazor WebAssembly (`Meadow.Client`) gehostet von `Meadow.Api` (Minimal API), MudBlazor 7.15, EF Core 8 mit Pomelo/MariaDB (`Meadow.Data`), xUnit 2.5.3 mit plain `Assert` (`Meadow.Data.Tests`), WebPush 1.0.13.

**Spec:** `docs/superpowers/specs/2026-10-19-behandlungsplaene-design.md` — bei jedem Zweifel gilt der Spec.

## Global Constraints

- **Ticket-Schnitt:** Jeder Task unten ist **ein Ticket** fürs Meadow-Board. Die Zeile „Board" je Task enthält Titel, Abhängigkeiten und Akzeptanzkriterien zum Übertragen.
- **Git:** nur committen, wenn der Nutzer es für das Ticket ausdrücklich freigibt. Der Commit-Schritt am Ende jedes Tasks gilt nur dann.
- **Sprache:** sichtbare Texte Deutsch **mit** Umlauten; Codekommentare Deutsch **ohne** Umlaute (`faellig`, `Gaben`), wie im restlichen Code. Testnamen englisch mit Unterstrichen.
- **Spaltennamen:** `Snake_Case` per `[Column]`, keine Fremdschlüssel (Projektkonvention seit `InitialCreate`).
- **„Heute" ist immer Parameter** (`DateTime today`) in jeder Logik in `Meadow.Shared`; nur Aufrufer (Provider, Scheduler, Razor) lesen `DateTime.Now` bzw. `DateTime.Today`. Vergleiche immer auf `.Date`.
- **Kuh-Schlüssel:** `CowTreatment.EarTagNumber` enthält die `Cow_ID` (nicht die Ohrmarke). Vergleiche `StringComparison.Ordinal`.
- **EF-Kommandos:** `dotnet ef migrations add <Name> --project Meadow.Data --startup-project Meadow.Data`. Die Dev-DB muss laufen (`docker compose -f docker-compose.dev.yml up -d`, Container `4cows-dev-db`). Migrationen laufen beim Start von `Meadow.Api` automatisch (`Program.cs:148`).
- **Laufende App sperrt `bin/`:** scheitert `dotnet build` mit MSB3021/MSB3027, mit `-p:BaseOutputPath=<scratch>/` bauen. Fremde `Meadow.Api`-Prozesse nie beenden.
- **Tests:** `dotnet test Meadow.Data.Tests --filter "FullyQualifiedName~<Klasse>"`; vor Abschluss jedes Tasks `dotnet test Meadow.Data.Tests` komplett grün.

---

## Dateiübersicht

| Datei | Rolle | Task |
| --- | --- | --- |
| `Meadow.Shared/Models/TreatmentPlanMode.cs` | **neu** — Enum `Window`/`Interval` | 1 |
| `Meadow.Shared/Models/Medicine.cs` | 4 Plan-Properties | 1 |
| `Meadow.Shared/Models/TreatmentPlanDefinition.cs` | **neu** — Validierung, Beschreibung, Stichtag | 1 |
| `Meadow.Data/Services/MedicineService.cs` | Insert/Update schreiben Plan-Spalten + Stichtag | 1 |
| `Meadow.Data/Migrations/*_AddMedicineTreatmentPlan.*` | **generiert** | 1 |
| `Meadow.Data.Tests/Models/TreatmentPlanDefinitionTests.cs` | **neu** | 1 |
| `Meadow.Client/Components/Dialogs/BaseData/EditMedicineDialog.razor` | Abschnitt „Behandlungsplan" | 2 |
| `Meadow.Client/Components/Cows/BaseData/BaseDataMedicine.razor` | Spalte „Behandlungsplan" | 2 |
| `Meadow.Shared/Models/TreatmentPlanCancellation.cs` | **neu** — Entität Abbruch | 3 |
| `Meadow.Shared/Models/TreatmentPlanRun.cs` | **neu** — Ergebnis-Record + Status-Enum | 3 |
| `Meadow.Shared/Models/TreatmentPlanRuns.cs` | **neu** — Berechnungskern | 3 |
| `Meadow.Data.Tests/Models/TreatmentPlanRunsTests.cs` | **neu** | 3 |
| `Meadow.Data/SQL/DatabaseContext.cs` | DbSets Abbruch (4), Push-Log (8) | 4, 8 |
| `Meadow.Data/Migrations/*_AddTreatmentPlanCancellation.*` | **generiert** | 4 |
| `Meadow.Shared/Services/ITreatmentPlanCancellationService.cs` | **neu** | 4 |
| `Meadow.Data/Services/TreatmentPlanCancellationService.cs` | **neu** — Cache + CRUD | 4 |
| `Meadow.Api/Endpoints/TreatmentPlanCancellationEndpoints.cs` | **neu** | 4 |
| `Meadow.Api/Infrastructure/DataVersion.cs` | `DataScope.TreatmentPlans` | 4 |
| `Meadow.Api/Program.cs` | Registrierungen (4, 8) | 4, 8 |
| `Meadow.Client/Services/HttpTreatmentPlanCancellationService.cs` | **neu** | 4 |
| `Meadow.Client/Services/MeadowStores.cs`, `wwwroot/js/meadow-db.js` | Lese-Snapshot offline | 4 |
| `Meadow.Client/Components/Services/MeadowDataLoader.cs` | Abbrüche mitladen | 4 |
| `Meadow.Client/Program.cs` | Registrierungen (4, 7) | 4, 7 |
| `Meadow.Shared/Profile/CowProfile.cs`, `CowProfileBuilder.cs` | `TreatmentPlans`, `ActivePlans` | 5 |
| `Meadow.Client/Components/Ui/CowDetailTabs.cs` | Lasche „Behandlungspläne" | 5 |
| `Meadow.Client/Components/Cows/CowDetail/TreatmentPlansPanel.razor` | **neu** | 5 |
| `Meadow.Client/Components/Dialogs/CancelTreatmentPlanDialog.razor` | **neu** | 5 |
| `Meadow.Client/Components/Pages/Cow_Detail.razor` | Lasche + Kachel verdrahten | 5 |
| `Meadow.Client/Components/Ui/MeadowTreatmentPlanCard.razor` | **neu** — Dashboard-Karte | 6 |
| `Meadow.Client/Components/Pages/Index.razor` | Karte einbinden | 6 |
| `Meadow.Shared/Lookups/TreatmentPlanReminderText.cs` | **neu** — Texte Hinweis + Push | 7 |
| `Meadow.Shared/Lookups/CowLookups.cs`, `BandageReminderText.cs` | `CollarSuffix` herausziehen | 7 |
| `Meadow.Client/Services/TreatmentPlanNoticeProvider.cs` | **neu** | 7 |
| `Meadow.Shared/Models/TreatmentPlanPushLog.cs` | **neu** | 8 |
| `Meadow.Data/Migrations/*_AddTreatmentPlanPushLog.*` | **generiert** | 8 |
| `Meadow.Api/Infrastructure/PushSender.cs` | `SendTreatmentPlanReminderToAllAsync` | 8 |
| `Meadow.Api/BackgroundServices/TreatmentPlanPushBackgroundService.cs` | **neu** | 8 |
| `Meadow.Data/SQL/DemoDataSeeder.cs` | Demo-Pläne | 9 |

## Abhängigkeiten der Tickets

```
1 Plan-Definition ──┬─> 2 Pflege-UI
                    └─> 3 Berechnungskern ──> 4 Abbruch-Persistenz ──┬─> 5 Kuh-Detailseite
                                                                      ├─> 6 Startseite
                                                                      ├─> 7 Hinweistafel ──> 8 Push
                                                                      └──────────────────────> 9 Demo + Abnahme (nach 2,5,6,7,8)
```
2 läuft parallel zu 3. 5, 6 und 7 laufen nach 4 parallel (sie fassen verschiedene Dateien an).

---

## Task 1: Plan-Definition am Medikament (Modell, Migration, Service)

**Board:** „Behandlungspläne 1/9: Plan-Definition am Medikament speichern". Abhängig von: —.
Akzeptanz: `Medicine` hat `PlanDoseCount`, `PlanMode`, `PlanDays`, `PlanValidFrom`; Migration nur mit `AddColumn` auf `Medicine`; `PUT /api/medicines/{id}` speichert die Plan-Felder; `PlanValidFrom` wird beim ersten Setzen eines Plans serverseitig auf heute gesetzt, bleibt bei Parameteränderung stehen und wird beim Entfernen `null`; ungültige Pläne werden mit 400 abgelehnt; Tests grün.

**Files:**
- Create: `Meadow.Shared/Models/TreatmentPlanMode.cs`
- Create: `Meadow.Shared/Models/TreatmentPlanDefinition.cs`
- Modify: `Meadow.Shared/Models/Medicine.cs` (nach `DefaultWhereHowId`)
- Modify: `Meadow.Data/Services/MedicineService.cs` (`InsertDataAsync`, `UpdateDataAsync` ~Z. 200-210)
- Modify: `Meadow.Api/Endpoints/MedicineEndpoints.cs` (POST ~Z. 45, PUT ~Z. 78)
- Create: `Meadow.Data/Migrations/*_AddMedicineTreatmentPlan.cs` (generiert)
- Test: `Meadow.Data.Tests/Models/TreatmentPlanDefinitionTests.cs`

**Interfaces:**
- Produces:
  - `enum TreatmentPlanMode { Window = 0, Interval = 1 }`
  - `Medicine.PlanDoseCount: int?`, `Medicine.PlanMode: TreatmentPlanMode?`, `Medicine.PlanDays: int?`, `Medicine.PlanValidFrom: DateTime?`
  - `TreatmentPlanDefinition.Validate(int? doses, TreatmentPlanMode? mode, int? days): string?` (null = gültig, auch „kein Plan")
  - `TreatmentPlanDefinition.IsDefined(Medicine): bool`
  - `TreatmentPlanDefinition.Describe(Medicine): string?`
  - `TreatmentPlanDefinition.ResolveValidFrom(Medicine? before, Medicine after, DateTime today): DateTime?`

- [ ] **Schritt 1: Failing Tests schreiben**

```csharp
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Models;

/// <summary>
/// Plan-Definition am Medikament: wann gilt ein Plan als definiert, wie wird er
/// beschrieben, und wann setzt der Server den Stichtag PlanValidFrom.
/// </summary>
public class TreatmentPlanDefinitionTests
{
    private static readonly DateTime Today = new(2026, 6, 15);

    private static Medicine Med(int? doses, TreatmentPlanMode? mode, int? days, DateTime? validFrom = null)
        => new(1, "Synulox") { PlanDoseCount = doses, PlanMode = mode, PlanDays = days, PlanValidFrom = validFrom };

    [Fact]
    public void Validate_accepts_no_plan_at_all()
        => Assert.Null(TreatmentPlanDefinition.Validate(null, null, null));

    [Theory]
    [InlineData(3, null, 5)]
    [InlineData(null, TreatmentPlanMode.Window, 5)]
    [InlineData(3, TreatmentPlanMode.Interval, null)]
    public void Validate_rejects_partial_plan(int? doses, TreatmentPlanMode? mode, int? days)
        => Assert.NotNull(TreatmentPlanDefinition.Validate(doses, mode, days));

    [Theory]
    [InlineData(1, 5)]
    [InlineData(21, 5)]
    [InlineData(3, 0)]
    [InlineData(3, 91)]
    public void Validate_rejects_values_out_of_range(int doses, int days)
        => Assert.NotNull(TreatmentPlanDefinition.Validate(doses, TreatmentPlanMode.Window, days));

    [Fact]
    public void Validate_rejects_unknown_mode()
        => Assert.NotNull(TreatmentPlanDefinition.Validate(3, (TreatmentPlanMode)7, 5));

    [Fact]
    public void IsDefined_requires_complete_valid_plan()
    {
        Assert.True(TreatmentPlanDefinition.IsDefined(Med(3, TreatmentPlanMode.Window, 5)));
        Assert.False(TreatmentPlanDefinition.IsDefined(Med(null, null, null)));
        Assert.False(TreatmentPlanDefinition.IsDefined(Med(1, TreatmentPlanMode.Window, 5)));
    }

    [Theory]
    [InlineData(TreatmentPlanMode.Window, 5, "3× innerhalb von 5 Tagen")]
    [InlineData(TreatmentPlanMode.Window, 1, "3× innerhalb von 1 Tag")]
    [InlineData(TreatmentPlanMode.Interval, 5, "3× alle 5 Tage")]
    [InlineData(TreatmentPlanMode.Interval, 1, "3× täglich")]
    public void Describe_formats_both_modes(TreatmentPlanMode mode, int days, string expected)
        => Assert.Equal(expected, TreatmentPlanDefinition.Describe(Med(3, mode, days)));

    [Fact]
    public void Describe_is_null_without_plan()
        => Assert.Null(TreatmentPlanDefinition.Describe(Med(null, null, null)));

    [Fact]
    public void ResolveValidFrom_sets_today_when_plan_is_new()
        => Assert.Equal(Today.Date,
            TreatmentPlanDefinition.ResolveValidFrom(Med(null, null, null), Med(3, TreatmentPlanMode.Window, 5), Today.AddHours(14)));

    [Fact]
    public void ResolveValidFrom_sets_today_on_insert()
        => Assert.Equal(Today.Date,
            TreatmentPlanDefinition.ResolveValidFrom(null, Med(3, TreatmentPlanMode.Window, 5), Today));

    [Fact]
    public void ResolveValidFrom_keeps_existing_date_when_parameters_change()
    {
        var before = Med(3, TreatmentPlanMode.Window, 5, validFrom: new DateTime(2026, 1, 1));
        var after = Med(4, TreatmentPlanMode.Interval, 7);
        Assert.Equal(new DateTime(2026, 1, 1), TreatmentPlanDefinition.ResolveValidFrom(before, after, Today));
    }

    [Fact]
    public void ResolveValidFrom_clears_date_when_plan_is_removed()
    {
        var before = Med(3, TreatmentPlanMode.Window, 5, validFrom: new DateTime(2026, 1, 1));
        Assert.Null(TreatmentPlanDefinition.ResolveValidFrom(before, Med(null, null, null), Today));
    }

    [Fact]
    public void ResolveValidFrom_ignores_client_supplied_date()
    {
        // Der Client darf den Stichtag nicht setzen - sonst koennte ein Bearbeiten
        // die gesamte Historie nachtraeglich in Plaene verwandeln.
        var after = Med(3, TreatmentPlanMode.Window, 5, validFrom: new DateTime(2020, 1, 1));
        Assert.Equal(Today.Date, TreatmentPlanDefinition.ResolveValidFrom(Med(null, null, null), after, Today));
    }
}
```

- [ ] **Schritt 2: Laufen lassen, Fehlschlag prüfen**

Run: `dotnet test Meadow.Data.Tests --filter "FullyQualifiedName~TreatmentPlanDefinitionTests"`
Expected: Build-Fehler, `TreatmentPlanMode` / `TreatmentPlanDefinition` / `Medicine.PlanDoseCount` unbekannt.

- [ ] **Schritt 3: Enum und Medicine-Properties anlegen**

`Meadow.Shared/Models/TreatmentPlanMode.cs`:

```csharp
namespace Meadow.Shared.Models;

/// <summary>
/// Art eines Behandlungsplans am Medikament. Als int gespeichert (Spalte
/// Plan_Mode) - die Werte sind deshalb Vertrag und duerfen nicht umnummeriert
/// werden.
/// </summary>
public enum TreatmentPlanMode
{
    /// <summary>"N Gaben innerhalb von D Tagen" - Starttag zaehlt als Tag 1.</summary>
    Window = 0,

    /// <summary>"N Gaben alle D Tage" - naechste Gabe = letzte tatsaechliche Gabe + D.</summary>
    Interval = 1
}
```

In `Meadow.Shared/Models/Medicine.cs` nach `DefaultWhereHowId` einfügen:

```csharp
    /// <summary>
    /// Behandlungsplan: Anzahl Gaben je Durchlauf. Alle drei Plan-Felder sind
    /// entweder gemeinsam gesetzt oder gemeinsam null - die Regel steht in
    /// <see cref="TreatmentPlanDefinition.Validate"/>.
    /// </summary>
    [Column("Plan_Dose_Count")]
    public int? PlanDoseCount { get; set; }

    [Column("Plan_Mode")]
    public TreatmentPlanMode? PlanMode { get; set; }

    [Column("Plan_Days")]
    public int? PlanDays { get; set; }

    /// <summary>
    /// Stichtag: Behandlungen davor zaehlen nicht zu Durchlaeufen. Setzt
    /// ausschliesslich der Server (<see cref="TreatmentPlanDefinition.ResolveValidFrom"/>),
    /// sonst erzeugte das Nachtragen eines Plans aus jeder alten Gabe einen
    /// "ueberfaelligen" Durchlauf.
    /// </summary>
    [Column("Plan_Valid_From")]
    public DateTime? PlanValidFrom { get; set; }
```

- [ ] **Schritt 4: `TreatmentPlanDefinition` implementieren**

`Meadow.Shared/Models/TreatmentPlanDefinition.cs`:

```csharp
namespace Meadow.Shared.Models;

/// <summary>
/// Regeln zur Plan-Definition am <see cref="Medicine"/>. Rein und paketfrei,
/// damit Dialog (Client), Endpunkt und Service (Server) dieselbe Regel nutzen.
/// </summary>
public static class TreatmentPlanDefinition
{
    public const int MinDoses = 2;
    public const int MaxDoses = 20;
    public const int MinDays = 1;
    public const int MaxDays = 90;

    /// <summary>
    /// <c>null</c> = gueltig. "Kein Plan" (alles null) ist gueltig; ein halb
    /// ausgefuellter Plan nicht.
    /// </summary>
    public static string? Validate(int? doses, TreatmentPlanMode? mode, int? days)
    {
        if (doses is null && mode is null && days is null)
        {
            return null;
        }

        if (doses is null || mode is null || days is null)
        {
            return "Behandlungsplan unvollständig: Anzahl, Art und Tage angeben.";
        }

        if (!Enum.IsDefined(mode.Value))
        {
            return "Unbekannte Art des Behandlungsplans.";
        }

        if (doses < MinDoses || doses > MaxDoses)
        {
            return $"Die Anzahl der Gaben muss zwischen {MinDoses} und {MaxDoses} liegen.";
        }

        if (days < MinDays || days > MaxDays)
        {
            return $"Die Tage müssen zwischen {MinDays} und {MaxDays} liegen.";
        }

        return null;
    }

    public static bool IsDefined(Medicine medicine)
        => medicine.PlanDoseCount is not null
           && Validate(medicine.PlanDoseCount, medicine.PlanMode, medicine.PlanDays) is null;

    public static string? Describe(Medicine medicine)
    {
        if (!IsDefined(medicine))
        {
            return null;
        }

        var doses = medicine.PlanDoseCount!.Value;
        var days = medicine.PlanDays!.Value;
        return medicine.PlanMode == TreatmentPlanMode.Window
            ? $"{doses}× innerhalb von {(days == 1 ? "1 Tag" : $"{days} Tagen")}"
            : days == 1 ? $"{doses}× täglich" : $"{doses}× alle {days} Tage";
    }

    /// <summary>
    /// Stichtag nach dem Speichern. Kein Plan -> null. Plan neu (vorher keiner
    /// oder Insert) -> heute. Plan bestand schon -> bisheriger Stichtag. Ein vom
    /// Client mitgeschickter Wert in <paramref name="after"/> wird ignoriert.
    /// </summary>
    public static DateTime? ResolveValidFrom(Medicine? before, Medicine after, DateTime today)
    {
        if (!IsDefined(after))
        {
            return null;
        }

        if (before is not null && IsDefined(before) && before.PlanValidFrom is not null)
        {
            return before.PlanValidFrom;
        }

        return today.Date;
    }
}
```

- [ ] **Schritt 5: Tests laufen lassen**

Run: `dotnet test Meadow.Data.Tests --filter "FullyQualifiedName~TreatmentPlanDefinitionTests"`
Expected: PASS (20 Testfälle).

- [ ] **Schritt 6: Service schreibt die Plan-Spalten**

In `Meadow.Data/Services/MedicineService.cs`:
- `UpdateDataAsync`: **vor** dem `ExecuteUpdateAsync` den alten Stand aus dem Cache holen und den Stichtag auflösen, dann die Kette um vier `SetProperty` erweitern:

```csharp
            Medicines.TryGetValue(medicine.MedicineId, out var before);
            medicine.PlanValidFrom = TreatmentPlanDefinition.ResolveValidFrom(before, medicine, DateTime.Today);
            // ... bestehende Kette:
                    .SetProperty(m => m.DefaultWhereHowId, medicine.DefaultWhereHowId)
                    .SetProperty(m => m.PlanDoseCount, medicine.PlanDoseCount)
                    .SetProperty(m => m.PlanMode, medicine.PlanMode)
                    .SetProperty(m => m.PlanDays, medicine.PlanDays)
                    .SetProperty(m => m.PlanValidFrom, medicine.PlanValidFrom));
```
- `InsertDataAsync`: vor dem `Add` `medicine.PlanValidFrom = TreatmentPlanDefinition.ResolveValidFrom(null, medicine, DateTime.Today);`
- Prüfen, dass der Cache-Eintrag nach dem Update das **ganze** `medicine`-Objekt übernimmt (inkl. `PlanValidFrom`); falls dort Felder einzeln kopiert werden, die vier ergänzen.
- `MergeAsync` bleibt unverändert: das überlebende Medikament behält seinen Plan.

- [ ] **Schritt 7: Endpunkte validieren**

In `Meadow.Api/Endpoints/MedicineEndpoints.cs`, in POST (Z. ~45) und PUT (Z. ~78) als erste Zeile im Handler:

```csharp
            var planError = TreatmentPlanDefinition.Validate(medicine.PlanDoseCount, medicine.PlanMode, medicine.PlanDays);
            if (planError is not null)
            {
                return Results.BadRequest(planError);
            }
```
Den Fehler-Stil an die übrigen 400-Antworten der Datei anpassen (gibt es dort ein `EndpointCommon.BadRequest`, das verwenden).

- [ ] **Schritt 8: Migration erzeugen und prüfen**

Run: `dotnet ef migrations add AddMedicineTreatmentPlan --project Meadow.Data --startup-project Meadow.Data`
Expected: `Up` enthält **genau vier** `AddColumn` auf `Medicine` (`Plan_Dose_Count` int null, `Plan_Mode` int null, `Plan_Days` int null, `Plan_Valid_From` datetime(6) null). Steht dort etwas anderes (DropColumn, AlterColumn, andere Tabelle), ist der Snapshot aus dem Tritt: `dotnet ef migrations remove --project Meadow.Data --startup-project Meadow.Data`, Ursache klären.

- [ ] **Schritt 9: Gesamtbuild und alle Tests**

Run: `dotnet build Meadow.sln` und `dotnet test Meadow.Data.Tests`
Expected: 0 Fehler, alle Tests grün.

- [ ] **Schritt 10: Commit (nur nach Freigabe)**

```bash
git add Meadow.Shared/Models/TreatmentPlanMode.cs Meadow.Shared/Models/TreatmentPlanDefinition.cs Meadow.Shared/Models/Medicine.cs Meadow.Data/Services/MedicineService.cs Meadow.Api/Endpoints/MedicineEndpoints.cs Meadow.Data/Migrations Meadow.Data.Tests/Models/TreatmentPlanDefinitionTests.cs
git commit -m "Behandlungsplaene: Plan-Definition am Medikament"
```

---

## Task 2: Plan-Definition in den Basisdaten pflegen

**Board:** „Behandlungspläne 2/9: Behandlungsplan im Medikament-Dialog pflegen". Abhängig von: 1.
Akzeptanz: Im Medikament-Dialog lässt sich ein Plan an-/abschalten und mit Anzahl, Art, Tagen bearbeiten; eine Vorschau zeigt `Describe`; ungültige Eingaben sperren Speichern mit der Meldung aus `Validate`; die Medikamentenliste zeigt eine Spalte „Behandlungsplan" (leer = kein Plan).

**Files:**
- Modify: `Meadow.Client/Components/Dialogs/BaseData/EditMedicineDialog.razor`
- Modify: `Meadow.Client/Components/Cows/BaseData/BaseDataMedicine.razor`

**Interfaces:**
- Consumes: `TreatmentPlanMode`, `TreatmentPlanDefinition.Validate/Describe`, `Medicine.Plan*` (Task 1). Speichern geht über das vorhandene `IMedicineService.UpdateDataAsync` / `InsertDataAsync`, keine neue API.

- [ ] **Schritt 1: Dialog lesen.** `EditMedicineDialog.razor` vollständig lesen: wie Felder gebunden werden (Kopie des Medikaments oder Einzelfelder), wie `Save` validiert und wie Fehler angezeigt werden. Das Muster für `DosageUnit`/`DefaultWhereHowId` übernehmen.

- [ ] **Schritt 2: Abschnitt „Behandlungsplan" einfügen** (unter den vorhandenen Feldern):

```razor
<MudDivider Class="my-3" />
<MudSwitch T="bool" @bind-Value="_hasPlan" Color="Color.Primary" Label="Behandlungsplan" />
@if (_hasPlan)
{
    <MudStack Row="true" AlignItems="AlignItems.Center" Wrap="Wrap.Wrap" Spacing="2">
        <MudNumericField T="int?" @bind-Value="_planDoses" Label="Gaben"
                         Min="TreatmentPlanDefinition.MinDoses" Max="TreatmentPlanDefinition.MaxDoses" Style="max-width:6rem" />
        <MudSelect T="TreatmentPlanMode?" @bind-Value="_planMode" Label="Art" Style="max-width:12rem">
            <MudSelectItem T="TreatmentPlanMode?" Value="TreatmentPlanMode.Window">innerhalb von</MudSelectItem>
            <MudSelectItem T="TreatmentPlanMode?" Value="TreatmentPlanMode.Interval">alle</MudSelectItem>
        </MudSelect>
        <MudNumericField T="int?" @bind-Value="_planDays" Label="Tage"
                         Min="TreatmentPlanDefinition.MinDays" Max="TreatmentPlanDefinition.MaxDays" Style="max-width:6rem" />
    </MudStack>
    <MudText Typo="Typo.caption">@(PlanPreview ?? PlanError)</MudText>
}
```

Code-Behind:

```csharp
    private bool _hasPlan;
    private int? _planDoses;
    private TreatmentPlanMode? _planMode;
    private int? _planDays;

    // Beim Oeffnen aus dem Medikament befuellen (OnInitialized/OnParametersSet,
    // je nachdem, wo der Dialog seine Kopie anlegt):
    //   _hasPlan = TreatmentPlanDefinition.IsDefined(medicine);
    //   _planDoses = medicine.PlanDoseCount ?? 3;
    //   _planMode = medicine.PlanMode ?? TreatmentPlanMode.Window;
    //   _planDays = medicine.PlanDays ?? 5;

    private string? PlanError => _hasPlan
        ? TreatmentPlanDefinition.Validate(_planDoses, _planMode, _planDays)
        : null;

    private string? PlanPreview => PlanError is null && _hasPlan
        ? TreatmentPlanDefinition.Describe(new Medicine { PlanDoseCount = _planDoses, PlanMode = _planMode, PlanDays = _planDays })
        : null;

    // In Save, vor dem Service-Aufruf:
    //   if (PlanError is not null) { <vorhandene Fehleranzeige>(PlanError); return; }
    //   medicine.PlanDoseCount = _hasPlan ? _planDoses : null;
    //   medicine.PlanMode      = _hasPlan ? _planMode  : null;
    //   medicine.PlanDays      = _hasPlan ? _planDays  : null;
```
`PlanValidFrom` wird im Dialog **nicht** angefasst (setzt der Server).

- [ ] **Schritt 3: Spalte in `BaseDataMedicine.razor`.** Neben der Einheit-Spalte eine Spalte „Behandlungsplan" mit `@(TreatmentPlanDefinition.Describe(context.Item) ?? "—")`; in der Mobil-Karte (falls vorhanden) dieselbe Zeile ergänzen.

- [ ] **Schritt 4: Build + Klicktest.** App starten (siehe Memory-Notiz „App lokal starten"), Einstellungen → Basisdaten → Medikament bearbeiten: Plan „3× alle 5 Tage" speichern, Liste zeigt die Spalte; neu öffnen zeigt die Werte; Plan abschalten → Spalte „—". In der DB prüfen: `docker exec 4cows-dev-db mariadb -uroot -padmin 4cows_v2 -e "select Medicine_Name, Plan_Dose_Count, Plan_Mode, Plan_Days, Plan_Valid_From from Medicine where Plan_Dose_Count is not null"` → `Plan_Valid_From` = heute.

- [ ] **Schritt 5: Commit (nur nach Freigabe)** — `git commit -m "Behandlungsplaene: Pflege im Medikament-Dialog"`

---

## Task 3: Berechnungskern `TreatmentPlanRuns`

**Board:** „Behandlungspläne 3/9: Durchläufe aus Behandlungen berechnen". Abhängig von: 1.
Akzeptanz: Reine Funktion berechnet je Kuh und Plan-Medikament die Durchläufe mit Status, Fortschritt, nächster Fälligkeit bzw. Fensterende nach den Regeln im Spec (Abschnitt „Fachliche Regeln"); alle Testfälle unten grün.

**Files:**
- Create: `Meadow.Shared/Models/TreatmentPlanCancellation.cs`
- Create: `Meadow.Shared/Models/TreatmentPlanRun.cs`
- Create: `Meadow.Shared/Models/TreatmentPlanRuns.cs`
- Test: `Meadow.Data.Tests/Models/TreatmentPlanRunsTests.cs`

**Interfaces:**
- Consumes: `Medicine.Plan*`, `TreatmentPlanDefinition.IsDefined` (Task 1); `CowTreatment`, `Cow`, `CowLookups.GetById`.
- Produces:
  - Entität `TreatmentPlanCancellation { int TreatmentPlanCancellationId; string CowId; int MedicineId; Guid StartTreatmentClientId; DateTime CancelledOn; string? Reason }` (DB-Anbindung erst in Task 4)
  - `enum TreatmentPlanRunStatus { Upcoming, DueToday, Overdue, Completed, Incomplete, Cancelled }`
  - `record TreatmentPlanRun` mit `CowId`, `MedicineId`, `StartTreatmentClientId`, `Mode`, `DoseCount`, `PlanDays`, `Doses`, `Status`, `NextDueDate?`, `WindowEnd?`, `Cancellation?`, berechnet `StartDate`, `DosesDone`, `IsActive`, `IsDue`, `OverdueDays(DateTime today)`
  - `TreatmentPlanRuns.Build(IEnumerable<CowTreatment>, IReadOnlyDictionary<int, Medicine>, IEnumerable<TreatmentPlanCancellation>, DateTime today): IReadOnlyList<TreatmentPlanRun>`
  - `TreatmentPlanRuns.ForCow(IEnumerable<TreatmentPlanRun>, string cowId): IReadOnlyList<TreatmentPlanRun>` — aktive zuerst, dann neuester Start zuerst
  - `TreatmentPlanRuns.ActiveForDashboard(IEnumerable<TreatmentPlanRun>, IReadOnlyDictionary<string, Cow>): IReadOnlyList<TreatmentPlanRun>` — nur aktive, nur vorhandene und nicht abgegangene Kühe, sortiert Overdue → DueToday → Upcoming, dann Fälligkeit, dann CowId
  - `TreatmentPlanRuns.Due(IEnumerable<TreatmentPlanRun>, IReadOnlyDictionary<string, Cow>): IReadOnlyList<TreatmentPlanRun>` — `ActiveForDashboard` gefiltert auf `IsDue`

- [ ] **Schritt 1: Failing Tests schreiben**

```csharp
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Models;

/// <summary>
/// Der Berechnungskern der Behandlungsplaene. Durchlaeufe werden nicht
/// gespeichert, sondern aus den Kuhbehandlungen abgeleitet - jede Regel des
/// Spec ("Fachliche Regeln") hat hier mindestens einen Fall.
/// </summary>
public class TreatmentPlanRunsTests
{
    private static readonly DateTime Today = new(2026, 6, 15);
    private const string CowA = "DE01";
    private const string CowB = "DE02";

    private int _id;

    private static Medicine Med(int id, int doses, TreatmentPlanMode mode, int days, DateTime? validFrom = null)
        => new(id, $"Med{id}") { PlanDoseCount = doses, PlanMode = mode, PlanDays = days, PlanValidFrom = validFrom };

    private static IReadOnlyDictionary<int, Medicine> Meds(params Medicine[] medicines)
        => medicines.ToDictionary(m => m.MedicineId);

    private CowTreatment Dose(DateTime date, string cow = CowA, int medicineId = 1)
        => new(++_id, cow, medicineId, date, 1f, 1, 1);

    private static TreatmentPlanCancellation Cancel(CowTreatment start, DateTime on)
        => new() { CowId = start.EarTagNumber, MedicineId = start.MedicineId, StartTreatmentClientId = start.ClientId, CancelledOn = on };

    private static IReadOnlyList<TreatmentPlanRun> Build(
        IReadOnlyDictionary<int, Medicine> meds, IEnumerable<CowTreatment> doses, params TreatmentPlanCancellation[] cancellations)
        => TreatmentPlanRuns.Build(doses, meds, cancellations, Today);

    [Fact]
    public void Medicine_without_plan_produces_no_runs()
    {
        var meds = Meds(new Medicine(1, "Ohne Plan"));
        Assert.Empty(Build(meds, new[] { Dose(Today) }));
    }

    [Fact]
    public void Interval_first_dose_is_upcoming_with_next_due_after_days()
    {
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { Dose(Today.AddDays(-2)) }));
        Assert.Equal(TreatmentPlanRunStatus.Upcoming, run.Status);
        Assert.Equal(Today.AddDays(3), run.NextDueDate);
        Assert.Equal(1, run.DosesDone);
        Assert.Equal(3, run.DoseCount);
        Assert.True(run.IsActive);
        Assert.False(run.IsDue);
    }

    [Fact]
    public void Interval_is_due_today_when_last_dose_plus_days_is_today()
    {
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { Dose(Today.AddDays(-5).AddHours(18)) }));
        Assert.Equal(TreatmentPlanRunStatus.DueToday, run.Status);
        Assert.True(run.IsDue);
    }

    [Fact]
    public void Interval_is_overdue_after_due_date()
    {
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { Dose(Today.AddDays(-7)) }));
        Assert.Equal(TreatmentPlanRunStatus.Overdue, run.Status);
        Assert.Equal(2, run.OverdueDays(Today));
    }

    [Fact]
    public void Interval_late_dose_shifts_next_due_date()
    {
        // Zweite Gabe zwei Tage zu spaet: die dritte ist ab DIESER Gabe faellig.
        var doses = new[] { Dose(Today.AddDays(-12)), Dose(Today.AddDays(-5)) };
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), doses));
        Assert.Equal(Today, run.NextDueDate);
        Assert.Equal(TreatmentPlanRunStatus.DueToday, run.Status);
        Assert.Equal(2, run.DosesDone);
    }

    [Fact]
    public void Run_completes_at_dose_count_and_next_dose_starts_new_run()
    {
        var doses = new[] { Dose(Today.AddDays(-20)), Dose(Today.AddDays(-15)), Dose(Today.AddDays(-1)) };
        var runs = Build(Meds(Med(1, 2, TreatmentPlanMode.Interval, 5)), doses);

        Assert.Equal(2, runs.Count);
        var completed = runs.Single(r => r.Status == TreatmentPlanRunStatus.Completed);
        Assert.Equal(2, completed.DosesDone);
        Assert.Null(completed.NextDueDate);
        var next = runs.Single(r => r.IsActive);
        Assert.Equal(Today.AddDays(-1), next.StartDate);
        Assert.Equal(1, next.DosesDone);
    }

    [Fact]
    public void Window_counts_doses_inside_window_and_reports_window_end()
    {
        var doses = new[] { Dose(Today.AddDays(-2)), Dose(Today.AddDays(-1)) };
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Window, 5)), doses));
        // Starttag = Tag 1 -> letzter Tag = Start + 4.
        Assert.Equal(Today.AddDays(2), run.WindowEnd);
        Assert.Equal(TreatmentPlanRunStatus.Upcoming, run.Status);
        Assert.Null(run.NextDueDate);
        Assert.Equal(2, run.DosesDone);
    }

    [Fact]
    public void Window_is_due_today_when_remaining_doses_fill_remaining_days()
    {
        // 3 in 3 Tagen, eine Gabe gestern: 2 Gaben offen, 2 Tage (heute, morgen) uebrig.
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Window, 3)), new[] { Dose(Today.AddDays(-1)) }));
        Assert.Equal(TreatmentPlanRunStatus.DueToday, run.Status);
    }

    [Fact]
    public void Window_is_not_due_when_already_treated_today()
    {
        var doses = new[] { Dose(Today.AddDays(-1)), Dose(Today.AddHours(7)) };
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Window, 3)), doses));
        Assert.Equal(TreatmentPlanRunStatus.Upcoming, run.Status);
    }

    [Fact]
    public void Window_expired_without_all_doses_is_incomplete()
    {
        var doses = new[] { Dose(Today.AddDays(-10)), Dose(Today.AddDays(-9)) };
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Window, 5)), doses));
        Assert.Equal(TreatmentPlanRunStatus.Incomplete, run.Status);
        Assert.False(run.IsActive);
    }

    [Fact]
    public void Dose_after_window_end_starts_new_run()
    {
        var doses = new[] { Dose(Today.AddDays(-10)), Dose(Today.AddDays(-2)) };
        var runs = Build(Meds(Med(1, 3, TreatmentPlanMode.Window, 5)), doses);

        Assert.Equal(2, runs.Count);
        Assert.Equal(TreatmentPlanRunStatus.Incomplete, runs.Single(r => r.StartDate == Today.AddDays(-10)).Status);
        Assert.True(runs.Single(r => r.StartDate == Today.AddDays(-2)).IsActive);
    }

    [Fact]
    public void Two_doses_on_same_day_count_separately()
    {
        var doses = new[] { Dose(Today.AddHours(8)), Dose(Today.AddHours(18)) };
        var run = Assert.Single(Build(Meds(Med(1, 2, TreatmentPlanMode.Window, 3)), doses));
        Assert.Equal(TreatmentPlanRunStatus.Completed, run.Status);
    }

    [Fact]
    public void Doses_are_ordered_by_date_regardless_of_input_order()
    {
        var late = Dose(Today.AddDays(-1));
        var early = Dose(Today.AddDays(-6));
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { late, early }));
        Assert.Equal(new[] { early, late }, run.Doses);
    }

    [Fact]
    public void Cancelled_run_is_inactive_and_later_dose_starts_new_run()
    {
        var start = Dose(Today.AddDays(-10));
        var later = Dose(Today.AddDays(-3));
        var runs = Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { start, later }, Cancel(start, Today.AddDays(-8)));

        Assert.Equal(2, runs.Count);
        var cancelled = runs.Single(r => r.StartTreatmentClientId == start.ClientId);
        Assert.Equal(TreatmentPlanRunStatus.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.Cancellation);
        Assert.Equal(1, cancelled.DosesDone);
        Assert.True(runs.Single(r => r.StartTreatmentClientId == later.ClientId).IsActive);
    }

    [Fact]
    public void Dose_on_cancellation_day_still_counts_to_cancelled_run()
    {
        var start = Dose(Today.AddDays(-10));
        var sameDay = Dose(Today.AddDays(-8));
        var run = Assert.Single(Build(Meds(Med(1, 3, TreatmentPlanMode.Interval, 5)), new[] { start, sameDay }, Cancel(start, Today.AddDays(-8))));
        Assert.Equal(TreatmentPlanRunStatus.Cancelled, run.Status);
        Assert.Equal(2, run.DosesDone);
    }

    [Fact]
    public void Completed_wins_over_cancellation()
    {
        var a = Dose(Today.AddDays(-10));
        var b = Dose(Today.AddDays(-5));
        var run = Assert.Single(Build(Meds(Med(1, 2, TreatmentPlanMode.Interval, 5)), new[] { a, b }, Cancel(a, Today)));
        Assert.Equal(TreatmentPlanRunStatus.Completed, run.Status);
    }

    [Fact]
    public void Doses_before_valid_from_are_ignored()
    {
        var meds = Meds(Med(1, 3, TreatmentPlanMode.Interval, 5, validFrom: Today.AddDays(-3)));
        var run = Assert.Single(Build(meds, new[] { Dose(Today.AddDays(-10)), Dose(Today.AddDays(-1)) }));
        Assert.Equal(Today.AddDays(-1), run.StartDate);
    }

    [Fact]
    public void Runs_are_separated_per_cow_and_medicine()
    {
        var meds = Meds(Med(1, 3, TreatmentPlanMode.Interval, 5), Med(2, 2, TreatmentPlanMode.Window, 3));
        var doses = new[]
        {
            Dose(Today.AddDays(-1), CowA, 1), Dose(Today.AddDays(-1), CowB, 1), Dose(Today.AddDays(-1), CowA, 2)
        };
        var runs = Build(meds, doses);

        Assert.Equal(3, runs.Count);
        Assert.All(runs, r => Assert.Equal(1, r.DosesDone));
        Assert.Equal(2, TreatmentPlanRuns.ForCow(runs, CowA).Count);
    }

    [Fact]
    public void ForCow_lists_active_runs_first_then_newest()
    {
        var meds = Meds(Med(1, 2, TreatmentPlanMode.Interval, 5));
        var doses = new[] { Dose(Today.AddDays(-30)), Dose(Today.AddDays(-25)), Dose(Today.AddDays(-20)), Dose(Today.AddDays(-15)), Dose(Today.AddDays(-1)) };
        var runs = TreatmentPlanRuns.ForCow(Build(meds, doses), CowA);

        Assert.Equal(3, runs.Count);
        Assert.True(runs[0].IsActive);
        Assert.Equal(Today.AddDays(-20), runs[1].StartDate);
        Assert.Equal(Today.AddDays(-30), runs[2].StartDate);
    }

    [Fact]
    public void ActiveForDashboard_orders_by_urgency_and_skips_gone_or_unknown_cows()
    {
        var meds = Meds(Med(1, 3, TreatmentPlanMode.Interval, 5));
        var doses = new[]
        {
            Dose(Today.AddDays(-2), "UP"),      // Upcoming
            Dose(Today.AddDays(-7), "OVER"),    // Overdue
            Dose(Today.AddDays(-5), "DUE"),     // DueToday
            Dose(Today.AddDays(-7), "GONE"),    // abgegangen
            Dose(Today.AddDays(-7), "UNKNOWN")  // nicht in der Kuhliste
        };
        var cows = new Dictionary<string, Cow>
        {
            ["UP"] = new("UP", null, 1, false, false),
            ["OVER"] = new("OVER", null, 2, false, false),
            ["DUE"] = new("DUE", null, 3, false, false),
            ["GONE"] = new("GONE", null, 4, false, true)
        };

        var runs = Build(meds, doses);
        var active = TreatmentPlanRuns.ActiveForDashboard(runs, cows);
        Assert.Equal(new[] { "OVER", "DUE", "UP" }, active.Select(r => r.CowId));
        Assert.Equal(new[] { "OVER", "DUE" }, TreatmentPlanRuns.Due(runs, cows).Select(r => r.CowId));
    }
}
```

- [ ] **Schritt 2: Laufen lassen, Fehlschlag prüfen**

Run: `dotnet test Meadow.Data.Tests --filter "FullyQualifiedName~TreatmentPlanRunsTests"`
Expected: Build-Fehler, `TreatmentPlanRuns` / `TreatmentPlanRun` / `TreatmentPlanCancellation` unbekannt.

- [ ] **Schritt 3: Entität `TreatmentPlanCancellation`**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Meadow.Shared.Models;

/// <summary>
/// Manueller Abbruch eines Behandlungsplan-Durchlaufs. Durchlaeufe selbst
/// werden nicht gespeichert (siehe <see cref="TreatmentPlanRuns"/>); der Abbruch
/// ist das Einzige, was sich nicht aus den Behandlungen ableiten laesst.
///
/// Der Durchlauf wird ueber die ClientId seiner ERSTEN Gabe identifiziert -
/// die ist stabil, schon bevor die Zeile den Server je gesehen hat.
/// </summary>
[Table("Treatment_Plan_Cancellation")]
public class TreatmentPlanCancellation
{
    public const int ReasonMaxLength = 190;

    [Key]
    [Column("Treatment_Plan_Cancellation_ID")]
    public int TreatmentPlanCancellationId { get; set; }

    [Required]
    [Column("Cow_ID")]
    public string CowId { get; set; } = string.Empty;

    [Required]
    [Column("Medicine_ID")]
    public int MedicineId { get; set; }

    [Required]
    [Column("Start_Treatment_Client_Id")]
    public Guid StartTreatmentClientId { get; set; }

    [Required]
    [Column("Cancelled_On")]
    public DateTime CancelledOn { get; set; }

    [StringLength(ReasonMaxLength)]
    [Column("Reason")]
    public string? Reason { get; set; }
}
```

- [ ] **Schritt 4: Record `TreatmentPlanRun`**

```csharp
namespace Meadow.Shared.Models;

public enum TreatmentPlanRunStatus
{
    /// <summary>Offen, heute nichts zu tun.</summary>
    Upcoming,

    /// <summary>Heute ist eine Gabe faellig.</summary>
    DueToday,

    /// <summary>Nur Interval: Faelligkeit ueberschritten.</summary>
    Overdue,

    /// <summary>Alle Gaben erhalten.</summary>
    Completed,

    /// <summary>Nur Window: Fenster vorbei, nicht alle Gaben erhalten.</summary>
    Incomplete,

    /// <summary>Manuell abgebrochen.</summary>
    Cancelled
}

/// <summary>
/// Ein berechneter Durchlauf eines Behandlungsplans fuer eine Kuh. Momentaufnahme
/// zu einem "heute" - Neuberechnung heisst neues Objekt.
/// </summary>
public sealed record TreatmentPlanRun
{
    public required string CowId { get; init; }
    public required int MedicineId { get; init; }
    public required Guid StartTreatmentClientId { get; init; }
    public required TreatmentPlanMode Mode { get; init; }
    public required int DoseCount { get; init; }
    public required int PlanDays { get; init; }

    /// <summary>Die gezaehlten Gaben, aufsteigend nach Datum. Nie leer.</summary>
    public required IReadOnlyList<CowTreatment> Doses { get; init; }

    public required TreatmentPlanRunStatus Status { get; init; }

    /// <summary>Nur Interval und nur solange aktiv: Tag der naechsten Gabe.</summary>
    public DateTime? NextDueDate { get; init; }

    /// <summary>Nur Window: letzter zulaessiger Tag (einschliesslich).</summary>
    public DateTime? WindowEnd { get; init; }

    public TreatmentPlanCancellation? Cancellation { get; init; }

    public DateTime StartDate => Doses[0].AdministrationDate.Date;
    public int DosesDone => Doses.Count;

    public bool IsActive => Status is TreatmentPlanRunStatus.Upcoming
        or TreatmentPlanRunStatus.DueToday
        or TreatmentPlanRunStatus.Overdue;

    public bool IsDue => Status is TreatmentPlanRunStatus.DueToday or TreatmentPlanRunStatus.Overdue;

    public int OverdueDays(DateTime today)
        => NextDueDate is null ? 0 : Math.Max(0, (today.Date - NextDueDate.Value.Date).Days);
}
```

- [ ] **Schritt 5: `TreatmentPlanRuns` implementieren**

```csharp
using Meadow.Shared.Lookups;

namespace Meadow.Shared.Models;

/// <summary>
/// Leitet Behandlungsplan-Durchlaeufe aus den Kuhbehandlungen ab. Rein: keine
/// Uhr, kein Netz, keine Datenbank - Client (Dashboard, Kuh-Seite, Hinweise)
/// und Server (Push) rufen dieselbe Funktion auf. Regeln: Spec
/// 2026-10-19-behandlungsplaene-design.md, Abschnitt "Fachliche Regeln".
/// </summary>
public static class TreatmentPlanRuns
{
    public static IReadOnlyList<TreatmentPlanRun> Build(
        IEnumerable<CowTreatment> treatments,
        IReadOnlyDictionary<int, Medicine> medicines,
        IEnumerable<TreatmentPlanCancellation> cancellations,
        DateTime today)
    {
        var day = today.Date;
        // Unique-Index auf Start_Treatment_Client_Id - doppelte gibt es nicht,
        // GroupBy schuetzt trotzdem vor einem Absturz bei kaputten Daten.
        var cancelByStart = cancellations
            .GroupBy(c => c.StartTreatmentClientId)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<TreatmentPlanRun>();
        var groups = treatments
            .Where(t => medicines.TryGetValue(t.MedicineId, out var m) && TreatmentPlanDefinition.IsDefined(m))
            .GroupBy(t => (CowId: t.EarTagNumber, t.MedicineId));

        foreach (var group in groups)
        {
            var medicine = medicines[group.Key.MedicineId];
            var validFrom = medicine.PlanValidFrom?.Date ?? DateTime.MinValue;
            var doses = group
                .Where(t => t.AdministrationDate.Date >= validFrom)
                .OrderBy(t => t.AdministrationDate)
                .ThenBy(t => t.CowTreatmentId);

            var current = new List<CowTreatment>();
            foreach (var dose in doses)
            {
                if (current.Count > 0 && !Accepts(current[0], dose, medicine, cancelByStart))
                {
                    result.Add(Close(current, medicine, cancelByStart, day, superseded: true));
                    current = new List<CowTreatment>();
                }

                current.Add(dose);

                if (current.Count == medicine.PlanDoseCount)
                {
                    result.Add(Close(current, medicine, cancelByStart, day, superseded: false));
                    current = new List<CowTreatment>();
                }
            }

            if (current.Count > 0)
            {
                result.Add(Close(current, medicine, cancelByStart, day, superseded: false));
            }
        }

        return result;
    }

    public static IReadOnlyList<TreatmentPlanRun> ForCow(IEnumerable<TreatmentPlanRun> runs, string cowId)
        => runs
            .Where(r => string.Equals(r.CowId, cowId, StringComparison.Ordinal))
            .OrderByDescending(r => r.IsActive)
            .ThenByDescending(r => r.StartDate)
            .ToList();

    public static IReadOnlyList<TreatmentPlanRun> ActiveForDashboard(
        IEnumerable<TreatmentPlanRun> runs, IReadOnlyDictionary<string, Cow> cows)
        => runs
            .Where(r => r.IsActive && CowLookups.GetById(cows, r.CowId) is { IsGone: false })
            .OrderBy(r => Urgency(r.Status))
            .ThenBy(r => r.NextDueDate ?? r.WindowEnd ?? DateTime.MaxValue)
            .ThenBy(r => r.CowId, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<TreatmentPlanRun> Due(
        IEnumerable<TreatmentPlanRun> runs, IReadOnlyDictionary<string, Cow> cows)
        => ActiveForDashboard(runs, cows).Where(r => r.IsDue).ToList();

    private static int Urgency(TreatmentPlanRunStatus status) => status switch
    {
        TreatmentPlanRunStatus.Overdue => 0,
        TreatmentPlanRunStatus.DueToday => 1,
        _ => 2
    };

    private static DateTime WindowEnd(CowTreatment first, Medicine medicine)
        => first.AdministrationDate.Date.AddDays(medicine.PlanDays!.Value - 1);

    /// <summary>
    /// Nimmt der offene Durchlauf (erste Gabe <paramref name="first"/>) diese Gabe an?
    /// Abgebrochen: nur bis einschliesslich Abbruchtag. Window: nur bis Fensterende.
    /// Interval: jede spaetere Gabe.
    /// </summary>
    private static bool Accepts(
        CowTreatment first, CowTreatment dose, Medicine medicine,
        IReadOnlyDictionary<Guid, TreatmentPlanCancellation> cancelByStart)
    {
        if (cancelByStart.TryGetValue(first.ClientId, out var cancellation)
            && dose.AdministrationDate.Date > cancellation.CancelledOn.Date)
        {
            return false;
        }

        return medicine.PlanMode != TreatmentPlanMode.Window
               || dose.AdministrationDate.Date <= WindowEnd(first, medicine);
    }

    private static TreatmentPlanRun Close(
        List<CowTreatment> doses, Medicine medicine,
        IReadOnlyDictionary<Guid, TreatmentPlanCancellation> cancelByStart,
        DateTime today, bool superseded)
    {
        var first = doses[0];
        var doseCount = medicine.PlanDoseCount!.Value;
        var days = medicine.PlanDays!.Value;
        var mode = medicine.PlanMode!.Value;
        cancelByStart.TryGetValue(first.ClientId, out var cancellation);

        DateTime? windowEnd = mode == TreatmentPlanMode.Window ? WindowEnd(first, medicine) : null;
        DateTime? nextDue = null;
        TreatmentPlanRunStatus status;

        if (doses.Count >= doseCount)
        {
            status = TreatmentPlanRunStatus.Completed;
        }
        else if (cancellation is not null)
        {
            status = TreatmentPlanRunStatus.Cancelled;
        }
        else if (superseded)
        {
            // Eine spaetere Gabe hat schon einen neuen Durchlauf begonnen -
            // dieser ist vorbei, egal was "heute" sagt (z.B. vordatierte Gabe).
            status = TreatmentPlanRunStatus.Incomplete;
        }
        else if (mode == TreatmentPlanMode.Window)
        {
            var remainingDoses = doseCount - doses.Count;
            var remainingDays = (windowEnd!.Value - today).Days + 1;
            var treatedToday = doses.Any(t => t.AdministrationDate.Date == today);

            status = remainingDays <= 0
                ? TreatmentPlanRunStatus.Incomplete
                : !treatedToday && remainingDoses >= remainingDays
                    ? TreatmentPlanRunStatus.DueToday
                    : TreatmentPlanRunStatus.Upcoming;
        }
        else
        {
            nextDue = doses[^1].AdministrationDate.Date.AddDays(days);
            status = today < nextDue ? TreatmentPlanRunStatus.Upcoming
                : today == nextDue ? TreatmentPlanRunStatus.DueToday
                : TreatmentPlanRunStatus.Overdue;
        }

        return new TreatmentPlanRun
        {
            CowId = first.EarTagNumber,
            MedicineId = first.MedicineId,
            StartTreatmentClientId = first.ClientId,
            Mode = mode,
            DoseCount = doseCount,
            PlanDays = days,
            Doses = doses.ToList(),
            Status = status,
            NextDueDate = nextDue,
            WindowEnd = windowEnd,
            Cancellation = cancellation
        };
    }
}
```

Hinweis: Liegt `CowLookups` in einem anderen Namespace als `Meadow.Shared.Lookups`, das `using` anpassen. `CowLookups.GetById` liefert `null` für unbekannte Kühe; das Pattern `is { IsGone: false }` filtert beides.

- [ ] **Schritt 6: Tests laufen lassen**

Run: `dotnet test Meadow.Data.Tests --filter "FullyQualifiedName~TreatmentPlanRunsTests"`
Expected: PASS (20 Tests). Dann `dotnet test Meadow.Data.Tests` komplett grün.

- [ ] **Schritt 7: Commit (nur nach Freigabe)** — `git commit -m "Behandlungsplaene: Berechnungskern TreatmentPlanRuns"`

---

## Task 4: Abbruch-Persistenz (Tabelle, Service, API, Client-Service, Offline-Lesen)

**Board:** „Behandlungspläne 4/9: Abbrüche speichern und laden". Abhängig von: 3.
Akzeptanz: Tabelle `Treatment_Plan_Cancellation` mit Unique-Index auf `Start_Treatment_Client_Id`; `GET/POST/DELETE /api/treatment-plan-cancellations` funktionieren und bumpen `DataScope.TreatmentPlans`; POST auf eine bereits abgebrochene Start-ClientId ist idempotent (liefert den bestehenden Datensatz); Client-Service hält einen Cache, `MeadowDataLoader` lädt ihn für Dashboard und Kuh-Seite; offline liefert der Lese-Snapshot die letzten Abbrüche; Schreiben offline zeigt eine verständliche Fehlermeldung.

**Files:**
- Modify: `Meadow.Data/SQL/DatabaseContext.cs` (DbSet + Unique-Index in `OnModelCreating`, neben den `ClientId`-Indizes Z. 64-79)
- Create: `Meadow.Data/Migrations/*_AddTreatmentPlanCancellation.cs` (generiert)
- Create: `Meadow.Shared/Services/ITreatmentPlanCancellationService.cs`
- Create: `Meadow.Data/Services/TreatmentPlanCancellationService.cs`
- Create: `Meadow.Api/Endpoints/TreatmentPlanCancellationEndpoints.cs`
- Modify: `Meadow.Api/Infrastructure/DataVersion.cs:4-9` (`TreatmentPlans` ans Ende)
- Modify: `Meadow.Api/Program.cs` (Singleton Z. 82-97, Mapping Z. 182-202)
- Create: `Meadow.Client/Services/HttpTreatmentPlanCancellationService.cs`
- Modify: `Meadow.Client/Program.cs:151-163`
- Modify: `Meadow.Client/Services/MeadowStores.cs:33-51, ~63-76`, `Meadow.Client/wwwroot/js/meadow-db.js:17, ~79`
- Modify: `Meadow.Client/Components/Services/MeadowDataLoader.cs:134-177` (+ Kuh-Seiten-Pfad)

**Interfaces:**
- Consumes: `TreatmentPlanCancellation` (Task 3).
- Produces:

```csharp
public interface ITreatmentPlanCancellationService
{
    /// <summary>Cache, Schluessel = StartTreatmentClientId.</summary>
    ImmutableDictionary<Guid, TreatmentPlanCancellation> Cancellations { get; }
    Task GetAllDataAsync();
    /// <summary>Idempotent: existiert fuer die Start-ClientId schon ein Abbruch, bleibt er stehen.</summary>
    Task<bool> CancelAsync(TreatmentPlanCancellation cancellation);
    /// <summary>Abbruch zuruecknehmen.</summary>
    Task<bool> RevokeAsync(Guid startTreatmentClientId);
}
```

Routen: `GET /api/treatment-plan-cancellations`, `POST /api/treatment-plan-cancellations`, `DELETE /api/treatment-plan-cancellations/{startTreatmentClientId:guid}`.

- [ ] **Schritt 1: Vorbild lesen.** `TreatmentReasonService.cs` (Data), `TreatmentReasonEndpoints.cs` (Api), `HttpTreatmentReasonService.cs` (Client) lesen: Cache-Aufbau (ImmutableDictionary + Lock/Interlocked), `IDbContextFactory`-Nutzung, `DatabaseStatusService.ReportSuccess/Failure`, `LoggerService`, `EndpointCommon`, `.BumpsOnWrite`, `HttpServiceBase`. Neue Klassen 1:1 nach diesem Muster bauen.

- [ ] **Schritt 2: DbContext.** `public DbSet<TreatmentPlanCancellation> TreatmentPlanCancellations { get; set; }` und in `OnModelCreating`:

```csharp
        modelBuilder.Entity<TreatmentPlanCancellation>()
            .HasIndex(c => c.StartTreatmentClientId)
            .IsUnique();
```

- [ ] **Schritt 3: Migration.** `dotnet ef migrations add AddTreatmentPlanCancellation --project Meadow.Data --startup-project Meadow.Data`. Erwartet: genau `CreateTable Treatment_Plan_Cancellation` + `CreateIndex` (unique). Sonst entfernen und klären.

- [ ] **Schritt 4: Server-Service** `TreatmentPlanCancellationService` (Singleton):
  - `GetAllDataAsync`: alle Zeilen `AsNoTracking`, Cache = `ToImmutableDictionary(c => c.StartTreatmentClientId)`.
  - `CancelAsync`: existiert die Start-ClientId im Cache **oder** in der DB → `true` ohne Schreiben; sonst `CancelledOn = c.CancelledOn.Date`, `Reason` auf `ReasonMaxLength` kürzen, einfügen, Cache aktualisieren. Unique-Verletzung (Race) → neu laden und `true` (vgl. `ClientIdUpsert.RaceWinnerAsync`).
  - `RevokeAsync`: `ExecuteDeleteAsync` auf die Start-ClientId, Cache entfernen; nicht vorhanden → `true` (idempotent).

- [ ] **Schritt 5: Endpunkte** `TreatmentPlanCancellationEndpoints.MapTreatmentPlanCancellationEndpoints(this RouteGroupBuilder api)`:
  - GET → `Results.Ok(svc.Cancellations.Values)`.
  - POST: `CowId` leer oder `StartTreatmentClientId == Guid.Empty` → 400; sonst `CancelAsync` → `Results.Ok(svc.Cancellations[c.StartTreatmentClientId])` bzw. `EndpointCommon.WriteFailed(...)`. `.BumpsOnWrite(DataScope.TreatmentPlans)`.
  - DELETE → `RevokeAsync` → `Results.NoContent()`. `.BumpsOnWrite(DataScope.TreatmentPlans)`.
  - In `Program.cs` registrieren (`AddSingleton<ITreatmentPlanCancellationService, TreatmentPlanCancellationService>()`) und mappen; beim Start nach `DataSeeder` `GetAllDataAsync` aufrufen, falls die anderen Services dort ebenfalls vorgewärmt werden.

- [ ] **Schritt 6: Client-Service** `HttpTreatmentPlanCancellationService : HttpServiceBase, ITreatmentPlanCancellationService` nach `HttpTreatmentReasonService`: GET füllt den Cache; `CancelAsync`/`RevokeAsync` rufen POST/DELETE und aktualisieren den Cache bei Erfolg sofort. Offline/Fehler → `false` (die UI meldet „Abbrechen ist nur online möglich."). Zusätzlich `event Action? Changed`, das nach jedem erfolgreichen Laden/Schreiben feuert (für Task 7). Registrieren in `Meadow.Client/Program.cs` als Singleton, sowohl als Klasse als auch als Interface (damit der Notice-Provider in Task 7 das Event abonnieren kann).

- [ ] **Schritt 7: Offline-Lesen.** In `MeadowStores.cs` eine Store-Konstante `TreatmentPlanCancellations = "treatmentPlanCancellations"` und den Pfad `/api/treatment-plan-cancellations` im `ByPath`-Map ergänzen; in `meadow-db.js` `DB_VERSION` um 1 erhöhen und den Store im `onupgradeneeded` (Z. ~79) anlegen, Schlüssel wie bei den Basisdaten-Stores. **Nicht** in `MeadowEntityType` aufnehmen (keine Outbox).

- [ ] **Schritt 8: Laden.** In `MeadowDataLoader.EnsureDashboardAsync` und im Pfad der Kuh-Detailseite `ITreatmentPlanCancellationService.GetAllDataAsync()` mit den übrigen `Ensure*Async` parallel laden. `DataScope.TreatmentPlans` muss bei Versionswechsel den Cache invalidieren — prüfen, wie `MeadowSyncState.DataVersionChanged` Scopes auf Loader-Caches abbildet, und den neuen Scope dort eintragen.

- [ ] **Schritt 9: Prüfen.** Build; App starten; per Browser-Konsole oder `curl`:

```bash
curl -s -X POST http://localhost:<port>/api/treatment-plan-cancellations -H "Content-Type: application/json" -d "{\"cowId\":\"DE01\",\"medicineId\":1,\"startTreatmentClientId\":\"11111111-1111-1111-1111-111111111111\",\"cancelledOn\":\"2026-10-19\"}"
curl -s http://localhost:<port>/api/treatment-plan-cancellations
curl -s -X DELETE http://localhost:<port>/api/treatment-plan-cancellations/11111111-1111-1111-1111-111111111111 -o /dev/null -w "%{http_code}"
```
Expected: POST 200 mit Datensatz, zweiter POST 200 ohne zweite Zeile (DB prüfen), GET listet ihn, DELETE 204, GET leer. `dotnet test Meadow.Data.Tests` grün.

- [ ] **Schritt 10: Commit (nur nach Freigabe)** — `git commit -m "Behandlungsplaene: Abbrueche speichern und laden"`

---

## Task 5: Kuh-Detailseite — Lasche „Behandlungspläne"

**Board:** „Behandlungspläne 5/9: Behandlungspläne auf der Kuh-Detailseite". Abhängig von: 4.
Akzeptanz: Neue Lasche „Behandlungspläne" (`?tab=plaene`); aktive Durchläufe oben mit „k/N", Fortschrittsbalken, Gabe-Daten, Fälligkeitstext und Status-Chip; Knopf „Gabe erfassen" öffnet den Kuhbehandlungs-Dialog für diese Kuh mit dem Medikament vorbelegt; „Abbrechen" fragt nach (optionaler Grund) und speichert; beendete Durchläufe darunter, abgebrochene mit „Abbruch zurücknehmen"; KPI-Kachel „Laufende Pläne" mit Anzahl, verlinkt auf die Lasche; `CowProfileBuilderTests` erweitert und grün.

**Files:**
- Modify: `Meadow.Shared/Profile/CowProfile.cs` (neben `OpenPlanned`, Z. ~116)
- Modify: `Meadow.Shared/Profile/CowProfileBuilder.cs:30-37`
- Modify: `Meadow.Client/Components/Ui/CowDetailTabs.cs`
- Create: `Meadow.Client/Components/Cows/CowDetail/TreatmentPlansPanel.razor`
- Create: `Meadow.Client/Components/Dialogs/CancelTreatmentPlanDialog.razor`
- Modify: `Meadow.Client/Components/Pages/Cow_Detail.razor` (Build-Aufruf 419-428, Kacheln ~65, Laschen 180-244)
- Modify: `Meadow.Client/Components/Ui/MeadowDialogLauncher.cs:63-122` (Vorbelegung Medikament)
- Test: `Meadow.Data.Tests/Profile/CowProfileBuilderTests.cs`

**Interfaces:**
- Consumes: `TreatmentPlanRuns.Build/ForCow`, `TreatmentPlanRun` (Task 3); `ITreatmentPlanCancellationService` (Task 4); `IMedicineService.Medicines`.
- Produces: `CowProfile.TreatmentPlans: IReadOnlyList<TreatmentPlanRun>`, `CowProfile.ActivePlans: int`; `CowDetailTab.TreatmentPlans` mit Slug `"plaene"`; `CowProfileBuilder.Build(..., DateTime today, IReadOnlyDictionary<int, Medicine>? medicines = null, IEnumerable<TreatmentPlanCancellation>? cancellations = null)`.

- [ ] **Schritt 1: Failing Tests** in `CowProfileBuilderTests.cs` (direkt gegen `CowProfileBuilder.Build`, die neuen Parameter sind optional, bestehende Aufrufe bleiben unverändert):

```csharp
    [Fact]
    public void TreatmentPlans_contains_only_runs_of_this_cow()
    {
        var today = new DateTime(2026, 6, 15);
        var meds = new Dictionary<int, Medicine>
        {
            [1] = new(1, "Synulox") { PlanDoseCount = 3, PlanMode = TreatmentPlanMode.Interval, PlanDays = 5 }
        };
        var mine = new CowTreatment(1, "DE01", 1, today.AddDays(-2), 1f, 1, 1);
        var other = new CowTreatment(2, "DE02", 1, today.AddDays(-2), 1f, 1, 1);

        var profile = CowProfileBuilder.Build("DE01", new[] { mine, other },
            Array.Empty<ClawTreatment>(), Array.Empty<PlannedCowTreatment>(), Array.Empty<PlannedClawTreatment>(),
            new Dictionary<int, Udder>(), today, meds, Array.Empty<TreatmentPlanCancellation>());

        var run = Assert.Single(profile.TreatmentPlans);
        Assert.Equal("DE01", run.CowId);
        Assert.Equal(1, profile.ActivePlans);
    }

    [Fact]
    public void TreatmentPlans_is_empty_without_medicines()
    {
        var today = new DateTime(2026, 6, 15);
        var profile = CowProfileBuilder.Build("DE01", new[] { new CowTreatment(1, "DE01", 1, today, 1f, 1, 1) },
            Array.Empty<ClawTreatment>(), Array.Empty<PlannedCowTreatment>(), Array.Empty<PlannedClawTreatment>(),
            new Dictionary<int, Udder>(), today);

        Assert.Empty(profile.TreatmentPlans);
        Assert.Equal(0, profile.ActivePlans);
    }
```
Run: `dotnet test Meadow.Data.Tests --filter "FullyQualifiedName~CowProfileBuilderTests"` → Build-Fehler.

- [ ] **Schritt 2: Profil erweitern.** In `CowProfile.cs`:

```csharp
    /// <summary>Behandlungsplan-Durchlaeufe dieser Kuh, aktive zuerst (TreatmentPlanRuns.ForCow).</summary>
    public required IReadOnlyList<TreatmentPlanRun> TreatmentPlans { get; init; }

    /// <summary>Anzahl aktiver Durchlaeufe - Kachel "Laufende Plaene".</summary>
    public required int ActivePlans { get; init; }
```
In `CowProfileBuilder.Build` die zwei optionalen Parameter anhängen und im Objektaufbau setzen:

```csharp
        // Nur die eigenen Behandlungen reichen: Durchlaeufe sind je Kuh unabhaengig.
        var plans = medicines is null
            ? Array.Empty<TreatmentPlanRun>()
            : TreatmentPlanRuns.ForCow(
                TreatmentPlanRuns.Build(mine /* die bereits gefilterten CowTreatments dieser Kuh */, medicines,
                    cancellations ?? Array.Empty<TreatmentPlanCancellation>(), today),
                cowId);
        // ...
            TreatmentPlans = plans,
            ActivePlans = plans.Count(p => p.IsActive),
```
(`mine` = die Variable, unter der der Builder die Kuhbehandlungen dieser Kuh schon gefiltert hat; Namen beim Lesen übernehmen.) Tests → PASS.

- [ ] **Schritt 3: Lasche.** In `CowDetailTabs.cs`: Enum-Wert `TreatmentPlans` **nach** `PlannedClaw` einfügen, in `All` aufnehmen, `Label` → `"Pläne"`, `ToSlug` → `"plaene"`, `Parse("plaene")` → `TreatmentPlans`. Falls es Tests zu `CowDetailTabs` gibt (`grep -rl CowDetailTabs Meadow.Data.Tests`), um den neuen Slug erweitern.

- [ ] **Schritt 4: Dialog** `CancelTreatmentPlanDialog.razor` (MudDialog nach dem Muster der vorhandenen Bestätigungsdialoge): Text „Behandlungsplan {Medikament} für {Kuh} abbrechen? Es gibt danach keine Erinnerungen mehr.", optionales `MudTextField` „Grund" (`MaxLength = TreatmentPlanCancellation.ReasonMaxLength`), Knöpfe „Zurück" / „Plan abbrechen" (Color.Error). Ergebnis: `DialogResult.Ok(string? reason)`.

- [ ] **Schritt 5: Panel** `TreatmentPlansPanel.razor`, Parameter `IReadOnlyList<TreatmentPlanRun> Runs`, `string CowId`, `EventCallback Changed`. Aufbau wie die anderen Panels in `Components/Cows/CowDetail/` (Panel-Rahmen, Leerzustand „Keine Behandlungspläne."). Pro Durchlauf eine Karte:
  - Kopf: Medikamentname (`IMedicineService.Medicines`), Plan-Beschreibung (`TreatmentPlanDefinition.Describe`), Status-Chip: Überfällig = Error, Heute fällig = Warning, Anstehend = Info, Abgeschlossen = Success, Unvollständig/Abgebrochen = Default.
  - `MudProgressLinear Value="@(100.0 * run.DosesDone / run.DoseCount)"` + Text `$"{run.DosesDone}/{run.DoseCount}"`.
  - Gaben als Datumsliste (`dd.MM.yyyy`).
  - Fälligkeitstext: Interval aktiv → `"nächste Gabe {NextDueDate:dd.MM.}"` bzw. `"seit {OverdueDays} Tag(en) überfällig"`; Window aktiv → `"noch {DoseCount-DosesDone} Gabe(n) bis {WindowEnd:dd.MM.}"`; abgebrochen → `"abgebrochen am {CancelledOn:dd.MM.yyyy}"` + Grund.
  - Aktiv: Knöpfe „Gabe erfassen" (`Launcher.OpenCowTreatmentAsync(CowId, run.MedicineId)` → nach Schließen `Changed`) und „Abbrechen" (Dialog → `CancelAsync(new TreatmentPlanCancellation { CowId, MedicineId, StartTreatmentClientId, CancelledOn = DateTime.Today, Reason })`; `false` → Snackbar „Abbrechen ist nur online möglich.").
  - Abgebrochen: Knopf „Abbruch zurücknehmen" → `RevokeAsync`.
  - Aktive oben, Trenner „Beendet", Rest darunter (Reihenfolge kommt schon aus `ForCow`).

- [ ] **Schritt 6: Launcher.** In `MeadowDialogLauncher.cs` der per-Kuh-Überladung von `OpenCowTreatmentAsync` einen optionalen Parameter `int? medicineId = null` geben und ihn an `Add_Cow_Treatment_Dialog` weiterreichen; im Dialog die erste Medikamentzeile (`MedicationRows`) mit dem Namen aus `IMedicineService.Medicines[medicineId]` vorbelegen. Vorbild: wie `PlannedCowPanel.razor:137-160` den Dialog vorbefüllt.

- [ ] **Schritt 7: Seite verdrahten.** In `Cow_Detail.razor`:
  - `ITreatmentPlanCancellationService` injizieren; im Build-Aufruf (419-428) `MedicineService.Medicines` und `Cancellations.Cancellations.Values` übergeben.
  - Lasche rendern (180-244): `case CowDetailTab.TreatmentPlans: <TreatmentPlansPanel Runs="_profile.TreatmentPlans" CowId="@CowId" Changed="ReloadAsync" />` (Name der vorhandenen Neu-Lade-Methode übernehmen).
  - KPI-Kachel „Laufende Pläne" mit `_profile.ActivePlans`, href `?tab=plaene`, nach Vorbild der Kachel „offene geplante Termine"; Warn-Optik, wenn ein Durchlauf `IsDue` ist.

- [ ] **Schritt 8: Prüfen.** `dotnet test Meadow.Data.Tests` grün; App: Medikament mit Plan „3× alle 5 Tage", Kuh einmal damit behandeln → Lasche zeigt „1/3", nächste Gabe in 5 Tagen; „Gabe erfassen" → Dialog mit Medikament vorbelegt, speichern → „2/3"; „Abbrechen" → Status abgebrochen, Kachel 0; „Abbruch zurücknehmen" → wieder aktiv. Offline (DevTools „Offline") → Gabe erfassen geht, Zählung springt auf „3/3", Abbrechen meldet „nur online".

- [ ] **Schritt 9: Commit (nur nach Freigabe)** — `git commit -m "Behandlungsplaene: Lasche auf der Kuh-Detailseite"`

---

## Task 6: Startseite — Karte „Laufende Behandlungspläne"

**Board:** „Behandlungspläne 6/9: Laufende Behandlungspläne auf der Startseite". Abhängig von: 4.
Akzeptanz: Auf `/app` erscheint unter der oberen Reihe eine Karte mit allen aktiven Durchläufen nicht abgegangener Kühe, sortiert Überfällig → Heute fällig → Anstehend; Zeile = Kuh (Link auf `/Kuh/{CowId}?tab=plaene`), Medikament, „k/N", Fälligkeitstext, farbiger Status; ohne aktive Durchläufe ist die Karte unsichtbar; neue Behandlungen (auch offline) aktualisieren die Karte ohne Reload.

**Files:**
- Create: `Meadow.Client/Components/Ui/MeadowTreatmentPlanCard.razor`
- Modify: `Meadow.Client/Components/Pages/Index.razor` (nach `.mw-dash-top`, vor `.mw-dash-charts` ~Z. 117; `OnInitializedAsync` ~224)
- Modify: zugehörige CSS-Datei des Dashboards (`Index.razor.css` oder das globale Meadow-Stylesheet, in dem `.mw-dash-top` steht)

**Interfaces:**
- Consumes: `TreatmentPlanRuns.Build/ActiveForDashboard` (Task 3); `ICowTreatmentService.Treatments`, `IMedicineService.Medicines`, `ICowService.Cows`, `ITreatmentPlanCancellationService.Cancellations` (Task 4).
- Produces: Komponente `<MeadowTreatmentPlanCard />` ohne Parameter.

- [ ] **Schritt 1: Vorbild lesen.** `Ui/MeadowNoticeBoard.razor` (blendet sich selbst aus, abonniert Änderungen) und `MeadowChartCard` (Karten-Rahmen, Titel) lesen; Optik und Lebenszyklus übernehmen.

- [ ] **Schritt 2: Komponente bauen.**

```razor
@implements IDisposable
@inject ICowTreatmentService CowTreatments
@inject IMedicineService Medicines
@inject ICowService Cows
@inject ITreatmentPlanCancellationService Cancellations
@inject MeadowDataChanges DataChanges

@if (_runs.Count > 0)
{
    <section class="mw-plan-card">
        <header class="mw-plan-card__head">
            <span>Laufende Behandlungspläne</span>
            <span class="mw-plan-card__count">@_runs.Count</span>
        </header>
        <ul class="mw-plan-card__list">
            @foreach (var run in _runs)
            {
                <li class="mw-plan-card__row mw-plan-card__row--@run.Status.ToString().ToLowerInvariant()">
                    <a href="@($"/Kuh/{Uri.EscapeDataString(run.CowId)}?tab=plaene")">@CowLabel(run.CowId)</a>
                    <span>@Medicines.Medicines.GetValueOrDefault(run.MedicineId)?.MedicineName</span>
                    <span>@run.DosesDone/@run.DoseCount</span>
                    <span>@DueText(run)</span>
                </li>
            }
        </ul>
    </section>
}

@code {
    private IReadOnlyList<TreatmentPlanRun> _runs = Array.Empty<TreatmentPlanRun>();

    protected override void OnInitialized()
    {
        Recompute();
        DataChanges.Changed += OnChanged;
    }

    private void OnChanged()
    {
        Recompute();
        InvokeAsync(StateHasChanged);
    }

    private void Recompute()
    {
        var today = DateTime.Today;
        var all = TreatmentPlanRuns.Build(CowTreatments.Treatments.Values, Medicines.Medicines,
            Cancellations.Cancellations.Values, today);
        _runs = TreatmentPlanRuns.ActiveForDashboard(all, Cows.Cows);
    }

    private string CowLabel(string cowId)
    {
        var cow = CowLookups.GetById(Cows.Cows, cowId);
        return cow is { CollarNumber: > 0 } ? $"{cow.EarTagNumber ?? cowId} ({cow.CollarNumber})" : cow?.EarTagNumber ?? cowId;
    }

    private static string DueText(TreatmentPlanRun run) => run.Status switch
    {
        TreatmentPlanRunStatus.Overdue => run.OverdueDays(DateTime.Today) == 1
            ? "seit 1 Tag überfällig" : $"seit {run.OverdueDays(DateTime.Today)} Tagen überfällig",
        TreatmentPlanRunStatus.DueToday => "heute fällig",
        _ when run.NextDueDate is { } next => $"nächste Gabe {next:dd.MM.}",
        _ => $"bis {run.WindowEnd:dd.MM.}"
    };

    public void Dispose() => DataChanges.Changed -= OnChanged;
}
```
`MeadowDataChanges.Changed`: Signatur prüfen (Action vs. Action<T>) und den Handler anpassen. Prüfen, ob Kuh-Labels schon irgendwo zentral formatiert werden (`grep -rn "CollarNumber" Meadow.Client/Components/Ui`); wenn ja, das verwenden statt `CowLabel`.

- [ ] **Schritt 3: Einbinden.** In `Index.razor` zwischen `.mw-dash-top` und `.mw-dash-charts` `<MeadowTreatmentPlanCard />`. In `OnInitializedAsync` sicherstellen, dass `EnsureDashboardAsync` (Task 4) die Abbrüche und Medikamente geladen hat, bevor die Karte rechnet — sonst nach dem Laden `StateHasChanged` auslösen.

- [ ] **Schritt 4: CSS.** Zeilen als Grid (`grid-template-columns: minmax(8rem,1fr) 1fr auto auto`), Statusfarben über vorhandene Meadow-Token (`--mw-…`): überfällig = Fehlerfarbe, heute = Warnfarbe. Unter 600px zweizeilig. Light/Dark prüfen.

- [ ] **Schritt 5: Prüfen.** App: Kühe mit überfälligem, heute fälligem und anstehendem Durchlauf → Reihenfolge stimmt; Kuh als abgegangen markieren → verschwindet; letzte Gabe erfassen → Durchlauf verschwindet ohne Reload; keine aktiven → Karte weg. Mobile-Breite (375px) ohne horizontales Scrollen.

- [ ] **Schritt 6: Commit (nur nach Freigabe)** — `git commit -m "Behandlungsplaene: Karte auf der Startseite"`

---

## Task 7: Hinweistafel — fällige Gaben melden

**Board:** „Behandlungspläne 7/9: Hinweise für fällige Behandlungsplan-Gaben". Abhängig von: 4.
Akzeptanz: Für jeden heute fälligen (Warning) und überfälligen (Critical) Durchlauf erscheint ein Hinweis „Gabe k/N {Medikament} heute fällig: {Kuh}" bzw. „… seit t Tag(en) überfällig: {Kuh}" mit Aktion „Ansehen" → `/Kuh/{CowId}?tab=plaene`; abgegangene Kühe erzeugen keinen Hinweis; der Hinweis verschwindet nach Erfassen der Gabe oder Abbruch ohne Reload; Texte stehen in `Meadow.Shared` und sind getestet.

**Files:**
- Modify: `Meadow.Shared/Lookups/CowLookups.cs` (neue Methode `CollarSuffix`)
- Modify: `Meadow.Shared/Lookups/BandageReminderText.cs` (private `CollarPart` durch `CowLookups.CollarSuffix` ersetzen)
- Create: `Meadow.Shared/Lookups/TreatmentPlanReminderText.cs`
- Create: `Meadow.Client/Services/TreatmentPlanNoticeProvider.cs`
- Modify: `Meadow.Client/Program.cs` (Registrierung wie `BandageReminderNoticeProvider`, Z. 135/143/220)
- Test: `Meadow.Data.Tests/Lookups/TreatmentPlanReminderTextTests.cs`

**Interfaces:**
- Consumes: `TreatmentPlanRun`, `TreatmentPlanRuns.Due` (Task 3); `HttpTreatmentPlanCancellationService.Changed` (Task 4).
- Produces:
  - `CowLookups.CollarSuffix(IReadOnlyDictionary<string, Cow> cows, string cowId): string` — `" (Halsband 12)"` oder `""`
  - `TreatmentPlanReminderText.For(TreatmentPlanRun run, string medicineName, IReadOnlyDictionary<string, Cow> cows, DateTime today): string`
  - `TreatmentPlanReminderText.PushTitle: string` = `"Behandlungspläne"`
  - `TreatmentPlanReminderText.PushSummary(IReadOnlyCollection<TreatmentPlanRun> dueRuns): string?` — `null`, wenn nichts fällig

- [ ] **Schritt 1: Failing Tests**

```csharp
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Wortlaut der Behandlungsplan-Erinnerungen. Eine Textstelle fuer Hinweis
/// (Client) und Push (Server) - wie beim Verband.
/// </summary>
public class TreatmentPlanReminderTextTests
{
    private static readonly DateTime Today = new(2026, 6, 15);

    private static readonly IReadOnlyDictionary<string, Cow> Cows = new Dictionary<string, Cow>
    {
        ["DE01"] = new("DE01", "DE01", 12, false, false),
        ["DE02"] = new("DE02", "DE02", 0, false, false)
    };

    private static TreatmentPlanRun Run(string cowId, TreatmentPlanRunStatus status, int done = 1, DateTime? nextDue = null)
        => new()
        {
            CowId = cowId,
            MedicineId = 1,
            StartTreatmentClientId = Guid.NewGuid(),
            Mode = TreatmentPlanMode.Interval,
            DoseCount = 3,
            PlanDays = 5,
            Doses = Enumerable.Range(0, done).Select(i => new CowTreatment(i + 1, cowId, 1, Today.AddDays(-10 + i), 1f, 1, 1)).ToList(),
            Status = status,
            NextDueDate = nextDue
        };

    [Fact]
    public void Due_today_names_next_dose_medicine_and_cow_with_collar()
        => Assert.Equal("Gabe 2/3 Synulox heute fällig: DE01 (Halsband 12)",
            TreatmentPlanReminderText.For(Run("DE01", TreatmentPlanRunStatus.DueToday, nextDue: Today), "Synulox", Cows, Today));

    [Fact]
    public void Overdue_counts_days_singular_and_plural()
    {
        Assert.Equal("Gabe 2/3 Synulox seit 1 Tag überfällig: DE02",
            TreatmentPlanReminderText.For(Run("DE02", TreatmentPlanRunStatus.Overdue, nextDue: Today.AddDays(-1)), "Synulox", Cows, Today));
        Assert.Equal("Gabe 3/3 Synulox seit 4 Tagen überfällig: DE02",
            TreatmentPlanReminderText.For(Run("DE02", TreatmentPlanRunStatus.Overdue, done: 2, nextDue: Today.AddDays(-4)), "Synulox", Cows, Today));
    }

    [Fact]
    public void Push_summary_is_null_when_nothing_is_due()
        => Assert.Null(TreatmentPlanReminderText.PushSummary(Array.Empty<TreatmentPlanRun>()));

    [Fact]
    public void Push_summary_counts_cows_due_and_overdue()
    {
        var runs = new[]
        {
            Run("DE01", TreatmentPlanRunStatus.DueToday, nextDue: Today),
            Run("DE02", TreatmentPlanRunStatus.DueToday, nextDue: Today),
            Run("DE03", TreatmentPlanRunStatus.Overdue, nextDue: Today.AddDays(-2))
        };
        Assert.Equal("2 Kühe heute fällig, 1 Kuh überfällig", TreatmentPlanReminderText.PushSummary(runs));
    }

    [Fact]
    public void Push_summary_counts_a_cow_once_even_with_two_due_plans()
    {
        var runs = new[]
        {
            Run("DE01", TreatmentPlanRunStatus.DueToday, nextDue: Today),
            Run("DE01", TreatmentPlanRunStatus.DueToday, nextDue: Today) with { MedicineId = 2 }
        };
        Assert.Equal("1 Kuh heute fällig", TreatmentPlanReminderText.PushSummary(runs));
    }

    [Fact]
    public void Push_summary_only_overdue()
        => Assert.Equal("1 Kuh überfällig",
            TreatmentPlanReminderText.PushSummary(new[] { Run("DE01", TreatmentPlanRunStatus.Overdue, nextDue: Today.AddDays(-1)) }));
}
```
Regel für die Sammelzahl: Kühe werden je Status einmal gezählt; eine Kuh, die einen überfälligen **und** einen heute fälligen Plan hat, zählt nur als überfällig.

Run: `dotnet test Meadow.Data.Tests --filter "FullyQualifiedName~TreatmentPlanReminderTextTests"` → Build-Fehler.

- [ ] **Schritt 2: `CollarSuffix` herausziehen.** In `CowLookups`:

```csharp
    /// <summary>" (Halsband 12)" oder leer - gemeinsamer Kuh-Zusatz der Erinnerungstexte.</summary>
    public static string CollarSuffix(IReadOnlyDictionary<string, Cow> cows, string cowId)
    {
        if (cows is null || string.IsNullOrWhiteSpace(cowId))
        {
            return string.Empty;
        }

        var cow = GetById(cows, cowId);
        return cow is null || cow.CollarNumber <= 0 ? string.Empty : $" (Halsband {cow.CollarNumber})";
    }
```
In `BandageReminderText` die private `CollarPart` löschen und `CowLookups.CollarSuffix(cows, treatment.EarTagNumber)` aufrufen. `BandageReminderTextTests` müssen unverändert grün bleiben.

- [ ] **Schritt 3: `TreatmentPlanReminderText` implementieren**

```csharp
using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Wortlaut der Behandlungsplan-Erinnerungen - dieselbe Quelle fuer den
/// In-App-Hinweis (Client) und die taegliche Sammel-Push (Server).
/// Dringlichkeit zuerst, dann das Tier - wie beim Verband.
/// </summary>
public static class TreatmentPlanReminderText
{
    public const string PushTitle = "Behandlungspläne";

    public static string For(TreatmentPlanRun run, string medicineName, IReadOnlyDictionary<string, Cow> cows, DateTime today)
    {
        var dose = $"Gabe {run.DosesDone + 1}/{run.DoseCount} {medicineName}";
        var when = run.Status == TreatmentPlanRunStatus.Overdue
            ? $"seit {Days(run.OverdueDays(today))} überfällig"
            : "heute fällig";
        return $"{dose} {when}: {run.CowId}" + CowLookups.CollarSuffix(cows, run.CowId);
    }

    public static string? PushSummary(IReadOnlyCollection<TreatmentPlanRun> dueRuns)
    {
        var overdueCows = dueRuns.Where(r => r.Status == TreatmentPlanRunStatus.Overdue)
            .Select(r => r.CowId).ToHashSet(StringComparer.Ordinal);
        var dueTodayCows = dueRuns.Where(r => r.Status == TreatmentPlanRunStatus.DueToday)
            .Select(r => r.CowId).Where(c => !overdueCows.Contains(c)).ToHashSet(StringComparer.Ordinal);

        var parts = new List<string>();
        if (dueTodayCows.Count > 0)
        {
            parts.Add($"{Cows(dueTodayCows.Count)} heute fällig");
        }

        if (overdueCows.Count > 0)
        {
            parts.Add($"{Cows(overdueCows.Count)} überfällig");
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string Days(int n) => n == 1 ? "1 Tag" : $"{n} Tagen";

    private static string Cows(int n) => n == 1 ? "1 Kuh" : $"{n} Kühe";
}
```
Hinweis: `run.CowId` ist die `Cow_ID`; der Verband-Text zeigt ebenfalls `EarTagNumber` (= Cow_ID) plus Halsband — gleich halten. Tests → PASS.

- [ ] **Schritt 4: Provider** `TreatmentPlanNoticeProvider : INoticeProvider, IDisposable` nach `BandageReminderNoticeProvider.cs` (gleicher Konstruktor-Stil, `Source`-Konstante `"TreatmentPlanReminder"`):

```csharp
    public IEnumerable<MeadowNotice> GetNotices()
    {
        var today = DateTime.Now;
        var runs = TreatmentPlanRuns.Build(_treatments.Treatments.Values, _medicines.Medicines,
            _cancellations.Cancellations.Values, today);

        return TreatmentPlanRuns.Due(runs, _cows.Cows)
            .Select(run => new MeadowNotice
            {
                // Stabil je Durchlauf - flackert nicht bei Neuberechnung.
                Id = $"treatment-plan-{run.StartTreatmentClientId}",
                Text = TreatmentPlanReminderText.For(run,
                    _medicines.Medicines.GetValueOrDefault(run.MedicineId)?.MedicineName ?? "Medikament",
                    _cows.Cows, today),
                Severity = run.Status == TreatmentPlanRunStatus.Overdue
                    ? MeadowNoticeSeverity.Critical
                    : MeadowNoticeSeverity.Warning,
                ActionHref = $"/Kuh/{Uri.EscapeDataString(run.CowId)}?tab=plaene",
                ActionLabel = "Ansehen",
                Source = Source
            })
            .ToList();
    }
```
`Changed` auslösen bei `MeadowSyncState.DataArrived(MeadowDataKind.CowTreatment)` und bei `HttpTreatmentPlanCancellationService.Changed`; beide in `Dispose` abmelden. In `Meadow.Client/Program.cs` registrieren und bei `MeadowNoticeState.AddProvider` anmelden — exakt an den drei Stellen, an denen `BandageReminderNoticeProvider` steht.

- [ ] **Schritt 5: Provider-Test** (optional, wenn `Notices/MeadowNoticeStateTests.cs` schon Fakes für Services hat): einen Test ergänzen, der mit einem fälligen Durchlauf genau einen Hinweis mit Id `treatment-plan-{guid}` und Severity `Warning` erwartet. Ohne vorhandene Fakes entfällt der Schritt — die Logik ist über `TreatmentPlanRunsTests` und `TreatmentPlanReminderTextTests` abgedeckt.

- [ ] **Schritt 6: Prüfen.** `dotnet test Meadow.Data.Tests` grün; App: Kuh mit heute fälliger Gabe → Hinweistafel zeigt gelben Hinweis, überfällig → roter; „Ansehen" springt in die Lasche; Gabe erfassen → Hinweis verschwindet.

- [ ] **Schritt 7: Commit (nur nach Freigabe)** — `git commit -m "Behandlungsplaene: Hinweise auf der Hinweistafel"`

---

## Task 8: Web-Push — tägliche Sammelerinnerung

**Board:** „Behandlungspläne 8/9: Tägliche Push-Erinnerung für Behandlungspläne". Abhängig von: 7.
Akzeptanz: Ein Hintergrunddienst schickt höchstens einmal pro Tag (auch über Neustarts) eine Push „Behandlungspläne — 2 Kühe heute fällig, 1 Kuh überfällig" an alle Abonnenten, Klick öffnet `/app`; nichts fällig → keine Push und kein Log-Eintrag; Intervall per Env `TreatmentPlanReminderIntervalHours`; ohne VAPID-Konfiguration passiert nichts.

**Files:**
- Create: `Meadow.Shared/Models/TreatmentPlanPushLog.cs`
- Modify: `Meadow.Data/SQL/DatabaseContext.cs` (DbSet + Unique-Index auf `SentOn`)
- Create: `Meadow.Data/Migrations/*_AddTreatmentPlanPushLog.cs` (generiert)
- Modify: `Meadow.Api/Infrastructure/PushSender.cs` (Interface ~Z. 28-40, Implementierung nach Z. 136)
- Create: `Meadow.Api/BackgroundServices/TreatmentPlanPushBackgroundService.cs`
- Modify: `Meadow.Api/Program.cs` (`AddHostedService`, neben dem Verband-Dienst)
- Test: `Meadow.Data.Tests/Lookups/PushReminderDedupTests.cs` (ein Fall ergänzen)

**Interfaces:**
- Consumes: `TreatmentPlanRuns.Build/Due` (Task 3), `ITreatmentPlanCancellationService` (Task 4), `TreatmentPlanReminderText.PushTitle/PushSummary` (Task 7), `PushLogic.ShouldSendReminderToday(DateTime? lastSentOn, DateTime today)` (vorhanden).
- Produces: `IPushSender.SendTreatmentPlanReminderToAllAsync(string body, CancellationToken ct = default): Task<int>`; Entität `TreatmentPlanPushLog { int TreatmentPlanPushLogId; DateTime SentOn }`.

- [ ] **Schritt 1: Dedup-Test ergänzen** in `PushReminderDedupTests.cs` (sichert ab, dass die vorhandene Regel auch für das Tagesdatum als Schlüssel trägt):

```csharp
    [Fact]
    public void Daily_summary_is_sent_once_per_day_keyed_by_date()
    {
        var today = new DateTime(2026, 6, 15, 7, 0, 0);
        Assert.True(PushLogic.ShouldSendReminderToday(null, today));
        Assert.False(PushLogic.ShouldSendReminderToday(today.Date, today.AddHours(10)));
        Assert.True(PushLogic.ShouldSendReminderToday(today.Date.AddDays(-1), today));
    }
```
Run: `dotnet test Meadow.Data.Tests --filter "FullyQualifiedName~PushReminderDedupTests"` → PASS (Regel existiert; der Test dokumentiert die Wiederverwendung).

- [ ] **Schritt 2: Entität + Migration**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Meadow.Shared.Models;

/// <summary>
/// Merker "Sammel-Push Behandlungsplaene an diesem Tag verschickt". Eigene
/// Tabelle wie PushReminderLog: ein Senden darf keine Datenversion bumpen.
/// Unique auf SentOn - ein zweiter Lauf am selben Tag scheitert am Index,
/// falls zwei Instanzen gleichzeitig senden wollten.
/// </summary>
[Table("Treatment_Plan_Push_Log")]
public class TreatmentPlanPushLog
{
    [Key]
    [Column("Treatment_Plan_Push_Log_ID")]
    public int TreatmentPlanPushLogId { get; set; }

    [Required]
    [Column("Sent_On")]
    public DateTime SentOn { get; set; }
}
```
DbSet `TreatmentPlanPushLogs`, `HasIndex(l => l.SentOn).IsUnique()`. `dotnet ef migrations add AddTreatmentPlanPushLog --project Meadow.Data --startup-project Meadow.Data` → genau `CreateTable` + `CreateIndex`.

- [ ] **Schritt 3: Sender.** In `IPushSender` und `WebPushSender`:

```csharp
    public Task<int> SendTreatmentPlanReminderToAllAsync(string body, CancellationToken ct = default)
        => BroadcastAsync(title: TreatmentPlanReminderText.PushTitle, body: body, url: "/app", ct);
```
Falls es einen Fake-`IPushSender` in Tests gibt (`grep -rl "IPushSender" Meadow.Data.Tests`), dort ergänzen.

- [ ] **Schritt 4: Hintergrunddienst** `TreatmentPlanPushBackgroundService` — `BandageReminderPushBackgroundService.cs` vollständig lesen und **dieselbe Struktur** verwenden (PeriodicTimer, Env-Intervall mit gleichem Fallback, Erstlauf beim Start, `IsEnabled`-Prüfung, Cache-Reload, Fehlerbehandlung, `MarkSentAsync` mit `CancellationToken.None`). Kern der Sende-Methode:

```csharp
    private async Task SendDueReminderAsync(CancellationToken cancellationToken)
    {
        if (!_pushSender.IsEnabled)
        {
            return;
        }

        var today = DateTime.Today;
        await Task.WhenAll(_cowTreatments.GetAllDataAsync(), _medicines.GetAllDataAsync(),
            _cows.GetAllDataAsync(), _cancellations.GetAllDataAsync());

        var runs = TreatmentPlanRuns.Build(_cowTreatments.Treatments.Values, _medicines.Medicines,
            _cancellations.Cancellations.Values, today);
        var body = TreatmentPlanReminderText.PushSummary(TreatmentPlanRuns.Due(runs, _cows.Cows));
        if (body is null)
        {
            return; // nichts faellig -> kein Senden, kein Merker
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var lastSentOn = await context.TreatmentPlanPushLogs
            .OrderByDescending(l => l.SentOn)
            .Select(l => (DateTime?)l.SentOn)
            .FirstOrDefaultAsync(cancellationToken);
        if (!PushLogic.ShouldSendReminderToday(lastSentOn, today))
        {
            return;
        }

        await _pushSender.SendTreatmentPlanReminderToAllAsync(body, cancellationToken);
        // Merker nach dem Senden, nicht abbrechbar - Begruendung wie im Verband-Dienst.
        context.TreatmentPlanPushLogs.Add(new TreatmentPlanPushLog { SentOn = today });
        await context.SaveChangesAsync(CancellationToken.None);
    }
```
Env-Variable `TreatmentPlanReminderIntervalHours`. In `Program.cs` `builder.Services.AddHostedService<TreatmentPlanPushBackgroundService>();` neben dem Verband-Dienst. Falls `docker-compose*.yml`/Deploy-Doku die Verband-Variable listet, die neue dort ergänzen.

- [ ] **Schritt 5: Prüfen.** Build + `dotnet test Meadow.Data.Tests` grün. App mit VAPID-Dev-Keys starten, im Browser Push abonnieren (Einstellungen), heute fälligen Durchlauf anlegen, `TreatmentPlanReminderIntervalHours=0.01` setzen → genau **eine** Push, Klick öffnet `/app`; `select * from Treatment_Plan_Push_Log` → eine Zeile mit heutigem Datum; App neu starten → keine zweite Push.

- [ ] **Schritt 6: Commit (nur nach Freigabe)** — `git commit -m "Behandlungsplaene: taegliche Push-Erinnerung"`

---

## Task 9: Demo-Daten und Abnahme

**Board:** „Behandlungspläne 9/9: Demo-Daten und End-to-End-Abnahme". Abhängig von: 2, 5, 6, 7, 8.
Akzeptanz: Der Demo-Seeder legt zwei Medikamente mit Plan an (eins `Window`, eins `Interval`) und Behandlungen so, dass nach dem Seed je mindestens ein Durchlauf überfällig, heute fällig, anstehend, abgeschlossen und abgebrochen ist; alle Akzeptanzkriterien der Tickets 1–8 sind in der App nachgeprüft (Desktop + 375px, Light + Dark, online + offline).

**Files:**
- Modify: `Meadow.Data/SQL/DemoDataSeeder.cs`

**Interfaces:**
- Consumes: alles aus Task 1–8.

- [ ] **Schritt 1: Seeder lesen.** Wie Medikamente und Kuhbehandlungen relativ zu „heute" erzeugt werden (Seeder läuft nachts um 03:00 neu).
- [ ] **Schritt 2: Seed ergänzen.** Zwei Medikamente setzen/anlegen: „Synulox RTU" `3× alle 2 Tage` (Interval), „Metacam 20 mg/ml" `3× innerhalb von 3 Tagen` (Window); `PlanValidFrom = heute − 30`. Behandlungen relativ zu `today`: Kuh A Synulox −4 (überfällig), Kuh B Synulox −2 (heute fällig), Kuh C Metacam −1 (heute fällig: 2 Gaben in 2 Tagen), Kuh D Synulox −1 (anstehend), Kuh E Synulox −10/−8/−6 (abgeschlossen), Kuh F Synulox −3 plus Abbruch heute −2 (abgebrochen). Kuh-IDs aus den vorhandenen Demo-Kühen nehmen.
- [ ] **Schritt 3: Abnahme** (App mit Demo-Daten, Checkliste):
  - Basisdaten: Spalte „Behandlungsplan" zeigt beide Pläne; Bearbeiten/Abschalten funktioniert.
  - Startseite: Karte mit A (überfällig), B, C (heute), D (anstehend) in dieser Reihenfolge; Hinweistafel: A rot, B und C gelb.
  - Kuh B: Lasche „Pläne" zeigt 1/3, „Gabe erfassen" → 2/3, Hinweis verschwindet, Karte zeigt „nächste Gabe in 2 Tagen".
  - Kuh F: abgebrochen mit Datum, „Abbruch zurücknehmen" → wird überfällig und erscheint auf der Startseite.
  - Kuh E: abgeschlossen unter „Beendet".
  - Offline: Gabe erfassen zählt sofort; Abbrechen meldet „nur online"; Karte und Lasche zeigen Snapshot.
  - Push: eine Sammelnachricht am Tag.
  - 375px und Dark Mode: kein horizontales Scrollen, Statusfarben lesbar.
- [ ] **Schritt 4: Commit (nur nach Freigabe)** — `git commit -m "Behandlungsplaene: Demo-Daten"`

---

## Folgekandidaten (nicht Teil dieses Plans)

- Hinweis im Behandlungsdialog: „Diese Gabe ist 2/3 des Behandlungsplans" bzw. „startet Behandlungsplan".
- Plan-Parameter beim Start eines Durchlaufs einfrieren (würde einen gespeicherten Durchlauf brauchen).
- Intervalle in Stunden.
