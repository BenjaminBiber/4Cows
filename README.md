# 4Cows

4Cows dient zur Dokumentation und Verwaltung von Klauen und Tierbehandlungen, mit Primären Fokus auf Rinder.
## Funktionen

- Speichern und Verwalten von Klauen- und Tierbehandlungen
- Planen von Klauen- und Tierbehandlungen
- Dark- & Lightmode
- Exportieren der Klauenbehandlungen als Excel-Dokument
- Auswerten von Lely Horizon Daten über einen Xlink-Scraper
- Medikamente mit Dosiereinheit und Standard-Verabreichungsart pflegen; beides
  belegt die Behandlungs-Dialoge vor

## Installation

Um 4Cows zu installieren wird Docker Compose empfohlen. Hierbei muss noch die IP Adresse des XLink-Servers eingetragen werden

```docker-compse
version: '3.8'

networks:
  4cows-network: 
    driver: bridge

services:
  4Cows:
    container_name: 4Cows
    image: benjaminbiber/4cows:PreRelease19
    depends_on:
      - 4Cows-DB
    ports:
      - "5750:8080"
    environment:
      DB_SERVER: "4Cows-DB"  
      DB_User: "root" 
      DB_Password: "4cows"
      DB_DB: "4cows_v2"
      XLinkUrl: "http://<Xlink-Server-IP>/Xlink/"
      XLinkID: "10672"
      # Nur fuer eine oeffentliche Demo-Instanz. Ist Demo__Enabled gesetzt,
      # zeigt "/" eine Landing-Page statt des Dashboards, das Dashboard liegt
      # dann unter "/app". Ausserdem werden Beispieldaten angelegt, der
      # XLink-Sync abgeschaltet und die Daten jede Nacht zurueckgesetzt.
      # Achtung: nur "true"/"false" werden erkannt, nicht "1" oder "yes".
      # Demo__Enabled: "true"
      # Demo__ResetHour: "3"
    networks:
      - 4cows-network 

  4Cows-DB:
    image: mariadb:latest
    container_name: 4Cows-DB
    environment:
      MYSQL_ROOT_PASSWORD: 4cows
      MYSQL_DATABASE: 4cows
    ports:
      - "3306:3306"
    volumes:
      - ./4cows-db:/var/lib/mysql
    networks:
      - 4cows-network 

```

## Demo-Modus

Für eine öffentlich erreichbare Demo-Instanz gibt es einen Schalter in
`appsettings.json`:

```json
"Demo": {
  "Enabled": true,
  "ResetHour": 3
}
```

Per Umgebungsvariable (Docker) heißt derselbe Schlüssel `Demo__Enabled` –
mit **doppeltem** Unterstrich. Erkannt werden nur `true` und `false`,
nicht `1` oder `yes`; alles andere gilt als `false`.

Was sich damit ändert:

| | Demo aus (Standard) | Demo an |
|---|---|---|
| `/` | Dashboard, wie bisher | Landing-Page |
| `/app` | Dashboard (Alias) | Dashboard |
| Daten | nur KPI- und Standardwerte | zusätzlich Beispieldaten |
| XLink-Sync | aktiv | **aus** |
| Nachts um `ResetHour` | – | Beispieldaten werden neu erzeugt |

Der XLink-Sync ist im Demo-Modus abgeschaltet, weil er jede Kuh als
abgegangen markieren würde, die der Scraper nicht liefert – die
Beispieltiere wären danach in keiner Auswahl mehr sichtbar.

## Technologie

**Frontend:** Blazor Server App mit Mudblazor

**Backend:** MariaDB Datenbank

## Roadmap

## Ideenspeicher
- Anpassung der KPIs über eine Settings Seite
- Weitere Einstellungen wie Standard-Werte für Klauenbehandlungen
