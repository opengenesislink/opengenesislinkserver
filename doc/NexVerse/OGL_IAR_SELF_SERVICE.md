# OGL IAR Self-Service – sichere Bürger-Archive (Generation 1)

OpenGenesisLINK erweitert die bestehende **administrative** World API
`/api/v1/iar/*` um einen getrennten, rein **benutzergebundenen** Zugang
für angemeldete lokale Avatar-Konten. Keine Weitergabe von Passwort,
fremdem Benutzerkennzeichen oder rohem Archivinhalt über NexBus.

**Wichtig:** Dies ist eine opt-in **Shared-Storage-Implementierung**.
Im Standardzustand ist sie deaktiviert. Für getrennte Server MUSS
ein gemeinsames verlässliches POSIX-Verzeichnis (z. B. NFS) auf ROBUST
und auf jedem teilnehmenden Regionssimulator gemountet sein.
Es wird **kein** netzwerkübergreifender IAR-Byte-Transport über NexBus
implementiert. Unterschiedliche lokale Verzeichnisse sind nicht ausreichend.

## Zentrales IAR-Backup auf UGREEN NASync DXP2800 (10.78.0.4)

Das Netzlaufwerk ist unter `10.78.0.4` im WireGuard-Netz
`10.78.0.0/24` erreichbar. UGOS Pro unterstützt NFS. Bei diesem
Storage-Modus werden Import- und Export-IARs **direkt auf dem NAS**
angelegt: kein dauerhafter IAR-Export auf der Systemplatte des
Regionssimulators und keine lokale Ausweichablage bei NAS-Ausfall.

### NAS einrichten (UGOS Pro)

1. Auf dem DXP2800 eine eigene Freigabe für OGL-IAR erstellen.
2. **Systemsteuerung > Dateidienste > NFS** aktivieren.
3. Die Freigabe in **Dateien > Freigegebener Ordner > Eigenschaften >
   NFS-Berechtigungen** ausschließlich für die tatsächlichen
   VPN-Client-IP-Adressen von ROBUST und der Regionsserver öffnen,
   Lese-/Schreibrecht setzen. Zugriffsrechte/UID-GID-Mapping abstimmen.
4. TCP-Port **2049** auf dem WireGuard-Pfad erlauben und aus dem
   öffentlichen Internet **nicht** freigeben.
5. In UGOS den **genauen Exportpfad** der Freigabe nachschlagen.
   Der Pfad hängt vom NAS-Speicherpool ab; hier wird **kein**
   unbekannter NAS-Exportpfad erfunden.

### Linux-Hosts mounten

Auf **jedem** beteiligten Server, also ROBUST und Regionssimulator:

```bash
sudo apt install nfs-common
sudo mkdir -p /mnt/ogl-iar
# EXPORTPFAD durch den exakten NFS-Pfad aus der UGREEN-Oberflaeche ersetzen!
sudo mount -t nfs4 -o vers=4.1,hard,nosuid,nodev,noexec \
  10.78.0.4:/EXPORTPFAD /mnt/ogl-iar
findmnt -T /mnt/ogl-iar -o TARGET,SOURCE,FSTYPE
```

Die `findmnt`-Ausgabe muss den **exakten Mountpunkt**
`/mnt/ogl-iar`, den NFS-Typ und die Quelle
`10.78.0.4:/...` zeigen. Ein leeres lokales Verzeichnis genügt
**nicht**. In der Systemd-Service-Unit für ROBUST bzw. OpenSim
`RequiresMountsFor=/mnt/ogl-iar` ergänzen und einen persistenten
NFS-Mount (fstab/mount-Unit) konfigurieren. Unter dem Mountpunkt
auf dem Host **keine** ungeschützten lokalen Backup-Dateien anlegen.
Für starke Fail-Closed-Semantik sollte der lokale Mountpoint ohne
NFS für den Dienstbenutzer nicht beschreibbar sein.

Simulator-Konfiguration in der jeweils tatsächlich geladenen INI:

```ini
[OpenGenesisLINKIAR]
Enabled = true
StorageRoot = "/mnt/ogl-iar"
MaximumArchiveMiB = 2048
```

World-API-/ROBUST-Konfiguration:

```ini
[OglIarSelfService]
Enabled = true
SharedStorageRoot = "/mnt/ogl-iar"
NasHost = "10.78.0.4"
RetentionDays = 30
MaximumArchiveMiB = 2048
```

Der aktualisierte Self-Service akzeptiert ausschließlich NFS/NFSv4
mit dem **exakten Mountpoint** und dieser NAS-Quelladresse.
Fehlt die Freigabe, liefert die API HTTP 503; der Simulator verweigert
den Bürger-Export/-Import ebenfalls. Es wird kein lokaler
Fallback erzeugt. Die administrative IAR-Verwaltung ist davon
unabhängig; für das beabsichtigte NAS-only-Betriebsmodell dürfen
auch dort ausschließlich NAS-Pfade konfiguriert werden.

### Maximale Aufbewahrung: 30 Tage

`RetentionDays` kann zwischen **1 und 30** konfiguriert werden.
Werte über 30 deaktivieren den Self-Service statt die Grenze
stillschweigend zu verlängern.

- Der World-API-Prozess kontrolliert den Speicher **beim Start**
  und danach **stündlich** und löscht abgeschlossene, abgelaufene
  `<region-uuid>/<operation-uuid>.iar`-Archive.
- Der Ablauf beginnt mit Abschluss des Import-/Exportjobs;
  wenn kein Jobdatensatz mehr vorhanden ist, dient der
  Datei-Änderungszeitpunkt als Fallback.
- Ab dem Ablaufzeitpunkt wird der **Download sofort verweigert**,
  auch wenn die nächste physische Bereinigung noch nicht lief.
- Noch aktive IAR-Jobs werden aus Sicherheitsgründen nicht
  mitten im Schreibvorgang gelöscht. Die vorher vorhandene
  24-Stunden-Bereinigung abgebrochener Uploads bleibt bestehen.
- Die automatische Bereinigung läuft **nur während ROBUST/World API
  aktiv und das NAS verbunden ist**. Wenn die 30-Tage-Grenze
  auch bei komplett abgeschaltetem ROBUST physisch garantiert
  werden soll, muss zusätzlich eine eigenständige Aufbewahrungsregel
  auf dem NAS eingerichtet werden. Diese wird hier bewusst
  **nicht automatisch auf dem DXP2800 konfiguriert**.
- Gelöscht werden nur vom Self-Service generierte IAR-Dateinamen
  in UUID-Regionverzeichnissen, keine unbekannten Dateien,
  Unterordner oder NAS-Systemdateien.
- RAID oder NAS-Snapshots ersetzen keine weitere unabhängige
  Sicherung. Kopien außerhalb des IAR-Ordners unterliegen
  nicht automatisch dieser Regel.

**Vor Aktivierung:** Bereits vorhandene `.iar`-Dateien im alten
lokalen Speicher werden nicht automatisch migriert oder gelöscht.
Den Altbestand separat prüfen und sicher umziehen. Die NAS-Regionordner
müssen zu den vorhandenen IAR-Jobpfaden passen.

## Berechtigungen / Voraussetzungen

- Aufrufe nur mit OAuth-/Bearer-Token eines **angemeldeten lokalen
  Avatars**. Ein Serviceprincipal oder API-Key ist unzureichend.
- GET (eigene Jobs, Fortschritt, Download und MD5) und Export-Start:
  `inventory:read`. Import-Start, Upload und Import-Freigabe:
  `inventory:write` – benötigt eine explizite OAuth-Einwilligung/Grant,
  nicht bloß eine normale öffentliche Besucherberechtigung.
- Die Benutzer-ID stammt **ausschließlich aus dem geprüften Token**;
  ein `user_id` aus Request-Daten wird nicht übernommen.
- Ein gestarteter Job kann nur mit demselben lokalen Avatar abgefragt
  oder heruntergeladen werden. Job-UUIDs allein autorisieren nichts.
- Der vorhandene signierte IAR-Feature-Gate bleibt aktiv und kann
  Importe und Exporte je nach Node-Lizenz verweigern.
- Bürgerexporte wenden im originalen Archiver `checkPermissions=CT`
  an. Nur Gegenstände mit **Copy- UND Transfer-Rechten** können
  exportiert werden. Das ersetzt keine umfassende Prüfung aller
  Asset-Eigentumsrechte und Copybot-Risiken.

## Aktivierung

1. Ein gemeinsames Verzeichnis, etwa `/srv/ogl-iar`, auf ROBUST und
   allen beteiligten Simulatorservern mounten. Benutzerkonten, Rechte,
   ausreichenden freien Speicher und Netzwerktrennung prüfen.
2. Auf den **Regionssimulatoren** in der tatsächlich geladenen
   `OpenSim.ini` setzen:

   ```ini
   [OpenGenesisLINKIAR]
       Enabled = true
       StorageRoot = "/mnt/ogl-iar"
       MaximumArchiveMiB = 2048
   ```

3. Auf **ROBUST/World API** in der tatsächlich geladenen
   `Robust.HG.ini` (oder `Robust.ini`) setzen:

   ```ini
   [OglIarSelfService]
       Enabled = true
       SharedStorageRoot = "/mnt/ogl-iar"
       NasHost = "10.78.0.4"
       RetentionDays = 30
       MaximumArchiveMiB = 2048
   ```

4. ROBUST und Simulatoren mit aktualisierten DLLs neu starten.
   Der bestehende `NexVerseWorldApi`-Dienst, registrierte
   `NexVerseNodeAgent`-Verbindungen und `IInventoryArchiverModule`
   müssen verfügbar sein. Nur lokale Avatare, kein HG-Fremdkonto.

**Vorsicht:** Wenn bislang `OpenGenesisLINKIAR` ohne eigene
`StorageRoot`-Konfiguration lief, ändert diese neue Einstellung
den Pfad des administrativen IAR-Archivs. Vor dem Umstellen bestehende
Archive sicher migrieren oder mounten. Das Update setzt den Simulatorpfad
nicht automatisch um.

Für eine produktive verteilte Installation sind außerdem TLS, ein
restriktiver Reverse-Proxy, kein öffentlicher Zugriff auf das
StorageRoot, Speicherquoten/Datei-Retention und Monitoring erforderlich.
`SharedStorageRoot` muss ein absoluter Pfad auf **demselben**
gemeinsamen Dateisystem sein. Nur gleiche Verzeichnisnamen auf
verschiedenen lokalen Festplatten reichen nicht.

## World-API-Endpunkte

Alle folgenden Endpunkte sind beim aktivierten Modul unter
`https://world.stadt-nexverse.de` verfügbar, sofern der Reverse-Proxy
`/api/v1/iar/me` zur bestehenden ROBUST World API durchleitet.

| Methode | Pfad | Funktion |
|---|---|---|
| POST | `/api/v1/iar/me/export` | Eigenen Export beginnen |
| POST | `/api/v1/iar/me/import` | Upload-Job reservieren |
| PUT | `/api/v1/iar/me/operations/{id}/chunks?offset=N` | Nächsten IAR-Block hochladen |
| POST | `/api/v1/iar/me/operations/{id}/start` | SHA-256 prüfen und Import anstoßen |
| GET | `/api/v1/iar/me/operations/{id}` | Eigener Live-Fortschritt, ETA, Status |
| GET | `/api/v1/iar/me/operations/{id}/file?offset=N&length=M` | Export in Blöcken herunterladen (HTTP 206) |
| GET | `/api/v1/iar/me/operations/{id}/md5` | MD5 + SHA-256 Prüfsummenmanifest |
| DELETE | `/api/v1/iar/me/operations/{id}` | Eigenes Archiv löschen oder laufenden Upload abbrechen |

### Export

```http
POST /api/v1/iar/me/export
Authorization: Bearer <aktueller-eigener-oauth-token>
Content-Type: application/json

{"region_id":"REGION_UUID","inventory_path":"/*"}
```

HTTP 202: `operation_id`, `status_url`.
Der Job läuft asynchron über den vorhandenen IAR-Archiver und NexBus.
Alle Ordner-/Asset-Schritte werden über die vorhandene
`IInventoryArchiverProgress`-Schnittstelle geliefert.
Der Export wird mit servergeneriertem, nicht überschreibbarem
Dateinamen `{id}.iar` im Region-Verzeichnis abgelegt.

### Import (auch >100 MiB)

Zuerst lokale Dateigröße und **SHA-256** ermitteln. Die Gesamtgröße
darf standardmäßig bis zu 2 GiB betragen (konfigurierbar bis 10 GiB,
sofern die bestehende Regions-Policy dieselbe Größe zulässt).

```http
POST /api/v1/iar/me/import
Authorization: Bearer <aktueller-eigener-oauth-token>
Content-Type: application/json

{
  "region_id":"REGION_UUID",
  "inventory_path":"/",
  "archive_size_bytes":187654321,
  "sha256":"...64 hex digits...",
  "merge":true,
  "dry_run":false
}
```

Dann PUT mit `Content-Type: application/octet-stream`, **maximal
4 MiB pro Request**. Beim ersten Block `offset=0`, dann das
zurückgelieferte `next_offset` für jeden folgenden Block übernehmen.
Ein fehlgeschlagener Block darf nicht blind erneut bei Offset 0
geschrieben werden: zuerst den aktuellen Offset abfragen.
Eine vollständige Datei mit falscher SHA-256-Prüfsumme wird abgelehnt und vom Shared Storage entfernt. Fehlgeschlagene Uploads lassen sich durch den Besitzer mit DELETE entfernen. Abgebrochene Uploads mit mehr als 24 Stunden Inaktivität werden beim nächsten eigenen Jobstart bereinigt.

Das Archiv wird beim Empfang nicht in den RAM geladen. Beim
Aufruf von `/start` prüft der Server die vollständige Größe und
SHA-256, erst danach startet der Original-IAR-Archiver mit
vorheriger TAR/GZIP-Sicherheitsinspektion.

Während des Uploads liefert `GET /api/v1/iar/me/operations/{id}` z. B.:

```json
{
  "operation_id":"...",
  "state":"running",
  "action":"import",
  "phase":"uploading",
  "progress_percent":45,
  "eta_seconds":75,
  "uploaded_bytes":84344832,
  "expected_bytes":187654321,
  "next_offset":84344832,
  "download_ready":false
}
```

`eta_seconds` ist eine **Schätzung aus tatsächlichem Fortschritt**.
Vor ausreichenden Messwerten und bei abgeschlossenen Jobs ist der
Wert `null` – es wird keine künstliche verbleibende Zeit erfunden.
Nach dem Upload beginnt der Fortschritt des eigentlichen IAR-Imports
als separate Phase; diese Prozentwerte sind nicht additiv.

### Exportdatei und Prüfsumme

Sobald `download_ready=true` ist, die Datei in höchstens
4-MiB-Blöcken mit `offset` und `length` herunterladen.
Die Antwort lautet HTTP 206 mit `Content-Range`,
`application/octet-stream` und `Content-Disposition`.
Jeder Download erfordert weiterhin den berechtigten Bearer-Token.

Der `md5`-Endpunkt liefert ein kleines Textmanifest mit
`MD5  Dateiname` plus `SHA256 <hash>`.
**MD5 ist nicht kollisionssicher:** SHA-256 für verlässliche
Integritätsprüfungen bevorzugen. Auf Serverseite werden beide
Prüfsummen aus der fertigen Archivdatei in einem Stream ermittelt.

## Interaktives Stadtportal-Widget

Mitgeliefert: [Beispiel-Widget](examples/ogl-iar-self-service-widget.js).
Das Modul zeigt Import/Export-Aktionen, den aktuellen Jobstatus, einen
Fortschrittsbalken und die geschätzte Restzeit. Es pollt alle
**2 Sekunden** den eigenen geschützten Job-Endpunkt.

Das Stadtportal liefert den gültigen OAuth-Token über eine eigene
Callbackfunktion; **keine Zugangsdaten im Widget fest einkodieren**:

```javascript
import { mountOglIarSelfService } from "./ogl-iar-self-service-widget.js";

const widget = mountOglIarSelfService(
  document.getElementById("iar-dashboard"),
  {
    baseUrl: "", // gleicher Origin oder geschützter Backend-Proxy
    regionId: "REGION-UUID-HIER",
    getAccessToken: async () => portalOAuth.getAccessToken()
  }
);
```

Das Beispiel importiert IARs in 4-MiB-Blöcken und bietet nach
erfolgreichem Export einen Dateidownload sowie ein eigenes
MD5/SHA-256-Manifest. Für große Dateien benötigt der Browser eine
File-System-Access-API (`showSaveFilePicker`); bei fehlender Unterstützung
verweigert das Widget einen RAM-intensiven Komplettdownload. Der
Benutzer muss bei Uploads vorerst den zuvor lokal berechneten
SHA-256-Hash angeben. Ein fertiger eigenständiger
Portal-Login ist **nicht** enthalten: Das Widget benutzt die
authentifizierte OAuth-Sitzung des einbettenden Stadtportals.

## Web-Integration und Einschränkungen

Ein interaktives 2-Sekunden-Statuspolling kann die eigenen Jobdaten
aus dem bestehenden Stadtportal mit dem OAuth-Bearer-Token abrufen.
Es gibt keinen fremdlesbaren öffentlichen Status und keinen
unautorisierten Downloadlink.

**Grenzen dieser Entwicklungsstufe:**
- Keine fertige direkte Inworld-Firestorm-Dateiauswahl; IARs werden
  über das authentifizierte **Webportal** oder einen HTTP-Client
  hoch- und heruntergeladen.
- Kein echtes Cross-Host-Byte-Routing ohne Shared-Mount.
- **Kein automatischer Resume nach ROBUST-Neustart**, wenn ein
  Upload bereits als laufend markiert war. Die vorhandene persistente
  Job-Engine kennzeichnet unterbrochene Jobs als fehlgeschlagen;
  erneuter Jobstart erforderlich.
- Kein Live-Push/SSE – der Client pollt die fortlaufend
  gespeicherten Fortschrittswerte des Original-Archivmoduls.
- Ein Import kann nicht gefahrlos mitten im Inventar-Restore
  unterbrochen werden. Deshalb gibt es keinen Abbruchknopf
  für aktive Importe.
- Nach erfolgreichem Import ist eine Inventaraktualisierung oder
  erneute Anmeldung im Viewer erforderlich, abhängig von dessen
  Cache.
- Die im Archiv vorhandenen Rechte bleiben Gegenstand des
  ursprünglichen OpenSim-/Asset-/Inventory-Importpfades.
- Vollständige automatische Dateiretention für erfolgreiche Archive,
  ein generischer Browser-Download ohne
  Clientcode und vollständig gridgebundene Quotenverwaltung sind
  Folgeschritte. Ein Export wird in dieser Version nur blockweise
  heruntergeladen, um den Arbeitsspeicher des Webservers zu schützen.

Die bestehende administrative `/api/v1/iar`-Schnittstelle und
Regionenverwaltung bleiben separat, unverändert authentifiziert und
benötigen weiterhin `regions:manage`.
