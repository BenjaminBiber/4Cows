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
      AdminToken: "AdminToken"
      XLinkUrl: "http://<Xlink-Server-IP>/Xlink/"
      XLinkID: "10672"
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


## Technologie

**Frontend:** Blazor Server App mit Mudblazor

**Backend:** MariaDB Datenbank

## Roadmap

## Ideenspeicher
- Anpassung der KPIs über eine Settings Seite
- Weitere Einstellungen wie Standard-Werte für Klauenbehandlungen
