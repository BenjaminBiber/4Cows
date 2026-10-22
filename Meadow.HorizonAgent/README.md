# Meadow Horizon-Agent

Liest den Tierbestand aus der Lely-Horizon-Datenbank und meldet ihn an Meadow. Läuft als
Windows-Dienst auf dem Horizon-PC – Meadow selbst hat keinen Zugriff auf dieses Netz.

## Voraussetzungen

- Horizon-PC: `CELSIUS_M740`, SQL-Server-Instanz `.\T4C3`, Datenbank `Lely`.
- Ein Konto, das sich lokal als Dienst anmelden darf (`install.ps1` legt bei Bedarf das
  Konto `MeadowAgent` an und richtet das Recht ein).
- Netzzugang vom Horizon-PC zu Meadow (`Meadow:BaseUrl` in `appsettings.json`).

## Sicherheitsmodell

Jede Verbindung wechselt als Erstes mit `EXECUTE AS USER = 'BenchmarkReadOnly' WITH NO REVERT`
auf einen Nur-Lese-Benutzer und prüft das Ergebnis über `USER_NAME()` – jeder lokale
Windows-Benutzer ist auf diesem Server sonst `db_owner` (guest). Die Verbindung läuft mit
`Pooling=false`, weil sich eine Verbindung mit `NO REVERT` nicht in den Pool zurücksetzen
lässt. Siehe `Reading/HorizonReader.cs` für das Artefakt.

Ein SQL-Login für den Agenten wird von `install.ps1` **nicht** angelegt – das Konto
`BenchmarkReadOnly` und die Windows-Authentifizierung der Verbindung müssen auf dem
Horizon-PC bereits eingerichtet sein.

## Erstinstallation

1. Publish-Ordner (`bin\publish\win-x64\` nach `dotnet publish`, siehe unten) auf den
   Horizon-PC kopieren.
2. `appsettings.json` im Zielordner ausfüllen:
   - `Meadow:BaseUrl` – Adresse von Meadow, z.B. `https://meadow.example.de/`.
   - `Meadow:ApiKey` – derselbe Wert wie `HORIZON_AGENT_KEY` in der `.env` neben
     `deploy/docker-compose.yml` auf dem Server.
   - `Horizon:ConnectionString` und `Horizon:AnimalQuery` nur ändern, wenn sich das
     Horizon-Schema unterscheidet (siehe „Schemaänderung“ unten).
3. Probelauf: `Meadow.HorizonAgent.exe --dry-run` – liest einmal aus Horizon, zeigt die
   ersten 20 Tiere und beendet sich, ohne etwas an Meadow zu schicken.
4. `install.ps1` als Administrator ausführen.
5. `Start-Service MeadowHorizonAgent`.

## `--dry-run`

```
Meadow.HorizonAgent.exe --dry-run
```

Liest einmal aus Horizon, gibt Anzahl und eine Tabelle der ersten 20 Tiere auf der Konsole
aus und beendet sich mit Exit-Code 0 (1 bei Fehler). Schickt nichts an Meadow und startet
keinen Dienst. Gedacht für den Probelauf nach der Installation und nach jeder Änderung an
`Horizon:AnimalQuery`.

## Update

1. Dienst stoppen: `Stop-Service MeadowHorizonAgent`.
2. Alle Dateien im Installationsordner **außer** `appsettings.json` durch die neue
   Publish-Ausgabe ersetzen.
3. `Meadow.HorizonAgent.exe --dry-run` zur Kontrolle.
4. Dienst starten: `Start-Service MeadowHorizonAgent`.

`install.ps1` erneut auszuführen ist nicht nötig, solange sich Konto, Dienstname oder
Installationsordner nicht ändern – jeder Schritt darin ist aber idempotent und schadet nicht.

## Schemaänderung nach einem Horizon-Update

Ändert ein Lely-Update Tabellen oder Spalten, die `Horizon:AnimalQuery` liest, die Abfrage in
`appsettings.json` anpassen und mit `--dry-run` prüfen, bevor der Dienst wieder läuft.

## Fehlersuche

- **Ereignisanzeige** → Windows-Protokolle → Anwendung → Quelle „Meadow Horizon-Agent“.
  Der Dienst schreibt dorthin, u.a. Verbindungsfehler zu Horizon oder zu Meadow.
- **Meadow-Seite:** der Datenbank-Dialog in Meadow zeigt den letzten Herzschlag und die
  letzte erfolgreiche Übernahme des Agenten.
- Schlägt `--dry-run` mit einer Meldung zur Nur-Lese-Absicherung fehl, ist entweder das
  Konto `BenchmarkReadOnly` auf dem SQL-Server nicht eingerichtet oder die Verbindung läuft
  nicht unter dem erwarteten Windows-Konto.

## Veröffentlichen (`dotnet publish`)

```
dotnet publish Meadow.HorizonAgent -p:PublishProfile=win-x64
```

Erzeugt eine einzelne `Meadow.HorizonAgent.exe` (self-contained, win-x64) unter
`bin\publish\win-x64\`, zusammen mit `appsettings.json`, `install.ps1`, `uninstall.ps1` und
dieser README.

## Deinstallation

```
.\uninstall.ps1
```

Stoppt den Dienst, entfernt ihn und die Ereignisquelle. Das Konto `MeadowAgent` bleibt dabei
bestehen; `.\uninstall.ps1 -RemoveUser` entfernt zusätzlich das Anmelderecht und das Konto.
