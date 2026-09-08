# 4Cows

4Cows dient zur Dokumentation und Verwaltung von Klauen und Tierbehandlungen, mit Primären Fokus auf Rinder.
## Funktionen

- Speichern und Verwalten von Klauen- und Tierbehandlungen
- Planen von Klauen- und Tierbehandlungen
- Dark- & Lightmode
- Exportieren der Klauenbehandlungen als Excel-Dokument
- Auswerten von Lely Horizon Daten über einen Xlink-Scraper


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

### Hero-Foto der Landing-Page

Der Hero der Landing-Page erwartet ein Foto unter
`4Cows-FE/wwwroot/images/hero.jpg` – Stall, Herde oder Hofansicht, quer,
mindestens etwa 1600px breit. Als JPEG, nicht als PNG: ein Foto in PNG
wiegt schnell das Siebenfache.

Das Bild liegt hinter `brightness(.35)` und einem Farbschleier, es muss
also nicht kontrastarm sein - hell darf es aber auch nicht beliebig sein.
Die beiden Werte in `meadow-landing.css` sind auf das aktuelle Foto
gemessen (weisser Text 9,07:1, der 12px-Eyebrow in Sage 4,85:1; mobil
gilt `brightness(.28)`). Wer das Foto tauscht, sollte nachmessen.

Fehlt die Datei, trägt ein Verlauf die Fläche allein: die Seite bleibt
vollständig lesbar, im Browser-Log steht dann aber ein 404 auf
`images/hero.jpg`.

Lokal zum Ausprobieren:

```
dotnet run --project 4Cows-FE --launch-profile http-demo
```

## Technologie

**Frontend:** Blazor Server App mit Mudblazor

**Backend:** MariaDB Datenbank

## Roadmap

## Ideenspeicher
- Anpassung der KPIs über eine Settings Seite
- Weitere Einstellungen wie Standard-Werte für Klauenbehandlungen
