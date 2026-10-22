# Betrieb auf dem Hof

Drei Container: die Anwendung, MariaDB, und Caddy davor. Nur Caddy hat
veroeffentlichte Ports, und die haengen an genau einer LAN-Adresse.

```
docker compose up -d
```

## Warum ueberhaupt ein Zertifikat

Nicht wegen Vertraulichkeit im eigenen Netz, sondern weil **Chrome ohne
gueltiges Zertifikat keinen Service Worker registriert**. Ohne Service Worker
gibt es keine PWA, kein Homescreen-Icon und kein Offline. Ein selbstsigniertes
Zertifikat reicht dafuer nicht, auch nicht mit importierter Wurzel auf dem
Geraet.

Deshalb DNS-01: der Nachweis gegenueber Let's Encrypt laeuft ueber einen
TXT-Eintrag im DNS, nicht ueber eine Anfrage von aussen. **Es muss kein
einziger Port nach aussen offen sein.**

## Was nur Benjamin tun kann

1. **Eine Domain besitzen**, deren DNS bei einem Anbieter mit `caddy-dns`-Modul
   liegt (Cloudflare, deSEC, Hetzner, INWX, Netcup, …). Der Registrar ist egal,
   die Nameserver sind es nicht. Steht ein anderer Anbieter an, in
   `caddy/Dockerfile` das Modul tauschen und neu bauen.

2. **Einen eng gefassten API-Token anlegen** — bei Cloudflare
   `Zone → DNS → Edit`, beschraenkt auf diese eine Zone. In die `.env` neben
   die compose-Datei, `chmod 600`, und `.env` gehoert in `.gitignore`. Das ist
   eine Schreibberechtigung aufs DNS; sie darf weder ins Image noch ins Repo.

3. **Die DNS-Form entscheiden.**
   - *Einfach:* ein oeffentlicher `A`-Eintrag `meadow.example.de →
     192.168.50.10`. Eine private IP in oeffentlichem DNS ist erlaubt und
     ueblich; sie verraet die interne Adresse und sonst nichts.
   - *Strenger, empfohlen:* **gar kein** oeffentlicher `A`-Eintrag. Das
     Zertifikat kommt aus DNS-01 und braucht keinen. Den Namen nur auf dem
     LAN-Resolver anlegen (Split-Horizon).

4. **Den LAN-Resolver** (Fritz!Box, Pi-hole, Unbound) fuer diesen Namen auf
   `192.168.50.10` zeigen lassen.

5. **Keine Portfreigabe fuer 80 oder 443 einrichten.** Danach gegenpruefen, vom
   Mobilfunk mit ausgeschaltetem WLAN:
   ```
   curl -v --max-time 10 https://meadow.example.de
   ```
   Das muss in einen Timeout laufen. Ein `403` oder `502` hiesse, dass etwas
   erreichbar ist.

6. **Feste DHCP-Reservierung** fuer `192.168.50.10`. Die Adresse steht woertlich
   in `docker-compose.yml` und im Caddyfile-Kommentar; wandert sie, laeuft
   nichts mehr.

7. Falls das Hofnetz einen anderen Bereich benutzt: beide Vorkommen von
   `192.168.50.` anpassen (compose-Ports, `XLinkUrl`).

## Kuh-Abgleich: Horizon-Agent oder XLink

Vorgabe ist der Horizon-Agent (`Meadow.HorizonAgent/README.md`): er läuft auf dem
Horizon-PC und meldet sich über `/api/horizon/snapshot` und `/heartbeat`. Dafür ein
gemeinsames Geheimnis anlegen und in die `.env` neben `docker-compose.yml` eintragen:

```
openssl rand -hex 32
```

Das Ergebnis als `HORIZON_AGENT_KEY` in die `.env`, denselben Wert als `Meadow:ApiKey` in
die `appsettings.json` des Agenten auf dem Horizon-PC.

**Wichtig:** Solange der Agent noch nicht installiert ist (Ticket 11), `COW_SYNC_SOURCE=XLink`
in der `.env` setzen – sonst läuft weder der XLink-Takt noch nimmt `/api/horizon/snapshot`
etwas an, und es findet gar kein Abgleich statt. Nach der Installation des Agenten
`COW_SYNC_SOURCE` wieder entfernen oder auf `HorizonAgent` setzen.

## Gegenprobe nach dem Start

```
# Caddy lauscht NUR auf der LAN-Adresse, nie auf 0.0.0.0
ss -ltnp | grep -E ':(80|443)'

# Anwendung und Datenbank haben gar keine veroeffentlichten Ports
docker compose ps

# Zertifikat geholt
docker compose logs caddy | grep -i "certificate obtained"
```

## Was bewusst nicht konfiguriert ist

`UseHttpsRedirection` und `UseHsts` stehen weiterhin in Program.cs, wirken in
diesem Aufbau aber nicht: TLS endet in Caddy, der Container spricht intern nur
`http:8080` und bekommt nie eine als https markierte Anfrage zu sehen. HSTS
setzt deshalb das Caddyfile, nicht `UseHsts`.

`Kpi__AllowScriptValidation` steht auf der Vorgabe `false`. Der Endpunkt fuehrt
SQL aus dem Anfragerumpf aus, und die Anwendung hat keine Authentifizierung.
Wer ihn einschaltet, sollte Punkt 5 vorher wirklich geprueft haben.
