# OpenGenesisLINK Generation 1.1 — Umsetzung und Release-Gates

**Status (04.11.2026):** G11-01 .NET-10-Fundament auf Linux x64, Linux ARM64 und Windows x64 **gebaut, als Staging-Paket geprüft und ROBUST-HTTP-Start erfolgreich**. Vollständige MariaDB-/Region-/Viewer-End-to-End-Abnahme, G11-03/04/05 und G11-06 weiterhin offen. Keine Produktivumstellung. Siehe [NET10_RUNTIME_STATUS.md](NET10_RUNTIME_STATUS.md).  
**Technische Basis:** `doc/Generation-1.1/ARCHITECTURE.md`.  
**Produktziel:** OpenGenesisLINK Generation 1.1, modernisierte kompatible G1-Linie, kein G2-/GridFabric-Ersatz.

## Ablauf und Abhängigkeiten

| Phase | Schwerpunkt | Voraussetzung | Ergebnis / Gate |
|---|---|---|---|
| G11-00 | Baseline & Bestandsaufnahme | aktueller G1-Hauptzweig | Buildmatrix, API-/Codec-/TLS-Inventar, reproduzierbare Last- und Fehlerfälle, Rollbackkonzept |
| G11-01 | .NET 10 Foundation | G11-00 | alle Hauptprojekte und Addons unter `net10.0`; CI/Release/Runtime kompatibel |
| G11-02 | OGL Image Codec Engine | G11-01; echte J2C-Testassets | providerbasierter Decoder, Textur-/Layerparität, Fuzzing, ARM64 und x64 geprüft |
| G11-03 | World API Modularisierung | G11-01; eingefrorener API-Vertrag | eigenständiges Addon, Kern ohne World-API-Abhängigkeit, Contract-Testgrün |
| G11-04 | HTTPS / Let's Encrypt | G11-01; Endpoint-Inventar | sichere TLS-Einrichtung und Zertifikatserneuerung auf allen unterstützten G1.1-Endpunkten |
| G11-05 | Stabilität, Optimierung & Datenintegrität | fortlaufend, integrierte G11-02–04 | Profiling, Fehlerregressionen, IAR-/Asset-Integrität, Lasttest und Rollback |
| G11-06 | Release Candidate / Migration | alle obigen Gates | staging-echte End-to-End-Tests, Viewer/HG-Kompatibilität, produktionsfähige Rollback-Artefakte |

Phasen G11-02, G11-03 und G11-04 können in eigenen Featurebranches parallel umgesetzt werden; G11-01 und G11-00 legen die gemeinsamen Verträge fest. Die Zielversion einer eigenständigen G1.1-Release-Linie wird vom Branch-/Tag-Schema getrennt dokumentiert; G1 bleibt unverändert.

## G11-01: Nachgewiesene .NET-10-Zwischenergebnisse

- Die Server- und Hauptaddonprojekte kompilieren unter `net10.0` auf allen drei Plattformen.
- Die echte `Robust.dll` startet unter .NET 10 und beantwortet mit isolierter Test-INI eine lokale HTTP-Anfrage.
- Linux x64, Linux ARM64 und Windows x64 haben je ein getestetes **frameworkabhängiges Staging-Paket** mit Manifest und ohne Live-Daten.
- Die Build-/Runtime-/Paketprüfungen stehen in eigenen G11-01-CI-Workflows und hängen **nicht** vom experimentellen JPEG2000-Decoder ab.
- Noch offen: voller Simulator-Regionstart, MariaDB/Inventar, LSL, Login, Hypergrid, E2E-Staging, Release-/Rollbackabnahme.

Verbindliche Nachweise und Startanleitung: [NET10_RUNTIME_STATUS.md](NET10_RUNTIME_STATUS.md).

## G11-00: Baseline

- Ausgangscommit und ungemergte PRs inventarisieren, einschließlich altem .NET-10-PR #220.
- Alle `net8.0`-TFMs, Buildskripte, `.runtimeconfig.json`, vorgepackte DLLs und Native-Abhängigkeiten erfassen.
- Alle JPEG2000-Verbraucher und `IJ2KDecoder`-Abhängigkeiten erfassen (Viewer Layer, Sculpt, Map, WebAPI-Media).
- Alle öffentlichen und internen HTTP-/HTTPS-Listeners, Hosts und Viewer-URLs auflisten.
- World-API-Routen, Scopes, Jobs, Auth, DB-Tabellen, Event-Verträge und abhängige Module tabellieren.
- Smoke-Test-Korpus für Login, Teleport, Karten, Assets, Voice, Inventory, IAR und Welt-API definieren.
- Produktiven Stand für Rollback sichern; lokale INIs und Datenbankzugänge nie automatisiert überschreiben.

## G11-01: .NET 10

- SDK, `global.json`, TFMs, CLI, Prebuild-/MSBuild-Skripte, native Bibliotheken und Workflow-Runners migrieren.
- Kompatibilität einzelner OpenSim-/libOMV-/Mono.Addins- und NuGet-Assemblies nachweisen.
- Release-Gate: `dotnet build --configuration Release`, alle Runtime- und Integrationstests auf Linux x64, ARM64 sowie Windows x64 ohne .NET-8-Runtime-Abhängigkeit.
- Protokoll-, DB- und Script-/LSL-Parität dokumentieren.

## G11-02: Decoder

- Einen passgenauen `IOglImageCodec`-Vertrag vor Änderungen am Bestandspixelpfad definieren.
- Testassets kategorisieren: gültige J2K-/JP2-Dateien, historische Randfälle, beschädigte Assets, übergroße Bilder und ungültige Header.
- Aktuellen OpenJPEG-Codec anbinden, Native-Interop gegen Speicherfehler und Fehlerzustände absichern.
- Layer-Boundaries und Texturbilder gegen Viewer und Legacy-Material vergleichen.
- Benchmark-Gate: Erfolgsquote >= gültiger G1-Baseline, keine regressiven Crashes, dokumentierte CPU-/RAM-Ergebnisse; beschädigte Bytes nicht unzulässig als erfolgreich melden.

## G11-03: World API Addon

- `NexVerseWorldApiConnector.cs`-Routenliste und Tests als Vertrag einfrieren.
- Core/API-Contracts extrahieren; kein Core->Addon-Zyklus.
- Addon-Projekt, Bootstrap, Konfiguration, Berechtigungsprüfung, Routing, Jobs, Audit und OpenAPI isolieren.
- Bestehende Endpunkte auf strukturell identische Addon-Handler umstellen; Legacy-Connector erst entfernen, wenn Route-/Auth-Parität bewiesen ist.
- Addon deaktiviert: ROBUST läuft; Addon aktiviert: exakt ein API-Router registriert. Eigenständige Addon-Release-Versionierung.

## G11-04: TLS

- ACME/Let's Encrypt Integration über bestehende kontrollierte ACME-Clients und automatisierte Rotation; Zertifikatsgeheimnisse ausschließlich außerhalb des Git-Arbeitsverzeichnisses.
- TLS-Architektur für Login/HG/ROBUST, Sim-CAPS, Asset/Texture-URLs, EventQueue, WebAPI, NexBus und Voice verifizieren.
- Strikte Validierung von Hostname und Chain; kein genereller `NoVerifyCertChain`-/`NoVerifyCertHostname`-Bypass.
- Kompatibilitätssuite: Firestorm / TrixieStorm, Login, TP, Maps, Assets, Scripting HTTP, HG Interoperabilität.
- Zulässige Übergangs-HTTP-Routen separat begründen und nicht mit vollständiger TLS-Abdeckung verwechseln.

## G11-05: Stabilität und Fehler

- Priorisierte Defektliste mit Severity, Reproduktionsschritten, Regressionstest und Metriken.
- IAR-Export/Import >2 GiB und bis 10 GiB mit echter Wiederherstellungsprüfung; korrekte Marker-/Job-Zustände und NAS-PUT/HEAD-Bestätigung.
- Fehlende/defekte Asset-Referenzen nicht stillschweigend als vollständiges Backup melden.
- NexBus-Timeouts, Async-Backpressure, Memory-Pressure, Datenbankhotspots und Region-Start-/TP-Latenzen benchmarken.
- Nur Änderungen mit messbarem Erfolg und gesichertem Rollback übernehmen.

## G11-06: Release Candidate

1. CI vollständig grün und reproduzierbare signierte Releases.
2. Migration auf Staging-Kopie einer realen G1-Welt, inklusive MariaDB und FSAssets.
3. Backup + Restore sowie Update + Rollback erfolgreich.
4. Einwandfreier Viewer-Login, TP, Assets, Texturen, Voice, Hypergrid (dokumentierte Fremdgrid-Ausnahmen).
5. Gültiges HTTPS und Zertifikatsrotation auf sämtlichen als unterstützt definierten Endpunkten.
6. World API ausschließlich durch Addon bereitgestellt, bestehende API-Kontrakte kompatibel.
7. Sicherheits- und Performance-Baseline gegenüber G1 dokumentiert; keine ungeklärten P0/P1-Defekte.

## Entwicklungsregel

- Jeder technische Block erhält eine eigenständige PR gegen `develop/generation-1.1`.
- Bestehende PR #220 ist **ein Referenzstand**, kein automatisch mergebarer Upgrade-Branch.
- Keinerlei direkte Änderungen an G1-`main`, Live-INI-Dateien oder produktiven Diensten ohne separate, bewusste Freigabe.
- Vertrauliche Zertifikate, Tokens, DB-Passwörter und NAS-Zugangsdaten gehören weder in Issues noch in Commits.
