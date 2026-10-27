# Horizon-Agent

Datum: 2026-10-21
Status: Entwurf freigegeben, Umsetzung offen

## Problem

Der Kuh-Abgleich liest heute die XLink-ReportTable per HTTP und zerlegt das HTML mit Regex
(`Meadow.Data/Services/XLinkService.cs`). Er kennt nur Halsband- und Ohrmarkennummer, und
beide dienen gleichzeitig als Schlüssel. Ein Abgang wird nur daraus geschlossen, dass ein Tier
in der Tabelle fehlt.

Auf dem Lely-Horizon-PC ist die Horizon-SQL-Datenbank mit dem lokalen Admin per
Windows-Authentifizierung lesbar. Gesucht ist ein **Horizon-Agent**: ein Windows-Dienst auf
diesem PC, der die Tierdaten aus der Datenbank liest und per HTTPS an den Meadow-Server
schickt.

Erste Ausbaustufe: **derselbe Datenumfang wie XLink, erweitert um die Lely-interne
Animal-ID** als stabilen Schlüssel. Halsband und Ohrmarke werden nur noch aktualisiert, nicht
mehr zur Identifikation benutzt (Ausnahme: einmaliges Verknüpfen, siehe Abgleich). Weitere
Datenarten (Milch, Laktation, Besamung) sind eigene, spätere Specs.

## Entscheidungen

1. **Push statt Pull.** Der Agent läuft auf dem Horizon-PC und baut die Verbindung nach
   außen auf. Meadow läuft im Linux-Docker und könnte sich per Windows-Auth an einem
   Rechner ohne Domäne nicht anmelden; am Lely-DB-Server bleibt so bis auf einen
   Nur-Lese-Login alles unverändert, und auf dem Horizon-PC wird kein Port geöffnet.
2. **Vollständiger Snapshot statt Delta (Ansatz A).** Der Agent schickt bei jedem Lauf
   *alle* Tiere aus Horizon, auch abgegangene, jeweils mit Abgangsstatus. Ein verpasster
   Lauf heilt sich beim nächsten; es gibt keinen Stand-Zeiger und keinen Puffer. Bei ein paar
   hundert bis wenigen tausend Tieren sind das wenige KB.
3. **`IsGone` kommt aus Horizon**, nicht aus dem Fehlen eines Tiers. Horizon gilt in beide
   Richtungen: kehrt ein Tier zurück, wird `IsGone` wieder `false`.
4. **Die Lely-Animal-ID ist der Schlüssel.** `Cow` bekommt `Lely_Animal_Id`.
5. **Der Agent bleibt dumm, die Logik liegt auf dem Server.** Der Agent liest und sendet;
   Zuordnung, Anlage, Beförderung und Schutzregel liegen in Meadow.
6. **Das SQL ist Konfiguration.** Das Horizon-Schema ist nicht dokumentiert und kann sich
   mit Horizon-Updates ändern. Die Abfrage steht in der `appsettings.json` des Agents und
   muss feste Spaltennamen liefern. Ein Schemawechsel heißt: Konfiguration ändern, nicht neu
   bauen.
7. **Quelle umschaltbar, Standard Agent.** Die Umgebungsvariable `CowSyncSource`
   (`HorizonAgent` | `XLink`, Standard `HorizonAgent`) wählt die eine Quelle, die schreibt.
   XLink bleibt als Rückfall für Installationen ohne Agent. Nie schreiben beide.

## Bausteine

### `Meadow.HorizonAgent` (neues Projekt)

.NET-Worker-Service mit `UseWindowsService()`, veröffentlicht als self-contained Single-File
(`win-x64`), damit auf dem Horizon-PC keine .NET-Runtime nötig ist.

- **`HorizonReader`** – öffnet die Verbindung (`Integrated Security=true`,
  `ApplicationIntent=ReadOnly`), setzt `TRANSACTION ISOLATION LEVEL READ UNCOMMITTED`,
  führt das konfigurierte SQL mit 30 s Befehls-Timeout aus und liefert
  `List<HorizonAnimal>`. Fehlt eine Pflichtspalte, wirft er mit einer Meldung, die die
  fehlende Spalte nennt.
- **`MeadowClient`** – HTTP zum Server: `SendSnapshotAsync`, `SendHeartbeatAsync`. Setzt den
  Header `X-Agent-Key`.
- **`AgentWorker`** – die Schleife:
  - Lebenszeichen alle 60 s.
  - Snapshot alle `SnapshotIntervalMinutes` (Standard 15) **oder** sofort, wenn die Antwort
    auf das Lebenszeichen `syncRequested = true` meldet.
  - Ist der Server nicht erreichbar: erneut versuchen mit Backoff (1, 2, 4 … höchstens
    5 Minuten); nach Erfolg zurück auf den normalen Takt.
  - Der letzte Lese- oder Sendefehler wird beim nächsten Lebenszeichen mitgeschickt.
- **`--dry-run`** – liest einmal, gibt Tierzahl und die ersten 20 Zeilen auf der Konsole
  aus, sendet nichts und beendet sich. Für die Einrichtung des SQL.

Konfiguration (`appsettings.json`):

```json
{
  "Horizon": {
    "ConnectionString": "Server=.\\SQLEXPRESS;Database=<HorizonDb>;Integrated Security=true;TrustServerCertificate=true;ApplicationIntent=ReadOnly",
    "AnimalQuery": "SELECT ... AS AnimalId, ... AS CollarNumber, ... AS EarTag, ... AS IsGone FROM ..."
  },
  "Meadow": {
    "BaseUrl": "https://<meadow-host>/",
    "ApiKey": "<geheim>",
    "SnapshotIntervalMinutes": 15
  }
}
```

Pflichtspalten der Abfrage: `AnimalId` (nicht leer, eindeutig), `CollarNumber` (int, darf
NULL sein → 0), `EarTag` (darf NULL/leer sein), `IsGone` (bit). Die konkreten Tabellen und
Spalten ergibt die Schema-Erkundung (Umsetzungsschritt 1).

Logging: Windows-Ereignisprotokoll, Quelle `Meadow Horizon-Agent`.

### `Meadow.Shared` – Verträge

- `HorizonAnimal { string AnimalId; int CollarNumber; string? EarTag; bool IsGone }`
- `HorizonSnapshot { string AgentVersion; string MachineName; DateTimeOffset ReadAtUtc; List<HorizonAnimal> Animals }`
- `AgentHeartbeat { string AgentVersion; string MachineName; string? LastError }`
- `AgentHeartbeatResponse { bool SyncRequested }`

### Server

- **DB:** `Cow.LelyAnimalId` → Spalte `Lely_Animal_Id`, `nvarchar(64)`, nullable, eindeutiger
  gefilterter Index (`WHERE Lely_Animal_Id IS NOT NULL`). EF-Migration in
  `Meadow.Data/Migrations`.
- **`HorizonSyncService`** in `Meadow.Data/Services` (neben `XLinkService`, damit in
  `Meadow.Data.Tests` testbar): `ApplySnapshotAsync(HorizonSnapshot)` mit dem Abgleich unten.
  Hält wie `XLinkService` `LastSyncUtc`, `LastSyncSucceeded`, `LastSyncError`, dazu
  Agent-Version, Rechnername, Zeit des letzten Lebenszeichens, Tierzahl des letzten Snapshots
  und das Flag „Abgleich angefordert".
- **`HorizonEndpoints`** (`Meadow.Api/Endpoints`):
  - `POST /api/horizon/snapshot` → 200 mit Zusammenfassung (angelegt, aktualisiert,
    verknüpft, befördert) oder 422 mit Begründung (Schutzregel, doppelte `AnimalId`). Erhöht
    nach erfolgreichem Schreiben den Datenstand wie `XLinkRunner`.
  - `POST /api/horizon/heartbeat` → 200 mit `AgentHeartbeatResponse`; setzt das Flag
    „angefordert" beim Ausliefern zurück.
  - Beide nur mit gültigem `X-Agent-Key`; Vergleich in konstanter Zeit
    (`CryptographicOperations.FixedTimeEquals`) gegen `HorizonAgent__ApiKey`. Ist kein Key
    konfiguriert, antworten beide mit 404. Im Demo-Modus antworten beide mit 403 (gleiche
    Begründung wie bei `/xlink/refresh`).
- **Gemeinsame Abgleich-Schnittstelle:** `IXLinkService` wird zu `ICowSyncService` mit
  zusätzlicher Eigenschaft `Source` (`XLink` | `HorizonAgent`) und den Agent-Feldern
  (nullable, nur bei `HorizonAgent` belegt). `/api/xlink/refresh` und `/api/xlink/status`
  werden zu `/api/cowsync/refresh` und `/api/cowsync/status`. Client und Server werden
  gemeinsam ausgeliefert, alte Routen bleiben nicht stehen.
  - Bei `XLink` verhält sich alles wie heute.
  - Bei `HorizonAgent` setzt `refresh` nur das Flag „angefordert" (202); der Agent liefert
    beim nächsten Lebenszeichen, also binnen etwa einer Minute. `CowSyncBackgroundService`
    wird in diesem Modus nicht registriert.
- **Client:** `BaseDataCow.razor` beschriftet den Knopf nach Quelle („Von Horizon
  aktualisieren" / „Von XLink aktualisieren"). `DatabaseInfoDialog.razor` zeigt bei
  `HorizonAgent`: letzter Snapshot, Tierzahl, Agent-Version, Rechnername, letzter Fehler und
  „Agent offline", wenn das letzte Lebenszeichen älter als 5 Minuten ist.
- **Deploy:** `deploy/docker-compose.yml` bekommt `CowSyncSource` und `HorizonAgent__ApiKey`.

## Abgleich

Für jedes Tier im Snapshot, in dieser Reihenfolge:

1. **Meadow-Kuh mit dieser `Lely_Animal_Id` vorhanden** → Halsband, Ohrmarke und `IsGone`
   aus Horizon übernehmen (nur geänderte Felder schreiben).
2. **Sonst: Kuh ohne Lely-ID mit derselben Ohrmarke** (nur wenn `EarTag` nicht leer) →
   `Lely_Animal_Id` setzen, dann wie 1.
3. **Sonst, nur bei aktivem Tier: Kalb ohne Ohrmarke und ohne Lely-ID mit derselben
   Halsbandnummer** (nur wenn `CollarNumber > 0`) → befördern wie heute
   (`PromoteCalfAsync`, `Cow_ID` bleibt), Ohrmarke und `Lely_Animal_Id` setzen.
4. **Sonst, nur bei aktivem Tier** → neu anlegen. `Cow_ID` = Ohrmarke wie heute; ohne
   Ohrmarke `Cow_ID` = neue GUID wie bei Kälbern. `Lely_Animal_Id` gesetzt.
5. **Sonst (abgegangenes Tier ohne Gegenstück)** → nichts tun. Sonst käme die ganze
   Horizon-Historie in den Bestand.

Meadow-Kühe, die im Snapshot nicht vorkommen, bleiben unangetastet (lokal angelegte Kälber,
manuell erfasste Tiere).

**Schutzregel:** Der Snapshot wird abgelehnt (422, nichts geschrieben), wenn

- er leer ist, oder
- er mindestens 10 aktive Meadow-Kühe *und* mehr als die Hälfte der aktiven Meadow-Kühe auf
  `IsGone = true` setzen würde, oder
- eine `AnimalId` doppelt vorkommt.

Damit kann ein kaputtes SQL nicht die Herde ausbuchen. Ein absichtlicher Massenabgang ist
nicht realistisch; falls doch, lässt er sich über `CowSyncSource=XLink` oder ein temporär
angepasstes SQL umgehen.

## Sicherheit und Betrieb

- **Dienstkonto:** lokaler Windows-User `MeadowAgent`. `install.ps1` legt für ihn einen
  SQL-Login an und gibt ihm ausschließlich `db_datareader` auf der Horizon-DB. Notbehelf,
  dokumentiert: Dienst unter dem lokalen Admin, mit dem Nachteil, dass eine
  Passwortänderung den Dienst stoppt.
- **API-Key:** steht in der `appsettings.json` des Agents; `install.ps1` setzt die ACL der
  Datei auf `MeadowAgent` + Administratoren.
- **Transport:** HTTPS auf den bestehenden Split-Horizon-Namen von Meadow.
- **`install.ps1`:** Dienst anlegen (`sc.exe create`, Start automatisch verzögert,
  Neustart bei Absturz nach 1 Minute), Ereignisquelle registrieren, ACL setzen, SQL-Login
  anlegen. **`uninstall.ps1`** macht das rückgängig (der SQL-Login wird entfernt).
- **Update:** Dienst stoppen, Dateien tauschen (`appsettings.json` bleibt), Dienst starten.
  Beschrieben in `Meadow.HorizonAgent/README.md`.
- Eine allgemeine Authentifizierung der übrigen API ist **nicht** Teil dieser Spec.

## Fehlerbehandlung

| Fall | Agent | Server / Anzeige |
|---|---|---|
| SQL-Fehler, fehlende Spalte, Timeout | Ereignisprotokoll, kein Snapshot, Fehler im nächsten Lebenszeichen | Fehler im Datenbank-Dialog |
| Server nicht erreichbar | Backoff bis 5 min, kein Puffer | nach 5 min „Agent offline" |
| Key falsch oder fehlt | 401/404 protokollieren, weiter im Takt | Anfrage abgewiesen, nichts geschrieben |
| Schutzregel / doppelte ID | 422-Begründung protokollieren | `LastSyncSucceeded = false`, Begründung im Dialog |
| Demo-Modus | 403 protokollieren | nichts geschrieben |

## Tests

- **`Meadow.Data.Tests`** – `HorizonSyncService`:
  - Regeln 1–5 einzeln, dazu: Verknüpfung über Ohrmarke setzt die ID genau einmal; zweiter
    Snapshot findet die Kuh über die ID, auch bei geänderter Ohrmarke und Halsbandnummer.
  - `IsGone` wird in beide Richtungen übernommen.
  - Abgegangenes Tier ohne Gegenstück wird nicht angelegt.
  - Meadow-Kuh ohne Gegenstück im Snapshot bleibt unverändert.
  - Schutzregel: leer, Massenabgang, doppelte ID → nichts geschrieben.
- **`Meadow.HorizonAgent.Tests`** (neu) – `AgentWorker` mit gefälschtem Reader und Client:
  Takt, `syncRequested` löst sofortigen Snapshot aus, Backoff und Rückkehr zum Takt,
  Lesefehler landet im nächsten Lebenszeichen. `HorizonReader`-Spaltenprüfung gegen eine
  `DataTable`.
- **Abnahme auf dem Horizon-PC:** Schema erkunden, SQL eintragen, `--dry-run` prüfen,
  danach echter Snapshot gegen einen Testserver, dann gegen den Hof-Server; im Dialog
  Tierzahl und Agent-Status kontrollieren.

## Umsetzungsschritte (für die Ticket-Aufteilung)

1. **Schema erkunden** auf dem Horizon-PC: Tabelle(n) und Spalten für Animal-ID,
   Halsband, Ohrmarke, Abgang finden; `AnimalQuery` festlegen und in der Agent-README
   dokumentieren.
2. **Server:** Migration `Lely_Animal_Id`, Verträge in `Meadow.Shared`,
   `HorizonSyncService` mit Tests, `HorizonEndpoints` mit Key-Prüfung.
3. **Umschaltung:** `ICowSyncService`, `/api/cowsync/*`, `CowSyncSource`, Client-Anzeige in
   `BaseDataCow` und `DatabaseInfoDialog`, docker-compose.
4. **Agent:** Projekt, Reader, Client, Worker, `--dry-run`, Tests.
5. **Installation und Abnahme:** `install.ps1`/`uninstall.ps1`, README, Abnahme auf dem
   Horizon-PC.

## Offen außerhalb der Spec

- Ob Lelys Lizenz- oder Supportbedingungen den direkten Lesezugriff erlauben und ob es eine
  offizielle Export-Schnittstelle gibt, ist ungeklärt und vor dem Produktivbetrieb zu
  prüfen.

## Nachtrag 2026-10-22: Schema, Zugriff, Ort/Gruppe/Beschreibung

Die Schema-Erkundung (Umsetzungsschritt 1) ist erledigt, dokumentiert im Artefakt
„Lely Horizon DB-Referenz“ (https://claude.ai/artifact/FTKEikjQF8AbSevLVucy6p, Stand 21.10.2026).
Dieser Nachtrag geht den Abschnitten oben vor, wo sie sich widersprechen.

### Quelle

- Instanz `.\T4C3` auf `CELSIUS_M740` (SQL Server 2019 Express), Datenbank `Lely`.
- Tiere: `dbo.HemAnimal`. `AniId` = Lely-Animal-ID, `AniUserNumber` = Halsband/Tiernummer
  (entspricht XLink-Spalte 1; bei der Abnahme gegenprüfen), `AniLifeNumber` = Ohrmarke,
  `AniActive = 0` = abgegangen. Zeilen mit `AniDeleted = 1` werden nicht gelesen.
- Neu übernommen: `AniDescription` (Beschreibung), `SytLocation.LocName` über `AniLocId`
  (Ort/Stallbereich), `HemGroup.GroName` über `AniGroId` (Gruppe).

Standard-`AnimalQuery`:

```sql
SELECT CAST(a.AniId AS nvarchar(64))                        AS AnimalId,
       CAST(ISNULL(a.AniUserNumber, 0) AS int)               AS CollarNumber,
       a.AniLifeNumber                                       AS EarTag,
       CAST(CASE WHEN a.AniActive = 1 THEN 0 ELSE 1 END AS bit) AS IsGone,
       l.LocName                                             AS Location,
       g.GroName                                             AS [Group],
       a.AniDescription                                      AS Description
FROM dbo.HemAnimal a
LEFT JOIN dbo.SytLocation l ON l.LocId = a.AniLocId
LEFT JOIN dbo.HemGroup    g ON g.GroId = a.AniGroId
WHERE a.AniDeleted = 0
```

Pflichtspalten bleiben `AnimalId`, `CollarNumber`, `EarTag`, `IsGone`. `Location`, `Group`,
`Description` sind optional: fehlt die Spalte im Ergebnis, ist der Wert `null`.

### Zugriff (ersetzt Dienstkonto/SQL-Login und READ UNCOMMITTED oben)

- Windows-Authentifizierung; jeder lokale Benutzer kommt über `VORDEFINIERT\Benutzer` auf den
  Server und landet als `guest` mit `db_owner`. Deshalb beginnt **jede** Verbindung mit
  `EXECUTE AS USER = 'BenchmarkReadOnly' WITH NO REVERT;` und prüft danach
  `SELECT USER_NAME()` = `BenchmarkReadOnly`, sonst Abbruch mit Fehler.
- Connection-String:
  `Server=.\T4C3;Database=Lely;Integrated Security=true;Encrypt=false;TrustServerCertificate=true;Pooling=false;Application Name=MeadowHorizonAgent;Connect Timeout=15`
  (`Pooling=false` ist Pflicht: eine Verbindung mit `NO REVERT` lässt sich nicht zurücksetzen).
- Isolation Read Committed (Standard). **Kein** `NOLOCK`, **kein** `READ UNCOMMITTED`, kein
  `ApplicationIntent`.
- Dienstkonto: lokales Konto `MeadowAgent` in der Gruppe `Benutzer`. `install.ps1` legt
  **keinen** SQL-Login an. Ob `LocalSystem`/virtuelle Dienstkonten Zugriff hätten, ist nicht
  getestet und wird nicht genutzt.

### Neue Kuh-Felder

`Cow` bekommt neben `Lely_Animal_Id` drei Felder, alle nullable, nur vom Horizon-Abgleich
geschrieben (XLink fasst sie nicht an):

| Property | Spalte | Typ |
|---|---|---|
| `Location` | `Location` | varchar(100) |
| `Group` | `Group_Name` | varchar(100) |
| `Description` | `Description` | varchar(500) |

Horizon ist führend: Regeln 1–4 des Abgleichs übernehmen alle drei Werte, auch `null`.
`HorizonAnimal` bekommt `string? Location`, `string? Group`, `string? Description`.
Die Basisdaten-Tabelle der Kühe zeigt Ort, Gruppe und Beschreibung; die Suche findet auch
Ort und Gruppe.

### Aufbau der Abgleichslogik

Die Regeln 1–5 und die Schutzregel stehen als reine Funktion `HorizonSyncPlanner.Plan` in
`Meadow.Shared` (liefert eine Aktionsliste oder eine Ablehnung) und werden dort getestet.
`HorizonSyncService` führt die Aktionen nur aus. Er schreibt über die schmale Naht
`IHorizonCowStore` (in `Meadow.Data`, implementiert von `CowService`), damit `ICowService`
und der Browser-Dienst `HttpCowService` unverändert bleiben.

### Abgleich aus der Oberfläche bei `HorizonAgent`

`/api/cowsync/status` meldet `running = true`, solange ein angeforderter Abgleich noch nicht
durch einen Snapshot beantwortet ist, höchstens drei Minuten lang. Danach `running = false`
und `lastSyncError = "Der Horizon-Agent hat nicht geantwortet."`. Der Browser wartet damit
wie bei XLink auf das Ergebnis.
