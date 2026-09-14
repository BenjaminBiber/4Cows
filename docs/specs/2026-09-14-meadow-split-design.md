> **Status:** Entwurfsvorlage für den Umbau, angenommen am 14.09.2026.
> Der Abnahmemaßstab ist der Auftrag, nicht dieses Dokument.
> **Zwölf Stellen dieses Entwurfs sind gegen die Codebasis geprüft und falsch** —
> sie stehen im [Nachtrag](#nachtrag-1409-2026--verifizierte-korrekturen) am Ende.
> Lies den Nachtrag, bevor du einen Abschnitt umsetzt.

# Split 4Cows in Meadow.Api + Meadow.Client (Blazor WASM, PWA, Offline-fähig)

## Context

4Cows läuft heute als .NET 8 Blazor Web App mit global `InteractiveServer`
([App.razor:67](4Cows-FE/Components/App.razor:67)). Jede Seite hängt an einem SignalR-Circuit.
Auf dem Handy im Stall reicht ein Sperrbildschirm, ein App-Wechsel oder ein WLAN→LTE-Wechsel,
und der Circuit stirbt — der Reconnect-Dialog erscheint. Das ist kein Bug, sondern die Bauart
von Blazor Server.

Drei Ziele treiben den Umbau:

1. **Reconnect-Dialog strukturell loswerden.** Ohne Circuit gibt es ihn nicht.
2. **PWA** — installierbar, Homescreen-Icon. Mit Blazor Server prinzipbedingt unmöglich.
3. **Offline arbeiten.** Behandlungen im Stall erfassen, auch ohne Verbindung; Übertragung,
   sobald das Netz wieder da ist.

**Framework-Entscheidung: Blazor WebAssembly, nicht React/Vue.** Bei einem Wechsel auf ein
JS-Framework wären 13.032 LOC Razor und das Meadow-Designsystem (50 Dateien, 5.253 LOC) neu zu
bauen. Mit WASM bleiben alle 84 Komponenten und MudBlazor unverändert; es ändert sich nur, woher
die Daten kommen. Das ist der Unterschied zwischen Refactoring und Rewrite.

**Entscheidungen aus der Vorbesprechung:** LAN-only, keine Authentifizierung im Umfang.
PWA im Umfang, HTTPS über Reverse Proxy mit echtem Zertifikat. Demo-Modus bleibt in Betrieb,
die Landing-Page darf durch WASM laufen.

---

## Zielarchitektur

```
Meadow.sln                          (bisher BB_Cow.sln)
├── Meadow.Shared      classlib     Entities-als-DTOs, KPI-/Profile-Modelle, reine Helfer, Interfaces
├── Meadow.Data        classlib     bisher BBCowDataLibrary — EF, DbContext, Migrations, Services
├── Meadow.Api         Sdk.Web      Controller + hostet den Client (ein Container, ein Port)
├── Meadow.Client      WASM + PWA   alle .razor, Meadow-Designsystem, CSS, wwwroot, Offline-Store
└── Meadow.Data.Tests               bisher BBCowDataLibrary.Tests
```

Umbenannt werden Projekte **und** Namespaces (`BB_Cow.Class` → `Meadow.Shared.Models`,
`BB_Cow.Services` → `Meadow.Data.Services` usw.) — in Phase 0 als eine IDE-Refactoring-Operation.
`CowTreatmentService` liegt heute im globalen Namespace und bekommt dabei einen.
Die 12 Testdateien ändern sich dadurch nur in ihren `using`-Zeilen.

**Der zentrale Hebel — die Interface-Naht.** Heute gibt es keine Interfaces (außer
`IKpiLookups`). Pro Service kommt eins nach `Meadow.Shared`, mit **identischen Signaturen**:

```
Server:  CowService     : ICowService    → EF, wie heute
Client:  HttpCowService : ICowService    → HttpClient + lokaler Cache, gleiche Signatur
Razor:   @inject ICowService CowService  → einzige Änderung in den Komponenten
```

Dadurch bleiben 83 der 84 Razor-Dateien inhaltlich unangetastet. Die Ausnahme ist
[Claw_Table.razor](4Cows-FE/Components/Pages/Tables/Claw_Table.razor) (Excel-Export, s. Phase 2).

**Ein Deployment, nicht zwei.** `Meadow.Api` liefert den Client per `UseBlazorFrameworkFiles()` +
`MapFallbackToFile("index.html")` aus.

---

## Was sich NICHT ändert

- Alle 84 Razor-Komponenten, das Meadow-Designsystem, 4.616 LOC CSS, MudBlazor 7.15
- `Meadow.Data` ist bereits blazorfrei (verifiziert: null Treffer für `Microsoft.AspNetCore`,
  `MudBlazor`, `IJSRuntime`, `ComponentBase`) — EF, Migrations und Seeder wandern unverändert
- Serialisierung ist unkritisch: keine Navigation Properties, keine Zyklen, kein
  `DateOnly`/`TimeSpan`/`decimal` in den 11 Entities
- Das Theme-Boot-Script ([App.razor:23-35](4Cows-FE/Components/App.razor:23)) zieht unverändert
  nach `index.html`

## Nicht im Umfang

Authentifizierung · getrennte DTO-Schicht neben den Entities · Umbau der
`bool`/`"--"`-Fehlerkonvention · native Store-App · Mandantenfähigkeit ·
**automatische Konfliktauflösung** beim Sync (s. Phase 4).

> ⚠️ Der Reverse Proxy darf **nicht** öffentlich erreichbar werden. Ohne Auth ist
> `POST /api/kpi/...` in einem von außen erreichbaren Netz nicht vertretbar — siehe den
> Kommentar in [KPIService.cs:111](BBCowDataLibrary/Services/KPIService.cs:111).

---

## Phase 0 — Umbenennen + Naht einziehen (App läuft durchgehend als Blazor Server)

Kein sichtbarer Effekt, größter Brocken mechanischer Arbeit. Danach läuft die App wie heute.

1. **Umbenennen:** `BBCowDataLibrary` → `Meadow.Data`, `BBCowDataLibrary.Tests` →
   `Meadow.Data.Tests`, `BB_Cow.sln` → `Meadow.sln`. Namespaces per IDE-Refactoring mitziehen.
   Docker-Compose, `Dockerfile` und die vier GitHub-Workflows referenzieren Projektpfade —
   mit anpassen.
2. `Meadow.Shared` anlegen. `Models/`, die KPI-Modelltypen (`KpiRow`, `KpiTileModel`, `KpiResult`,
   `KpiDefinitionSummary`, `KpiDrillDownUrl`) und `Profile/` dorthin verschieben. In Shared nur
   `Microsoft.EntityFrameworkCore.Abstractions` referenzieren (~30 KB), nicht das volle EF;
   gebraucht wird es allein für `[Index]` in [Cow.cs:8](BBCowDataLibrary/Models/Cow.cs:8).
3. 11 Interfaces nach Shared extrahieren: `ICowService`, `IClawTreatmentService`,
   `ICowTreatmentService`, `IPClawTreatmentService`, `IPCowTreatmentService`, `IMedicineService`,
   `IWhereHowService`, `ITreatmentReasonService`, `IUdderService`, `ISettingsService`, `IKpiService`.
   `KPIService.GetKPIValueAsync(DatabaseContext, …)` gehört **nicht** aufs Interface — die
   Überladung bleibt serverintern (sie teilt bewusst eine Connection über alle SQL-KPIs,
   [KPIService.cs:190](BBCowDataLibrary/Services/KPIService.cs:190)).
4. `@inject` in ~40 Razor-Dateien und die DI-Registrierung in
   [Program.cs:56-75](4Cows-FE/Program.cs:56) auf Interfaces umstellen.
5. Die ~30 reinen Compute-Methoden zu statischen Helfern in Shared ziehen — sie brauchen
   **keinen Endpunkt** und dürfen keiner werden: alle `GetById`, `Get*NameById`,
   `GetCollarNumberByCowId` (21 Aufrufstellen), `GetEarTagDisplay` (13), `GetDisplayLabel`,
   `FilterFuncCow`, `SearchCows`, `SearchAsync`, `GetMinYear`, `Get*ChartData`, `GetUdderString`.
6. Service-als-Parameter auflösen: `WhereHowService.GetFullWhereHowName(id, UdderService, udderId)`
   und `CowTreatmentService.SearchCowTreatmentMedicaments(value, token, MedicineService)`.
7. Die vier Find-or-Create-Methoden zu **transaktionalen Upserts** machen:
   `UdderService.GetIDByBools:83`, `WhereHowService.GetWhereHowIDByName:277`,
   `MedicineService.GetMedicineIdByName:323`, `TreatmentReasonService.GetIdByNameAsync:289`.
   Heute Check-then-Insert ohne Transaktion; mit mehreren Tablets erzeugen zwei gleichzeitige
   Eingaben Dubletten — was doppelte Udder-Zeilen hier schon angerichtet haben, steht in
   [UdderService.cs:47-56](BBCowDataLibrary/Services/UdderService.cs:47).

**Fertig wenn:** `dotnet build` grün, alle 12 Testdateien grün, App im Browser unverändert.

---

## Phase 1 — Meadow.Api (altes Frontend läuft daneben weiter)

1. Projekt anlegen, [Program.cs](4Cows-FE/Program.cs) übernehmen: DB-Bootstrap (`:37`),
   `MigrationHelper`/`MigrateAsync`/`DataSeeder`/`DemoDataSeeder` (`:104-118`), Env-Vars (`:17-35`),
   beide HostedServices (`:94-101`), `LoggerService` (`:16,38`). Die doppelte
   `UseStaticFiles()`-Zeile (`:119` und `:126`) dabei entsorgen.
2. Controller, ~55 Endpunkte: **11 Collection-GETs** (`GetAllDataAsync()` je Service),
   **~41 Mutationen**, die tatsächlich aus Razor erreicht werden, **4 Infrastruktur**
   (`/api/config`, `/api/health`, `/api/xlink/refresh` + `/status`, `/api/export/claw-treatments`).
3. **Jede Mutation gibt die erzeugte Entität zurück** (201 + Body). Keine Stilfrage:
   `ClawTreatmentService.cs:56`, `PClawTreatmentService.cs:53` und `KPIService.cs:58` machen
   `_cache.Add(entity.Id, entity)`. Serverseitig schreibt EF die Identity in die Instanz zurück;
   über HTTP bliebe `Id == 0`, der zweite Insert liefe auf `.Add(0, …)` und
   `ImmutableDictionary.Add` **wirft** bei Schlüsselkollision mit abweichendem Wert.
4. `GET /api/config` liefert `DemoSettings` + `DB_SERVER`/`DB_DB` + `XLinkUrl` + Sync-Intervall.
   Ohne das zeigt [DatabaseInfoDialog.razor:39-46](4Cows-FE/Components/4CowsComponent/Dialogs/DatabaseInfoDialog.razor:39)
   still die Fallback-Werte: `IConfiguration` existiert in WASM und wirft nicht, sie ist nur leer.
5. `POST /api/kpi/{id}/value` nimmt eine **Id, niemals SQL im Request-Body**. Das Script wird
   serverseitig aus der DB geladen, `KpiScriptGuard` bleibt serverseitig und validiert beim
   Schreiben *und* beim Ausführen.
6. `POST /api/xlink/refresh` startet asynchron und antwortet 202, Fortschritt über
   `/api/xlink/status`. Synchron würde der Aufruf am `HttpClient`-Timeout scheitern — der Scraper
   geht bis `MaxPages = 1000` ([XLinkService.cs:22](BBCowDataLibrary/Services/XLinkService.cs:22)).
7. **`X-Data-Version`-Header auf jeder Antwort.** `DemoResetBackgroundService` setzt den Wert nach
   jedem nächtlichen Reset neu. Ohne das zeigt jedes über Nacht offene Tablet Daten von vor dem
   Reset mit IDs, die es nicht mehr gibt — genau das Problem, das `ReloadCachesAsync`
   ([DemoResetBackgroundService.cs:209-232](4Cows-FE/Components/Services/DemoResetBackgroundService.cs:209))
   heute prozessintern löst und nach dem Split nicht mehr lösen kann.

**Fertig wenn:** alle Endpunkte per `.http`/curl gegen die Dev-DB durchgespielt sind, inklusive
**zwei** Inserts hintereinander je Service (der Fall aus Punkt 3).

---

## Phase 2 — Meadow.Client (WASM, noch online-only)

1. Projekt anlegen; alle `.razor`, `Components/Meadow`, CSS und `wwwroot` verschieben.
2. 11 `Http*Service : IXService` implementieren. Signaturen identisch, Rumpf `HttpClient`.
   Fehlschlag → `false` bzw. `"--"`, damit die Razor-Dateien nichts merken.
3. `DelegatingHandler`, der `X-Data-Version` jeder Antwort prüft; bei Änderung alle Client-Caches
   leeren und `MeadowDataChanges` feuern. Dazu ein leichter Poll, solange der Tab sichtbar ist.
4. [MeadowDataLoader.cs](4Cows-FE/Components/Services/MeadowDataLoader.cs) von „lädt pro Navigation"
   auf „lädt einmal pro Session, invalidiert über das Version-Token" umstellen. Sonst werden aus
   `EnsureLookupsAsync` sechs HTTP-Aufrufe **pro Seitenwechsel** (`EnsureDashboardAsync`: zehn).
   Der dokumentierte Fallstrick bei kaltem Cache ([MeadowDataLoader.cs:8-13](4Cows-FE/Components/Services/MeadowDataLoader.cs:8) —
   `FilterFuncCow` filtert beim Direktaufruf alle Zeilen weg) trifft jetzt jeden Browser beim Start.
5. `KpiRowProvider` und `KpiEvaluator` nach Shared und **client-seitig** registrieren.
   Builder-KPIs werden lokal über die Client-Caches ausgewertet, SQL-KPIs gehen per Id an die API.
   Bliebe `KpiRowProvider` serverseitig, läse er die Caches der API — die niemand mehr wärmt —
   und jedes Builder-KPI lieferte stillschweigend 0 statt eines Fehlers.
   `KpiScriptGuard` bleibt serverseitig. (Nebeneffekt: Builder-KPIs funktionieren damit in Phase 4
   auch offline.)
6. Client-seitiges Pendant zu `DatabaseStatusService`, gespeist aus den HTTP-Ergebnissen
   (`ReportSuccess`/`ReportFailure` wie heute). `DatabaseConnectionState` und `MeadowOfflineGate`
   funktionieren damit unverändert weiter.
7. `index.html` aus [App.razor](4Cows-FE/Components/App.razor) ableiten: Theme-Boot-Script
   übernehmen, Splash-Screen ergänzen (WASM prerendert nicht). `HeadOutlet` wird zu
   `builder.RootComponents.Add<HeadOutlet>("head::after")` — sonst hört `<PageTitle>` in
   [MainLayout.razor:12](4Cows-FE/Components/Layout/MainLayout.razor:12) **wortlos** auf zu wirken.
8. **Kultur auf `de-DE` pinnen** (`CultureInfo.DefaultThreadCurrentCulture`) vor `RunAsync()`.
   `KpiEvaluator` formatiert mit `CurrentCulture`, während `KpiSqlBuilder` fest `de_DE` verdrahtet
   ([KpiSqlBuilderTests.cs:32-37](BBCowDataLibrary.Tests/Kpi/KpiSqlBuilderTests.cs:32) dokumentiert
   die Kopplung). Ohne Pinning zeigt ein englisches Handy `1,240` für das eine und `1.240` für das
   andere KPI. `InvariantGlobalization=true` ist damit **verboten**.
9. `IsLanding` ([App.razor:121](4Cows-FE/Components/App.razor:121)) entfällt — es hängt an
   `HttpContext`. Alle Skripte laden unbedingt. Chart.js wird von `cdn.jsdelivr.net`
   ([App.razor:94](4Cows-FE/Components/App.razor:94)) lokal eingebunden; für Offline ohnehin Pflicht.
10. Excel-Export in `Claw_Table.razor` auf `POST /api/export/claw-treatments` umbauen: der Client
    schickt die gefilterten IDs, der Server liefert die Datei. `ExportDataToExcel` (`:448-514`),
    `DownloadFile` (`:432-446`) und die `OfficeOpenXml`-Usings fallen raus — das entfernt zugleich
    die `System.Drawing`-Nutzung (`:473-474,505`), die auf `browser-wasm` nicht unterstützt ist.
11. `Error.cshtml`/`Error.cshtml.cs` (Razor Page mit `PageModel`) und
    `UseStatusCodePagesWithReExecute` entfallen; 404 löst der Client-Router über `MapFallbackToFile`.
    Den 21-zeiligen Kommentar in `NotFoundPage.razor:5-19` dabei entsorgen.
12. `Meadow.Api` hostet den Client; `4Cows-FE` löschen.

**Fertig wenn:** App läuft auf einem Port, Handy sperren + entsperren zeigt **keinen
Reconnect-Dialog** mehr, alle Tabellen/Dialoge/KPIs funktionieren.

---

## Phase 3 — PWA + Deployment

1. `manifest.json`, Icons (aus den vorhandenen Hoof-SVGs), `theme-color` aus den Meadow-Tokens,
   `apple-touch-icon`, `display: standalone`, Service Worker.
2. **`wwwroot` ist 59 MB, davon 33 MB `images/Mockups/` und 23 MB `images/Screenshots/`.**
   Der publizierte Service Worker cacht alles davon vorab — die Installation wäre ~59 MB.
   Beides aus `wwwroot` herausnehmen oder per `ServiceWorkerAssetsManifestItemExclude`
   ausschließen. `wwwroot/sql/*.sql` (u.a. `4Cows-DB-V3.sql`) wird heute öffentlich ausgeliefert
   und gehört bei der Gelegenheit ebenfalls raus.
3. **Service-Worker-Strategie:** App-Shell cache-first (Precache). API-Aufrufe network-first und
   **niemals vom Service Worker gecacht** — der Datencache liegt in IndexedDB und gehört der App.
   Zwei konkurrierende Caches wären die Hauptquelle für „warum sehe ich alte Daten".
4. Reverse Proxy (Caddy oder Traefik) mit Let's Encrypt per DNS-01 auf einen echten Domainnamen,
   der intern per Split-Horizon-DNS auf die lokale IP zeigt. Ohne gültiges Zertifikat auf den
   Endgeräten registriert Chrome keinen Service Worker — dann gibt es keine PWA und damit auch
   kein Offline. `UseHttpsRedirection` ([Program.cs:125](4Cows-FE/Program.cs:125)) hinter dem
   Proxy klären.
5. Publish-Größe messen. Erst `PublishTrimmed` + Brotli; reicht das nicht, ein
   `JsonSerializerContext` mit `[JsonSerializable]` für `KpiDefinition`, `KpiTileModel`,
   `KpiResult` und die Entities. Bis dahin `JsonSerializerIsReflectionEnabledByDefault=true` —
   [KpiDefinition.cs:162-181](BBCowDataLibrary/Models/KpiDefinition.cs:162) serialisiert reflektiv
   und liefert unter Trimming sonst leere Objekte statt eines Fehlers.
6. Dockerfile + Workflows: `wasm-tools`-Workload, Publish-Ziel `Meadow.Api`.

**Fertig wenn:** Lighthouse-PWA-Audit grün, Install-Prompt auf Android erscheint, Icon auf dem
Homescreen, App startet aus dem Homescreen heraus.

---

## Phase 4 — Offline-Sync

Ab hier arbeitet die App ohne Verbindung weiter. Erst in dieser Phase, weil Offline-Debugging
auf einer noch wackligen Architektur kaum zu diagnostizieren ist.

### Umfang: was offline geht — und was nicht

| | Offline |
|---|---|
| Lesen: Kühe, Behandlungen, Stammdaten, Kuh-Detail, Builder-KPIs | ✅ aus IndexedDB |
| Anlegen/Ändern: `CowTreatment`, `ClawTreatment`, `PlannedCowTreatment`, `PlannedClawTreatment` | ✅ in die Outbox |
| Kuh auf Abgang setzen (`UpdateIsGoneAsync`) | ✅ in die Outbox |
| Stammdaten (Medikament, WhereHow, Behandlungsgrund, Euter), KPI-Definitionen, Einstellungen | ❌ nur online |
| SQL-KPIs, Excel-Export, XLink-Sync | ❌ nur online |

Stammdaten bewusst nur online: sie werden am Schreibtisch gepflegt, nicht im Stall — und die vier
Find-or-Create-Methoden aus Phase 0.7 wären offline nicht sinnvoll koordinierbar.

> **Konkrete UI-Folge:** Die Medikamenten-Autocomplete in
> `Add_Cow_Treatment_Dialog.razor` legt heute über `MedicineService.GetMedicineIdByName`
> stillschweigend ein neues Medikament an, wenn man freien Text eingibt. Offline muss die
> Autocomplete auf bekannte Einträge beschränkt sein und Freitext ablehnen — sonst hängt an jeder
> Behandlung ein Medikament, das es serverseitig nicht gibt. Gleiches gilt für WhereHow und
> Behandlungsgrund.

### Migration: `ClientId` als zweiter Schlüssel

Neue EF-Migration, die den fünf offline-schreibbaren Tabellen eine Spalte `ClientId GUID NOT NULL`
mit Unique-Index gibt. Der Client erzeugt sie **vor** dem Speichern.

Das ist die tragende Entscheidung dieser Phase. Ohne sie:
- hat eine offline angelegte Behandlung keinen Schlüssel, bis der Server einen vergibt — und die
  Zeile kann bis dahin weder angezeigt, referenziert noch korrigiert werden;
- ist die Übertragung **nicht idempotent**. Bricht die Verbindung nach dem Schreiben, aber vor der
  Antwort ab, legt der Wiederholungsversuch dieselbe Medikamentengabe ein zweites Mal an. Bei einem
  Behandlungsjournal mit Wartezeiten ist das ein fachlicher Fehler, kein kosmetischer.

Serverseitig wird daraus ein Upsert auf `ClientId`: ist sie bekannt, gibt der Server die
bestehende Zeile zurück statt eine neue anzulegen.

### Lokaler Store

Ein dünner IndexedDB-Wrapper (~100 LOC JS + ein C#-Service), **keine Bibliothek und kein
EF-Core-auf-SQLite-in-WASM**: die Daten liegen ohnehin schon als vollständige Tabellen-Snapshots
in `ImmutableDictionary` vor, es wird nie in IndexedDB *abgefragt*, nur ganze Tabellen gelesen
und geschrieben. Ein Object Store je Tabelle plus einer für die Outbox.

Startsequenz des Clients:
1. Caches aus IndexedDB hydrieren → sofortiges Rendern, auch offline
2. Wenn online: frisch holen, Caches **und** IndexedDB aktualisieren
3. `X-Data-Version` weicht ab → vollständiger Neuabruf

Das beschleunigt nebenbei den Online-Start spürbar.

### Outbox

Append-only Queue in IndexedDB: `{ seq, clientId, entityType, operation, payload, createdUtc,
attempts, lastError }`. Die `Http*Service`-Implementierungen schreiben bei fehlender Verbindung
in die Outbox **und** in den lokalen Cache, damit die Zeile sofort in der Tabelle steht.

Übertragung strikt in `seq`-Reihenfolge, ausgelöst durch: App-Start, `online`-Event,
Sichtbarwerden des Tabs, manuellen Knopf, sowie Retry mit exponentiellem Backoff.
Nach erfolgreichem Leeren der Outbox: vollständiger Neuabruf und Cache-Ersetzung.

### Konflikte

Bewusst **keine automatische Auflösung**. Behandlungen sind ein Journal — zwei Geräte, die
Einträge anlegen, kollidieren nicht. Die echten Fälle sind Änderung oder Löschung einer Zeile,
die jemand anders bereits geändert oder gelöscht hat. Regel:

- Zielzeile existiert → Last-Write-Wins, der Server nimmt die Änderung an
- Zielzeile ist weg → der Outbox-Eintrag scheitert dauerhaft und landet in einer sichtbaren Liste
  „konnte nicht übertragen werden" mit dem Originalinhalt, die der Nutzer manuell auflöst

Automatisches Mergen wäre bei einem Medikamentenjournal die falsche Art von Cleverness.

### UI

`MeadowOfflineGate` wechselt die Bedeutung: heute blockiert es bei unerreichbarer Datenbank,
künftig ist es ein Statusband — „Offline · 3 Änderungen warten auf Übertragung". Dazu ein
Zustand je Zeile (übertragen / wartend / fehlgeschlagen) in den vier Behandlungstabellen und
die Fehlerliste aus dem Konflikt-Abschnitt.

**Fertig wenn:** Flugmodus → Behandlung anlegen → App schließen → App öffnen (Eintrag ist noch da)
→ Flugmodus aus → Eintrag steht in der Datenbank, genau einmal.

---

## Verifikation

**App starten** (siehe Memory `4cows-app-verification-setup`):
Benjamins eigene Instanz läuft auf **5107** — nie prozessweit `4Cows-FE.exe`/`Meadow.Api.exe`
beenden, immer nur die selbst gestartete PID. Für Tests Port 5108+ nehmen. Nicht `dotnet run`,
sondern in ein Scratch-Verzeichnis bauen (`dotnet build -o "$SCRATCH/app"`) und die exe von dort
starten, weil `bin/Debug` gesperrt sein kann.

**Dev-DB:** `docker-compose.dev.yml`, Container `4cows-dev-db`.
Abfragen per `docker exec 4cows-dev-db mariadb -uroot -padmin 4cows_v2 -e "…"`.

| Phase | Nachweis |
|---|---|
| 0 | `dotnet build` grün, alle 12 Testdateien grün, App im Browser identisch zu vorher |
| 1 | Alle ~55 Endpunkte per `.http`/curl. Explizit: **zwei** Inserts hintereinander je Service, `POST /api/kpi/{id}/value` mit gültigem und mit abgelehntem Script |
| 2 | Kuh-Detail, alle 6 Tabellen, alle Dialoge, Dashboard-KPIs (Builder **und** SQL), Excel-Export, XLink-Refresh. Neues Medikament anlegen → Spalte zeigt den Namen, nicht `--`. Zahlenformat bei auf Englisch gestelltem Browser prüfen |
| 2 | **Kerntest:** Handy, Seite öffnen, Bildschirm sperren, 2 Min warten, entsperren → kein Reconnect-Dialog |
| 3 | Lighthouse-PWA-Audit, Install-Prompt auf Android, Publish-Größe messen, Demo-Reset über Nacht → Tablet zieht nach |
| 4 | Flugmodus-Durchlauf (s. Phase 4). Dazu: Verbindung **während** der Übertragung kappen und wieder herstellen → die Behandlung existiert **genau einmal** (`SELECT ClientId, COUNT(*) … GROUP BY ClientId HAVING COUNT(*) > 1` muss leer sein). Offline eine Zeile ändern, die parallel online gelöscht wurde → Eintrag landet sichtbar in der Fehlerliste |

---

## Aufwand

Realistisch **7–12 Wochen nebenbei** bei ~29.600 LOC — davon 4–8 für die Phasen 0–3 und weitere
3–4 für den Offline-Sync.

Phase 0 ist der größte Einzelposten an mechanischer Arbeit und zugleich der risikoärmste: sie
ändert kein Verhalten. Phase 2 trägt das Architekturrisiko, weil dort die Cache-Semantik von
„ein geteilter Server-Cache" auf „ein Cache pro Browser" kippt. Phase 4 trägt das fachliche
Risiko — dort entscheidet die `ClientId`, ob eine abbrechende Übertragung im Stall eine
Medikamentengabe verdoppelt.

Jede Phase hinterlässt eine lauffähige App. Abbruch nach Phase 0 oder 1 hinterlässt eine
sauberere Codebasis, keine Bauruine.

---

# Nachtrag 14.09.2026 — verifizierte Korrekturen

Der Entwurf oben ist vor dem `ClawFinding`-Feature und ohne Messung am laufenden System
geschrieben. Die folgenden zwölf Punkte sind gegen die Codebasis, die Dev-Datenbank und das
installierte .NET-SDK geprüft. Wo Entwurf und Nachtrag sich widersprechen, gilt der Nachtrag.

| # | Entwurf sagt | Verifiziert | Konsequenz |
|---|---|---|---|
| 1 | „11 Interfaces" | **14** Service-Typen werden in Razor injiziert. Es fehlen `ClawFindingService` (11 Injektionsstellen, kam mit Migration `20260914091623_AddClawFinding`), `XLinkService` (2), `KpiRowProvider` (1) | 13 Interfaces, dazu `KpiRowProvider` und `DatabaseStatusService` als konkrete Shared-Klassen |
| 2 | „83 der 84 Dateien nur `@inject`" | 87 `.razor`-Dateien. **4 Ausnahmen** bis Phase 2, ~9 weitere in Phase 4 | siehe Ausnahmenbilanz unten |
| 3 | „die fünf betroffenen Tabellen bekommen `ClientId`" | Nur **vier** Offline-Pfade sind Inserts. `UpdateIsGoneAsync` ist ein UPDATE — eine `ClientId` auf `Cow` identifiziert die *Zeile*, nicht die *Operation*, und dedupliziert deshalb nichts. `IsGone = true` zweimal zu setzen ist ohnehin idempotent | **`ClientId` auf 4 Tabellen.** Kuh-Abgang ist per Entscheidung ganz aus dem Offline-Umfang |
| 4 | „Lighthouse-PWA-Audit grün" | Die PWA-Kategorie wurde in **Lighthouse 12 gelöscht** (Chrome 126, Mai 2024). Es gibt keinen PWA-Score mehr | Kriterium wird: DevTools → Application → Manifest ohne Installability-Fehler, und die Install-Affordanz erscheint |
| 5 | „per `ServiceWorkerAssetsManifestItemExclude` ausschließen" | Diese Property existiert im .NET-8-SDK **nicht** (0 Treffer in `sdk/8.0.421/Sdks/`) | Zwei echte Hebel: `offlineAssetsExclude` im Service Worker, und Entfernen aus `@(StaticWebAsset)` |
| 6 | „33 MB Mockups, die der Service Worker vorab cacht" | Nur **12 von 97** Mockups sind referenziert (4,4 MB). Die übrigen 85 (28,6 MB) und alle 97 Screenshots (23 MB) sind tot | ~53 MB löschen, die 12 behalten und aus dem Precache nehmen |
| 7 | Excel-Export „wandert serverseitig" | Der Export ist **heute schon kaputt**: kein `ExcelPackage.LicenseContext` irgendwo im Repo (`git grep` → 0), EPPlus 7.5.1 wirft ohne ihn. Zusätzlich ist `System.Drawing.ColorTranslator` (`Claw_Table.razor:485`) seit .NET 7 Windows-only und würde im Linux-Container ohnehin werfen | Verschieben **und reparieren**: Lizenzkontext setzen, `Color.FromArgb(0x2C,0x7D,0xA0)` statt `ColorTranslator.FromHtml` |
| 8 | Projekte und Namespaces heißen Meadow | Razor emittiert `@using` **innerhalb** des Namespace. `_4Cows_FE.Components.Meadow` → `Meadow.Api.Components.Meadow` lässt `using Meadow.Shared.Models` gegen dieses Segment auflösen: **CS0234 in ~87 generierten Dateien gleichzeitig** | Invariante: kein Namespace-Segment heißt exakt `Meadow` unterhalb eines `Meadow.*`-Roots. `Components/Meadow` → `Components/Ui` |
| 9 | `wwwroot` ist 59 MB | 57,45 MB. Dazu kommt `_content/Blazor.AceEditorJs` mit **18 MB in 473 Dateien** (191 Sprachmodi, 48 Themes, `worker-xquery.js` allein 3,4 MB). Benutzt werden **drei** | Ohne ein Trim-Target für dieses Paket ist der einstellige MB-Bereich unerreichbar |
| 10 | `X-Data-Version` setzt der Demo-Reset | Auch jede Mutation und der XLink-Sync im Nicht-Demo-Betrieb ändern Daten | `IEndpointFilter` bumpt bei jedem 2xx-Non-GET; 12 Scope-Zähler plus eine Boot-Id |
| 11 | (nicht erwähnt) | `UseStatusCodePagesWithReExecute("/nicht-gefunden")` (`Program.cs:133`) verwandelt einen API-404 in **200 plus deutsche HTML-Seite**. Ein Client, der `IsSuccessStatusCode` prüft, parst dann HTML als JSON | `app.UseWhen(!path.StartsWithSegments("/api"), …)` und `app.Map("/api/{**rest}", () => Results.NotFound())` vor dem Fallback |
| 12 | (nicht erwähnt) | `DemoDataSeeder` schreibt Behandlungen per `AddRangeAsync` (`:258-267`). Nach der `ClientId`-Migration bekäme jede Zeile `Guid.Empty`, und die **zweite** Zeile jedes Batches verletzt den Unique-Index. Die Exception wird in `DemoResetBackgroundService.ResetAsync:169` nur geloggt — die öffentliche Demo stünde nachts leer da | Seeder müssen `Guid.NewGuid()` vergeben, im selben PR wie die Migration |

## Ausnahmenbilanz — „nur `@inject`"

| Datei | Zusätzliche Änderung | Phase |
|---|---|---|
| `Add_Claw_Treatment_Dialog.razor:161` | `ClawFindingService.FailedId` → `ClawFinding.FailedId`. Kompiliert heute nur über die C#-„Color Color"-Regel; nach `@inject IClawFindingService` bindet der einfache Name an das Feld → CS1061 | 0 |
| `_Imports.razor` | der zentrale `@using`-Block | 0 |
| `DatabaseInfoDialog.razor:43,46` | `Environment.GetEnvironmentVariable` ist ein BCL-Static — nicht injizierbar, im Browser für immer `null`. 2 Zeilen | 2 |
| `Claw_Table.razor` | Excel-Export (im Auftrag bereits als Ausnahme genannt) | 2 |
| 9 weitere (Tabellen, Dialoge, `MainLayout`, `Index`) | Offline-Guards, Sync-Band, Zeilenzustand | 4 |

**Bis Phase 2: 4 Ausnahmen. Nach Phase 4: 13 von 87.** Keine davon lässt sich in einen Service
schieben.

## Vier Entscheidungen

| Frage | Entscheidung |
|---|---|
| Kuh auf Abgang setzen offline | **Ganz aus dem Offline-Umfang.** Offline schreibbar sind die vier Behandlungstabellen. `UpdateIsGoneAsync` ist ohnehin nur aus `BaseDataCow.razor:187` erreichbar — einer Seite, die der Auftrag als online-only führt |
| `/api/kpi/validate` („SQL testen") | **Gated ausliefern:** abgeschaltet wenn `Demo:Enabled`, zusätzlich hinter `Kpi:AllowScriptValidation` (Default `false`). Der Endpunkt führt vom Client geschicktes SQL als `root` ohne Authentifizierung aus; `KpiScriptGuard` ist das Einzige dazwischen |
| Unreferenzierte Bilder | **Löschen** (~53 MB). Git behält sie in der Historie |
| `dotnet test` im Workflow | **Nein.** Genau ein Job: Build und Push |

## Basislinie vor dem Umbau

Aufgenommen am 14.09.2026 auf `531ea37`, damit spätere Aussagen belegbar sind und nicht behauptet:

| Messwert | Wert |
|---|---|
| `dotnet test BB_Cow.sln` | **217 bestanden, 0 Fehler** |
| `dotnet build` Warnungen | 20 |
| `git grep LicenseContext` | **0** — der Excel-Export kann heute nicht funktionieren |
| Dev-DB Zeilen | Cow 40 · Cow_Treatment 121 · Claw_Treatment 80 · Planned_Cow 15 · Planned_Claw 15 · Medicine 6 · WhereHow 12 · Treatment_Reason 6 · Claw_Finding 7 · Udder 6 · KPI 7 · AppSetting 2 |
