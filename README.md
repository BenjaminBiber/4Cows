
# 4Cows

4Cows dient zur Dokumentation und Verwaltung von Klauen und Tierbehandlungen, mit Primären Fokus auf Rinder.
## Funktionen

- Speichern und Verwalten von Klauen- und Tierbehandlungen
- Planen von Klauen- und Tierbehandlungen
- Dark- & Lightmode
- Exportieren der Klauenbehandlungen als Excel-Dokument


## Installation

Um 4Cows zu installieren wird Docker Compose empfohlen. Hierbei muss die Ip-Adresse des Servers eingefügt werden.

```docker-compse
    4Cows:
    container_name: 4Cows
    image: benjaminbiber/4cows:latest
    ports:
      - "5750:8080"
    environment:
      DB_SERVER: "<IP of the Machine>"
      DB_User: "root" 
      DB_Password: "4cows"
      DB_DB: "4cows"

  4Cows-DB:
    image: mariadb:latest
    container_name: 4Cows-Db
    environment:
      MYSQL_ROOT_PASSWORD: 4cows
      MYSQL_DATABASE: 4cows
    ports:
      - "3306:3306"
```


## Technologie

**Frontend:** Blazor Server App mit Mudblazor

**Backend:** MariaDB Datenbank


## Roadmap

- Caching mit Frozen-Dictionaries

- Code-Cleanup und Refactoring der Startpage

- Refactoring der Datenbankstruktur

