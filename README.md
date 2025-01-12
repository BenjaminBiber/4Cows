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

  selenium-hub:
    image: selenium/hub:4.7.2-20221219
    container_name: selenium-hub
    ports:
      - "4442:4442"
      - "4443:4443"
      - "4444:4444"
   
  selenium-chrome:
    image: selenium/node-chrome:4.7.2-20221219
    shm_size: 2gb
    depends_on:
      - selenium-hub
    environment:
      - SE_EVENT_BUS_HOST=selenium-hub
      - SE_EVENT_BUS_PUBLISH_PORT=4442
      - SE_EVENT_BUS_SUBSCRIBE_PORT=4443
      - SE_VNC_NO_PASSWORD= true

  xlinkcache:
    image: redis:6.2-alpine
    container_name: xlinkcache
    restart: always
    ports:
      - '6379:6379'
    command: redis-server --save 20 1 --loglevel warning
    volumes: 
      - /etc/docker_vol/cache:/data

  xlinkscraper:
    image: benjaminbiber/xlinkscraper:PreRelease19
    container_name: xlinkscraper_test
    restart: always
    ports:
      - '5751:8080'
    depends_on:
      - 4Cows-DB
    environment:
      DB_SERVER: "4cows-DB"  
      DB_User: "root" 
      DB_Password: "4cows"
      DB_DB: "4cows_v2"
      REDIS_URL: "xlinkcache"
      Selenium_URL: "selenium-hub"
      XLinkUrl: "http://<Xlink-Server-IP>/Xlink/"
    networks:
      - 4cows-network 


```


## Technologie

**Frontend:** Blazor Server App mit Mudblazor

**Backend:** MariaDB Datenbank


## Roadmap

- Charts 100% responsive machen

## Ideenspeicher

- Übsichtstabelle für Kühe, mit Dialog zur näheren 
Übersicht über Behandlungen
- Basisdatenbearbeitung für Medikamente und Kühe -> manuelle Ausführung der Scraper Api
- Anpassung der KPIs über eine Settings Seite
- Weitere Einstellungen wie Standard-Werte für Klauenbehandlungen