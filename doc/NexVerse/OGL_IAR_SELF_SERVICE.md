# OGL IAR Self-Service – sichere Bürger-Archive (Generation 1)

OpenGenesisLINK erweitert die bestehende **administrative** World API
`/api/v1/iar/*` um einen getrennten, rein **benutzergebundenen** Zugang
für angemeldete lokale Avatar-Konten. Keine Weitergabe von Passwort,
fremdem Benutzerkennzeichen oder rohem Archivinhalt über NexBus.

**Status:** Benutzergebundener Self-Service bleibt standardmäßig deaktiviert
(`Enabled=false`). **WebDAV ist der bevorzugte und voreingestellte
StorageBackend-Typ**, NFS kann explizit als Legacy-Backend gewählt werden.
Die Inventararchivierung selbst verwendet weiterhin native
OpenSim-Datei-/Stream-Schnittstellen; bei WebDAV werden daher ausschließlich
**temporäre Arbeitsdateien** auf ROBUST und dem Zielsimulator benötigt.
Das dauerhafte Archiv liegt auf dem NAS. Ohne funktionierenden WebDAV-Zugang
startet kein neuer Self-Service-Job.

## UGREEN NASync DXP2800 / WireGuard / WebDAV (Standard)

Die NAS-IP `10.78.0.4` ist aus OpenGenesisLINK über den bereits eingerichteten
WireGuard-Split-Tunnel erreichbar. Damit bleibt die öffentliche Serverroute
unberührt. Der 1blu-OpenVZ-Host benötigt für WebDAV kein NFS-Kernelmodul.

1. UGOS Pro: **Systemsteuerung > Dateidienste > WebDAV** öffnen;
   HTTPS aktivieren (häufig Port 5006; tatsächlich eingestellten Port prüfen).
2. Eine **eigene Freigabe/Unterverzeichnis** für IAR-Archive erstellen.
   Dafür einen eigenen NAS-Benutzer nur mit Les-/Schreibrechten für diese
   Freigabe anlegen; nicht das NAS-Admin-Konto verwenden.
3. NAS-WebDAV-HTTPS-Zertifikat korrekt konfigurieren:
   `WebDavBaseUrl` muss einen **gültigen, vertrauenswürdigen
   Zertifikatsnamen** verwenden, der NAS-IP zugeordnet ist.
   Beim Zugriff per `https://10.78.0.4:5006/...` muss ein
   *IP-SubjectAlternativeName* im Zertifikat stehen. Bei einem
   DNS-Hostnamen muss dessen Zertifikat zum Namen passen. Weder
   `--insecure` noch ein TLS-Validierungs-Bypass sind zulässig.
   Der WebDAV-Client verbindet sich unabhängig vom DNS immer
   mit `10.78.0.4` (kein Proxy, kein Redirect).
4. Den tatsächlichen **WebDAV-Ordnerpfad** aus UGOS Pro verwenden.
   Er wird nicht aus dem bisherigen NFS-Pfad abgeleitet.
5. Den Servern über WireGuard nur Zugriff auf den WebDAV-Port des NAS
   gestatten; **keine öffentliche Freigabe**. Vom ROBUST- und SIM-Host
   `nc -zvw3 10.78.0.4 5006` bzw. den konfigurierten Port testen.
6. Auf ROBUST und jedem Regionssimulator eine **eigene** Passwortdatei
   für denselben dedizierten NAS-Account ablegen (nur Eigentümer lesbar):

   ```bash
   sudo install -d -m 700 /etc/opengenesislink
   read -rsp "NAS-WebDAV-Passwort: " OGL_IAR_PASS; echo
   printf '%s' "$OGL_IAR_PASS" | sudo tee /etc/opengenesislink/iar-webdav.pass >/dev/null
   unset OGL_IAR_PASS
   sudo chmod 600 /etc/opengenesislink/iar-webdav.pass
   ```

   Das Kennwort **nicht** in Git oder in World-API-/NexBus-Payloads hinterlegen.
   Bei Datenzugriff über systemd muss der Dienstbenutzer die Datei lesen dürfen.

### ROBUST: tatsächliche `Robust.HG.ini`

```ini
[OglIarSelfService]
Enabled = true
StorageBackend = "WebDAV"
NasHost = "10.78.0.4"
WebDavBaseUrl = "https://NAS-ZERTIFIKATSNAME:5006/WEB_DAV_IAR_ORDNER"
WebDavUsername = "ogl-iar"
WebDavPasswordFile = "/etc/opengenesislink/iar-webdav.pass"
TemporaryStorageRoot = "/var/lib/opengenesislink/iar-staging"
RetentionDays = 30
MaximumArchiveMiB = 2048
```

Die URL ist **ein Muster**, kein garantierter UGOS-Pro-WebDAV-Pfad.
`TemporaryStorageRoot` ist kein Backup-Ort. Upload-Blöcke
werden dort zunächst geschrieben, SHA-256-verifiziert und anschließend
per HTTPS ins NAS übertragen; erst dann wird der native Import
im Simulator ausgelöst.

### Simulator: tatsächlich verwendete `OpenSim.ini`

```ini
[OpenGenesisLINKIAR]
Enabled = true
StorageBackend = "WebDAV"
NasHost = "10.78.0.4"
WebDavBaseUrl = "https://NAS-ZERTIFIKATSNAME:5006/WEB_DAV_IAR_ORDNER"
WebDavUsername = "ogl-iar"
WebDavPasswordFile = "/etc/opengenesislink/iar-webdav.pass"
StorageRoot = "OGLInventoryArchives"
MaximumArchiveMiB = 2048
```

Auf jedem SIM muss die Region-Archivverwaltung mit WebDAV-Client und
NexVerseNodeAgent aktiv sein. Der vorhandene `StorageRoot` des
administrativen IAR-Dienstes bleibt standardmäßig erhalten.
Beim Selbstbedienungs-Export schreibt der native Archiv-Manager erst
eine **temporäre** Datei in seinen `StorageRoot/<Region-UUID>/`.
Nur nach erfolgreichem WebDAV-PUT wird das Remote-Archiv als
fertig gemeldet und die Arbeitsdatei gelöscht.
Beim Import wird das über ROBUST hochgeladene NAS-Archiv in eine
vorübergehende Datei am SIM geladen, geprüft und über den nativen
IAR-Archiver eingelesen. Auch diese Datei wird nach Jobabschluss
entfernt. **Vor dem Aktivieren den benötigten lokalen
temporären Speicher für maximale Archive berücksichtigen.**

### Retention – maximal 30 Tage

- Neue Self-Service-Archive erhalten eine eindeutige
  `<region-uuid>/<operation-uuid>.iar`-Adresse und eine
  `.ogl-retention`-Markierung im NAS-Ordner.
- Im aktiven ROBUST entfernt der stündliche Worker abgelaufene
  terminale Self-Service-Archive. Auch verwaiste, UUID-benannte
  Archivdateien können nach Jobstore-Rotation entfernt werden, **nur
  wenn eine passende WebDAV-Retention-Markierung mit ausreichend altem
  Änderungsdatum existiert**. Andere NAS-Dateien werden nicht gelöscht.
  Downloads werden nach Ablauf vor der physischen Löschung gesperrt.
- **Einschränkung:** Bei WebDAV-Ausfall, nicht unterstützter
  PROPFIND-/HEAD-Metadatenabfrage, fehlendem Marker oder
  gestopptem ROBUST läuft die physische Löschung nicht garantiert
  fristgerecht. Für eine garantierte 30-Tage-Grenze zusätzlich eine
  unabhängige Retention auf dem NAS einrichten, **nur auf dem
  dedizierten IAR-Archivordner**. NAS-Snapshots/Recycle-Bin haben
  eigene Retention.
- Staged Uploads nach über 24 Stunden Inaktivität werden beim
  nächsten Jobstart des Avatars verworfen. Crash-Restbestände
  auf dem Simulator erfordern derzeit operative Kontrolle.
- Kennwort-/Zertifikatsfehler, NAS-Ausfälle, nicht lesbare Archive
  und WebDAV-Fehler führen zu einer Fehlermeldung. Es gibt **keinen
  automatischen dauerhaften lokalen Backup-Fallback**.

### Legacy-Backend: NFS

Für Hosts mit NFS-Kernelunterstützung kann weiter verwendet werden:

```ini
[OglIarSelfService]
Enabled = true
StorageBackend = "NFS"
SharedStorageRoot = "/mnt/ogl-iar"
NasHost = "10.78.0.4"
RetentionDays = 30
MaximumArchiveMiB = 2048
```

Im Simulator dazu denselben **tatsächlich gemounteten**
`[OpenGenesisLINKIAR].StorageRoot=/mnt/ogl-iar` einstellen.
Die vorhandene Linux-Mountinfo-Überprüfung bleibt fail-closed.
Auf dem OpenVZ-Server von 1blu ist dieser Modus mangels NFS-Client
derzeit nicht nutzbar. `StorageBackend` ist **kein Protokoll-Umschalter
für die administrative Legacy-IAR-API**; deren bestehende Dateipfade
sind gesondert zu beachten.

## Aktivierung und Betrieb

1. Vorhandene `Robust.HG.ini` und `OpenSim.ini` sichern, da
   `git pull` keine lokalen Änderungen überschreiben darf.
2. WebDAV-Ordner und NAS-Account auf UGOS einrichten; HTTPS-Zertifikat
   und Dateirechte prüfen.
3. Passwortdatei auf jedem betroffenen Host sichern und konfigurieren.
4. ROBUST und Simulatoren auf dieselbe neue Version aktualisieren,
   beide Konfigurationen für WebDAV setzen und neu starten.
5. **Zuerst Staging mit Testavatare**: Import und Export von
   `>100 MiB` testen, SHA-256, Download, 30-Tage-Status, Cleanup,
   NAS-Ausfall, Wiederanlauf und Rechtefilter Copy+Transfer prüfen.
6. Abgeschlossene Jobs kontrollieren: fertige Archive müssen auf
   dem NAS liegen, temporäre lokale Arbeitsdateien verschwinden.
7. Unabhängige 30-Tage-NAS-Retention und Speicher-/Netzmonitoring
   ergänzen. Ein WebDAV-Zertifikatsfehler darf nicht übergangen werden.

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
- WebDAV verbindet ROBUST und Simulator ohne Shared-Mount; temporäre native Arbeitsdateien sind erforderlich.
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
- Vollständige NAS-seitige Dateiretention auch bei Offlinezeiten,
  ein generischer Browser-Download ohne
  Clientcode und vollständig gridgebundene Quotenverwaltung sind
  Folgeschritte. Ein Export wird in dieser Version nur blockweise
  heruntergeladen, um den Arbeitsspeicher des Webservers zu schützen.

Die bestehende administrative `/api/v1/iar`-Schnittstelle und
Regionenverwaltung bleiben separat, unverändert authentifiziert und
benötigen weiterhin `regions:manage`.
