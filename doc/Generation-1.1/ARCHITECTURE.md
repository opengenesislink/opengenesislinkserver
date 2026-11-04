# OpenGenesisLINK Generation 1.1 — Zielarchitektur

Status: **Entwicklungsplanung / kein produktiver Release**  
Basis: Generation-1-`main` zum Start des Zweiges, Commit `7a745a90b44b20d1317a5a1283b05cfd32fdf9c3`.  
Ziel: evolutionäre Modernisierung der bestehenden G1-Plattform; **nicht Generation 2 / GridFabric**.

## Verbindliche Ziele

1. Neue, robuste, providerbasierte **JPEG2000-Decoding-Engine** statt des bisherigen J2C-/CSJ2K-Standardpfads.
2. Messbare **Leistungsoptimierungen und Fehlerbehebungen** in Simulator, ROBUST, IAR, Assets, NexBus und Rendering.
3. **Durchgängige HTTPS-/TLS-Unterstützung** für öffentliche und interne HTTP-Endpunkte mit automatisch erneuerten Let's-Encrypt-Zertifikaten.
4. **World API als separat versioniertes Addon**, nicht mehr als fest integrierter ROBUST-API-Connector samt aller Endpoints im Serverkern.
5. **Durchgehende Migration auf .NET 10 / C# 14**, einschließlich ROBUST, Simulator, Addons, Buildtools, CI und Laufzeitpaketen.

Die bestehenden Datenbanken, UUIDs, Assets, Inventare, Regionen, Estates, Viewer- und Hypergrid-Funktionen dürfen beim Upgrade nicht verloren gehen. Der produktive G1-`main`-Zweig wird nicht als Entwicklungsspielwiese verwendet. Kein automatischer Merge, kein Deployment und keine produktive INI-Überschreibung.

## 1. OGL Image Codec Engine

### Istzustand
- `OpenSim/Region/CoreModules/Agent/TextureSender/J2KDecoderModule.cs` nutzt `CSJ2K` und fällt auf `OpenJPEG` zurück.
- `IJ2KDecoder` gibt öffentlich `OpenJPEG.J2KLayerInfo[]` weiter; weitere Subsysteme nutzen `OpenJPEG.DecodeToImage` unmittelbar, darunter Mesh/Sculpt, Warp3D, Karten, Profilbilder und World-API-Medien.
- Vorhandene Asset-Daten sind überwiegend JPEG2000-Codestreams. **Ein neuer Decoder bedeutet nicht automatisch ein neues kompatibles Inworld-Texturformat.**

### Ziel
- Providerabstraktion `IOglImageCodec` mit `Identify`, `Inspect`, `DecodePixels`, `DecodeLayerBoundaries`, `EncodeCompatibleTexture`.
- Primäradapter: aktuelle, gepinnte OpenJPEG-2.x-Bibliothek über isoliertes und plattformübergreifend getestetes Native-Interop. Native-Abhängigkeiten für Linux x64/ARM64 und Windows x64 paketieren.
- Sekundäradapter: kompatibler Legacy-Decoder für klar definierte Formate und geordnete Fallbacks; kein pauschales Verschlucken von Fehlern oder Erfinden von Layergrenzen als vermeintlich valide Ergebnisse.
- HTJ2K/OpenJPH zunächst nur als Forschung/optionaler Provider. Kein HTJ2K-Upload zum Standard machen, solange Viewer- und Grid-Kompatibilität nicht nachgewiesen ist.
- Cache auf Grundlage von Asset-UUID, Inhaltsfingerabdruck, Decoder-Version und Decode-Optionen; Limits für Auflösung, Komponenten, Dekompressionsgröße, Zeit und Parallelität. Fuzzing und fehlerhafte Assets müssen Prozesse nicht zum Absturz bringen.
- Fehlerarten getrennt ausweisen: Asset fehlt, ungültiger Asset-Typ, beschädigter Codestream, unsupported feature, Codec-Ausnahme, Timeout. Ein anderer Decoder kann **beschädigte** Assets nicht zuverlässig wiederherstellen.
- Alte `IJ2KDecoder`-Vertragsoberfläche durch Adapter kompatibel halten, bis alle Verbraucher migriert sind.

### Abnahmekriterien
- Testkorpus aus echten anonymisierten G1-J2C/JP2-Assets sowie gültigen und korrupten Dateien.
- Messung Decoder-Erfolgsquote, CPU-Zeit, P95-Latenz, Speicherpeak und Pixel-/Layerparität gegenüber G1.
- Viewer-Tests für Texturen, Mip-/Quality-Layer, Sculpties, Mesh, Terrain, Weltkarte und Profilbilder.
- Keine ungeprüfte Änderung des Asset-Formats in der Datenbank oder über das Viewer-Protokoll.

## 2. Optimierung und Defektbehebung

- Zunächst reproduzierbare Baseline: Profiling/Tracing für ROBUST, Simulator, AssetService, NexBus, Region-Start/TP, IAR und Datenbank.
- Fehler priorisieren: P0 Datenverlust/Sicherheit, P1 Login/TP/Assets, P2 Rendering/Performance, P3 Komfort.
- Konkrete Arbeitspakete: IAR-Export/Import und Statuskonsistenz, NAS-WebDAV-Abschlussbestätigung, fehlende Asset-Referenzen, begrenzte asynchrone NexBus-Aufträge, DB-Pool und Hot-Path-Queries, Entity-/Asset-Caching, Rendering-/Sculpt-Fehlerpfade.
- Kein ungeprüfter Wechsel der Physikengine, Datenbankschemata oder Inventory-Permissions im Rahmen allgemeiner Optimierungen.
- Vorher-nachher-Benchmarks, Last- und Langlauftests, Memory-/CPU-Profile, reproduzierbare Regressionstests und Rollbackmöglichkeit.

## 3. HTTPS/TLS + Let's Encrypt

### Istzustand
- ROBUST besitzt HTTPS-/Zertifikatsoptionen, aber die produktive G1-Konfiguration enthält auch HTTP-Endpunkte und deaktivierte Zertifikatsprüfungen (`NoVerifyCertChain=true`, `NoVerifyCertHostname=true`).
- Simulator-CAPS, Login, Hypergrid/Gatekeeper, EventQueue, World API, Asset- und Medienendpunkte besitzen unterschiedliche URL- und Portmodelle.

### Ziel
- Zentrale `OglTlsEndpointPolicy` / `OglCertificateProvider` für Zertifikatsquelle, Hosts, Zertifikatsrotation, Bindings und URL-Generierung.
- Betriebsmodi: **Reverse-Proxy-TLS** (Apache/HAProxy für statische öffentliche Hosts) und **direktes TLS pro Listener** (wenn Simulator-CAPS/Ports oder Grid-Protokoll es erfordern). Proxy-TLS allein macht nicht automatisch alle Simulatorports HTTPS-fähig.
- Let's Encrypt via ACME-Client und automatische Erneuerung; HTTP-01 über öffentliches Port 80 oder DNS-01 über API-fähigen DNS-Provider, z. B. für Wildcard-/mehrere Servernamen. Schlüssel außerhalb Git und mit restriktiven Dateirechten.
- Sichere Zertifikatsrotation ohne unnötigen Serviceausfall; Reload, Ablaufwarnungen, Healthchecks, vollständige Zertifikatskette und korrekter Hostname.
- In G1.1-Produktion beide `NoVerifyCert...`-Bypasses entfernen. Legacy-Fremdgrid-/HG-Kompatibilität **explizit testen** und erforderlichenfalls gesondert dokumentierte Übergangsroute; niemals global Zertifikatsprüfung abschalten.
- Matrix der tatsächlich genutzten Endpunkte: ROBUST/Login/Gatekeeper, Simulator-Seed-CAPS, EventQueue, Assets/Textures, World API, Voice/WS, interne NexBus-Verbindungen, WebDAV/NAS. Sorgfältige Prüfung von Redirects und Viewer-Protokollverträglichkeit.

### Abnahmekriterien
- HTTPS/TLS 1.2+/1.3 je nach Clientkompatibilität, gültige vollständige Kette, automatisierte Rotation, keine Klartext-Credentials, keine unsichere globale Zertifikatsvalidierung.
- Firestorm/TrixieStorm Login, Teleport, Mesh-/Texturstreaming, Inventory, Script-HTTP, HG nach Kompatibilitätsmatrix; authentifizierte APIs unter HTTPS.
- Negativtests für abgelaufene, falsche und selbstsignierte Zertifikate.

## 4. World API als eigenes Addon

### Istzustand
`NexVerse/Server/Api/NexVerseWorldApiConnector.cs` registriert die World-API-Endpunkte direkt im Servercode und enthält einen sehr großen Servicegraphen. Eine reine Ordnerumbenennung genügt nicht für ein austauschbares Modul.

### Ziel
- Eigenes separat baubares, versioniertes Paket `OpenGenesisLINK.Addons.WorldAPI`, perspektivisch eigenes Repository `opengenesislinkworldapi`.
- Im Kern bleiben nur generische, stabile Serviceverträge, Plugin-Discovery, Lifecycle, Authentifizierungs-/Berechtigungskontrakte, Host- und HTTP-Routing-Abstraktionen sowie Event-/Job-Verträge.
- **Kein Core-Compile-Verweis auf konkrete World-API-Implementierungen.** World API kann explizit aktiviert/deaktiviert und separat aktualisiert werden.
- Addon-interne `ApiHost`, `Routes`, `Auth`, `Jobs`, `Media`, `Profiles`, `Admin`, `Metrics`, `Docs`, `WorldApiBootstrap` mit sauberen DI/Lifecycle-Schnittstellen.
- Core-nahe NexBus-/Asset-/Simulator-Verträge in einem schlanken `OpenGenesisLINK.Contracts` halten, um kreisförmige Abhängigkeiten zu verhindern.
- API-Kompatibilität (`/api/v1`, Auth, Fehlercodes, Portal- und Viewer-Integrationen, Backwards-Compatible JSON) vor Migration einfrieren und contract-/End-to-End-testen.
- Migrationsfolge: Inventarisieren -> Verträge extrahieren -> unabhängiges Addon bauen -> alte Handler parallel nur im Test vergleichen -> exakt eine Registrierung im Zielbetrieb -> alten Connector entfernen.
- Kein Verlust von Benutzerrechten, Auditdaten, Jobs oder World-API-Kontinuität.

### Abnahmekriterien
- ROBUST startet und funktioniert ohne World-API-Addon.
- Mit Addon: identische API-Routen, Berechtigungen und Datenzustände; keine Doppelregistrierung.
- Addon-Build/-Release/-Rollback unabhängig vom Hauptserver möglich.

## 5. .NET 10 / C# 14

- Offener Altentwurf: PR #220 (`upgrade/net10-pre-09313`, Stand vom Oktober 2026). Nur als Referenz nach Rebase-/Diff-Prüfung, nicht blind mergen.
- Alle `net8.0`-TFMs auf `net10.0` umstellen: ROBUST, Simulator, Helfer, unabhängige Addons, Tools, Tests und runtimeconfig-Dateien; CI-Builds, Container, systemd und Runtime-Installer aktualisieren.
- Paket-/Native-Interop-Audit (OpenJPEG, libOMV, Mono.Addins, System.Drawing, libgdiplus, MariaDB-Connector, Scripts/LSL/OSSL, WebSockets, TLS, Reflection, Prebuild/MSBuild).
- Portable `linux-x64`, `linux-arm64`, `win-x64` Release- und Smoke-Tests; RAM-/CPU-Baseline und native Abhängigkeiten prüfen.
- C# 14 kann genutzt werden, ersetzt aber keine notwendige ABI-/Plugin-Migrationsprüfung. Inkompatible Addons brauchen explizite kompatible Neubuilds.
- Keine .NET-8- und .NET-10-DLLs ungeprüft in einem Release mischen. Release-Manifest mit Laufzeit- und Plugin-Kompatibilität.

## Betrieb, Sicherheitsgrenzen und Migration

- Release-Line G1.1 eigenständig, G1 bleibt vorerst rückrollbar. Produktiven G1-`main` nicht automatisch auf G1.1 umstellen.
- Upgrade nur mit konsistenten DB-/FSAssets-/Inventar-/Konfigurations-Backups und geübtem Restore. Secrets nie im Git versionieren.
- Testumgebung mit echten G1-Kompatibilitätsfällen, aber anonymisierten oder entsprechend geschützten Daten.
- Feature-Flags, Migration Logs, eindeutig benannte Releases und Rollback-Artefakte für jeden großen technischen Schritt.
- G1.1 wird erst produktionsreif erklärt, wenn fünf Abnahme-Gates und vollständige CI inklusive Runtime-/Integrationstests bestanden sind.

## Nicht Bestandteil dieses Projekts

- Generation 2 / GridFabric-Neuimplementierung und inkompatibles Federation-Protokoll.
- Erzwungene Konvertierung aller bestehenden JPEG2000-Texturen zu HTJ2K.
- Wechsel von MariaDB zu einer anderen Datenbank oder eine Datenmigration ohne Notwendigkeit.
- Eine Zusage, dass jeder tatsächlich defekte Asset-Codestream automatisch reparierbar sei.
