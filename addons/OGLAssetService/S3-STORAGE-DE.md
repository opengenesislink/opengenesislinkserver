# OpenGenesisLINK Generation 1 - optionaler S3-Object-Storage-Provider

**Status: Implementiert als Opt-in-Quellcode, NICHT produktiv aktiviert.**
**Version: OpenGenesisLINK Generation 1 / .NET 10 / 0.9.4.0.**
Das bestehende FSAssets-Backend, seine Datenbank, alle aktiven INIs
und das produktive Grid bleiben unveraendert. Die Auswahl wird durch
Build, Merge, Installation oder fehlende S3-Konfiguration **nicht**
automatisch geaendert.

## Bestehende Architektur und Einhaengepunkt

`OGLAssetConnector : FSAssetConnector, IAssetService` bleibt der einzige
OGL AssetService-Connector. Die vorhandenen `IFSAssetDataPlugin`-Adapter
verwalten unveraendert das OpenSim-Metadatenschema (UUID, Name,
Beschreibung, Type, ContentType, CreatorID, Local, Temporary, Flags,
CreationDate und SHA-256-Payload-Hash). Keine neue S3-Metadatenbank und
kein neues oeffentliches AssetService-Protokoll.

Bei explizitem S3-Betrieb ersetzt `OGLS3ObjectStore` **nur die
Payload-I/O innerhalb des vorhandenen OGLAssetConnectors**. Der vorhandene
SQL-Metadaten-Adapter bleibt die UUID-zu-SHA256-Quelle der Wahrheit.
`OGLObjectCache`, optional `OGLDiskCache` sowie die vorhandenen
Invalidierungswege werden weiterhin verwendet. Die Caches sind
weiterhin nur mit `SingleWriterCacheEnabled=true` und den bereits
bestehenden Sicherheitsbedingungen nutzbar. Redis wird nicht dupliziert.

```text
ROBUST / vorhandene IAssetService-Schnittstelle
    |
    OGLAssetConnector (bestehender FSAssetConnector)
    |-- Standard: FSAssets-Metadaten + FSAssets-Payload (bisheriges Backend)
    |-- opt-in:  FSAssets-SQL-Metadaten + OGLS3ObjectStore (Payload)
    |             |
    |             AWS SDK for .NET 10 / S3 API / HTTPS oder privates VPN
    |             |
    |             S3-Bucket (MinIO, AIStor, kompatible Objektspeicher)
    +-- bestehender OGL-RAM-/Disk-Cache und optionale Redis-Koordination
```

Die zugrundeliegende `FSAssetConnector`-Initialisierung und
SQL-Metadaten-Abhaengigkeit bleiben derzeit erhalten. S3 ist daher
**kein selbststaendiger Ersatz fuer die Metadatenbank**. Die bestehenden
FSAssets-Initialisierungskomponenten bleiben im S3-Testbetrieb im
Prozess geladen. Dies ist eine bewusste, kompatibilitaetsorientierte
Einbettung, keine parallel entwickelte AssetService-Architektur.

## Unverbindliches Konfigurationsbeispiel (immer AUS)

Aus `OGLS3Storage.ini.example`:

```ini
[OGLS3Storage]
    Enabled = false
    Endpoint = http://127.0.0.1:9000
    Bucket = ogl-assets
    Region = us-east-1
    AccessKey =
    SecretKey =
    ForcePathStyle = true
    UseSSL = false
    ConnectionTimeoutSeconds = 10
    RequestTimeoutSeconds = 60
    MaxRetryAttempts = 3
    HealthCheckEnabled = false
```

`[OGLAssetService] StorageBackend` ist standardmaessig `fsassets`.
Eine Aktivierung **nur in einem isolierten Staging** setzt gleichzeitig
folgende Werte voraus:

```ini
[OGLAssetService]
    Enabled = true
    Mode = compatibility
    StorageBackend = s3
    SingleWriterCacheEnabled = false
    RedisEnabled = false
    ParallelStoreEnabled = false
    ExperimentalAtomicUpdatesEnabled = false

[OGLS3Storage]
    Enabled = true
    Endpoint = http://10.80.0.2:9000
    Bucket = ogl-assets
    Region = us-east-1
    ForcePathStyle = true
    UseSSL = false
```

Die private IP ist **nur ein Beispiel**, keine Vorgabe fuer den
UGREEN DXP2800. WireGuard wird auf dem Betriebssystem bereitgestellt,
nicht im AssetService. Es gibt keinerlei fest eingebaute NAS- oder
WireGuard-IP und keinen oeffentlichen S3-Zugriffzwang.

Fehlt die Sektion oder ist `Enabled=false`, werden **keine**
S3-Zugangsdaten gelesen, kein S3-Client erzeugt, keine S3-Verbindung
aufgebaut, keine Buckets angelegt und keine Hintergrundjobs gestartet.
Ein irrtuemlich gesetzter einzelner Aktivierungsschalter erzeugt einen
Startfehler statt eine stille Backend-Umschaltung.

**Zugangsdaten** ausschliesslich in privater Secret-Verwaltung oder
`OGL_S3_ACCESS_KEY` / `OGL_S3_SECRET_KEY`, jeweils mit Zugriff nur
fuer den Dienstbenutzer. Die leeren INI-Felder dienen lediglich als
manueller Ausweichweg; nie reale Credentials einchecken oder loggen.
Es werden keine HTTP-Endpoints fuer S3-Credentials bereitgestellt.

## Objektformat und S3-Operationen

Die **originalen Asset-Bytes** werden unveraendert unter einem
Content-Addressing-Key abgelegt:

```text
Bucket: ogl-assets
Key: assets/sha256/<erste2>/<naechste2>/<64-Zeichen-SHA256>
Beispiel: assets/sha256/ab/cd/abcd...(64 Hexzeichen)
```

Keine JSON- oder ZIP-Huelle um das Payload; die bestehenden
FSAssets-SQL-Metadaten bleiben unveraendert. UUIDs sind nicht im S3-Key,
sondern im vorhandenen SQL-Index zur Payload-SHA256 gespeichert. Das
gewohnte `IAssetService`-API verwendet weiterhin UUIDs.

- **PUT:** `If-None-Match: *` verhindert Ueberschreiben; 412 wird nur
  nach vollstaendigem GET und Hashvergleich als idempotenter Erfolg
  akzeptiert. Ein neues PUT meldet Erfolg erst nach S3-Antwort sowie
  HEAD-Laengenpruefung und danach erfolgreichem SQL-Metadatenschreiben.
- **GET:** Inhalt wird vollstaendig gelesen und SHA-256 geprueft, bevor
  er an OpenSim zurueckgegeben wird. Ungueltige/abgebrochene Antworten
  fuehren nicht zu einem erfolgreichen Asset.
- **HEAD:** Prueft das Objekt ohne vollstaendigen Download; 404 bedeutet
  fehlendes Objekt, 403/5xx/Timeout eine Betriebsstoerung. Eine
  existierende UUID ohne Blob ist ein Integritaetsfehler, kein leeres
  Asset.
- **DELETE:** Entfernt bei autorisiertem `IAssetService.Delete` nur
  die bestehende UUID/Hash-Zuordnung aus dem SQL-Metadatenspeicher.
  Content-Addressed S3-Blobs koennen von mehreren UUIDs referenziert
  sein und werden **niemals automatisch physisch geloescht**.
  Referenzzaehlung und sichere Garbage Collection gehoeren in ein
  separates, explizit freizugebendes Offline-Verfahren.
- **UpdateContent:** Nur ueber die bestehende opt-in
  `IAssetService`-Interface-Dispatch-Methode und einen
  `IFSAssetHashCompareExchange`-faehigen Metadaten-Adapter. Der neue
  Blob wird zuerst durabel geschrieben; die UUID/Hash-Zuordnung wird
  anschliessend atomar aktualisiert. Ein CAS-Konflikt ist **kein
  erfolgreicher Update**.

Die S3-Schreiboperation meldet niemals ohne S3-Erfolg eine erfolgreiche
Speicherung. **Keine automatische Migration, kein gemischter
Schreib-Backend-Fallback**, insbesondere nicht bei NAS-Ausfall.
Fehler werden an die aufrufende Komponente weitergegeben.

## Timeouts, Gesundheit, Sicherheit

Das AWS-SDK erhaelt MaxErrorRetry mit konfigurierter Obergrenze, einen
eigenen ConnectionTimeout und CancellationTokens pro Request.
Keine unbegrenzten Retry-Loops und keine stillschweigende Fehler-
Umdeutung. Der Bucket wird bei bewusster S3-Auswahl mit HEAD geprueft.
`HealthCheckEnabled=false` startet keinen periodischen Health-Worker;
die One-Shot-Pruefung `CheckBucket()` wird bei explizit aktivierter S3-
Auswahl beim Start ausgefuehrt. Nur falls
`HealthCheckEnabled=true` bei aktivem S3 gesetzt ist, startet ein
optionaler Health-Timer mit einem begrenzten HEAD-Bucket-Probe alle
60 Sekunden. Gleichzeitige Probes werden vermieden. Der interne Status
`S3HealthStatus`, die Zaehler `S3HealthChecks` und
`S3HealthFailures` sind im OGL-Connector fuer vertrauenswuerdige
lokale Diagnose verfuegbar; keine Bucket-Namen, Zugangsdaten oder
Endpoints werden mit diesen Statuswerten offengelegt. Keine
Bucket-Erstellung durch den Provider.

HTTP ist nur fuer Loopback oder private/ULA-IP-Adressen erlaubt
(z.B. WireGuard). Fuer oeffentliche Hosts ist HTTPS verpflichtend.
TLS-Zertifikatsvalidierung des SDK bleibt eingeschaltet; niemals global
deaktivieren oder eigene ungueltige Zertifikate durchwinken.
Endpoint-URLs duerfen keine eingebetteten Zugangsdaten enthalten.

Ein S3-Dienst sollte auf einer privaten WireGuard-Schnittstelle
lauschen. Der Anbieter braucht Bucket-HEAD/ListBucket (fuer 404 vs
403), `s3:GetObject`/HEAD und `s3:PutObject` fuer
`assets/sha256/*`. `s3:DeleteObject`, `s3:CreateBucket`
und globale Administratorrechte sind **nicht** erforderlich.
Ein Storage-Bucket kann Versioning/Object-Lock zusaetzlich aktivieren.

## Isolierte Tests und Releasegrenze

```bash
dotnet build addons/OGLAssetService/OGLAssetService.csproj -c Release
dotnet run --project addons/OGLAssetService.S3.Tests/OGLAssetService.S3.Tests.csproj -c Release
dotnet run --project addons/OGLAssetService.Tests/OGLAssetService.Tests.csproj -c Release
```

Die GitHub-CI `ogl-s3-storage.yml` startet fuer den
`--s3-integration`-Test einen **ephemeren MinIO-Container nur auf
127.0.0.1:19119**, erzeugt einmalige Test-Credentials und einen
eindeutigen Bucket `ogl-s3-ci-<RUN_ID>`. Get/Put/HEAD,
fehlender Bucket, Integritaetsverletzung, Netzwerkfehler und
Idempotenz werden ausschliesslich dort geprueft. Keine produktiven
S3-Buckets, keine NAS-Verbindung, keine Aktivierung eines produktiven
ROBUST.

Die CI startet nach den direkten S3-Funktionstests eine **tatsaechliche
isolierte ROBUST-Instanz** mit temporaerer MariaDB-Testdatenbank und
MinIO-Testbucket: HTTP-Asset-Upload, Metadatenpruefung (UUID/Typ/Name/
Beschreibung), unveraendertes Binary-GET fuer Textur/Mesh/Sound/
Animation und Readback nach Neustart des ROBUST-Prozesses werden
geprueft. Die temporaeren FSAssets-Verzeichnisse bleiben frei von
Payload-Kopien. Die Testcontainer werden nach dem Job entfernt.

Die vorhandenen OGL-/FSAssets-/Redis-/SQL-/Viewer-/Hypergrid-
Regressionen bleiben Pflicht. Ein gruenes Ergebnis der CI ersetzt
keine spaetere Staging-Abnahme mit echter WireGuard-Verbindung und
separatem Metadatenbank-Klon.

## Vorbereitung einer spaeteren, NICHT implementierten Aktivierung

1. Snapshot von SQL, FSAssets-Base und Spool bei gestoppten Schreibern.
2. Bestehenden Offline-Migrationscode (OGLFsassetsMigrator /
   OGLParallelSnapshotMigrator / MetadataAudit) pruefen, aber **nicht**
   starten. Ein spaeteres separates S3-Migrationstool benoetigt
   UUID-zu-SHA256-TSV, Resume-Journal, Dry-Run, SHA-Pruefung und
   Audit-Bericht fuer verwaiste/mehrfach referenzierte Blobs.
3. Isolierten Metadatenbank-Klon und einen leeren Testbucket bereitstellen.
4. Vor dem Umschalten alle bereits referenzierten Hashes auf S3
   nachweislich auf Vollstaendigkeit und Integritaet pruefen.
5. LSL, OAR/IAR, Mesh/Textures, Sounds, Animationen und Hypergrid
   mit echtem Viewer und einem separaten Testgrid pruefen.
6. Belastungstests sowie WireGuard-Unterbrechung/Fail-Closed-Verhalten;
   Wiederanlauf und Rollback auf vorherigen SQL/FSAssets-Snapshot
   dokumentieren.
7. **Erst nach neuer Betreiberfreigabe** aktiven ROBUST-Connector und
   beide S3-Schalter bewusst im Staging aendern. Produktion niemals
   automatisch umstellen.

**Diese Erweiterung ist bewusst deaktiviert. Die bisherige
FSAssets-Installation arbeitet ohne neue Dienste und ohne S3-Zugangsdaten.**
