# OpenGenesisLINK 0.9.3.13 — Official Stable Release

**27. Oktober 2026 · Stable**  
**Basis:** OpenGenesisLINK Generation 1 / OpenSimulator 0.9.3.0  
**Vorgänger:** `v0.9.3.12`  
**Freigabe:** Betreiberentscheidung mit [dokumentierter Nachweisausnahme](09313_OPERATOR_RELEASE_DECISION.md) und verpflichtender erfolgreicher Gesamt-CI auf dem getaggten Commit.

## Neu in 0.9.3.13

### OGL AssetService Gen 1 — neue Speicher- und Cache-Architektur
- Modularer AssetService als integriertes Addon mit eigenständigen Build-, Migrations-, Cache- und Integrationstests.
- Optionale SHA-256-inhaltsadressierte Speicherung, UUID-/Hash-Metadatenprüfung, SQLite-Index und OGL-first-/Parallel-Store-Pfade.
- Sicherere Snapshot-/Restore-Vorbereitung einschließlich Preflight, SQL-Export-/Zeilenzahlkontrollen für MariaDB/PostgreSQL, Shard-/Symlink-Schutz und Negativtests.
- **Wichtig:** Produktive FSAssets bleiben autoritativ. Weder automatischer Asset-Cutover noch Legacy-Löschung werden durch ein Upgrade aktiviert. Neue Speicherpfade bleiben gesonderte Opt-ins.

### Modernisierte Mesh-, PBR- und Materialpipeline
- Verbesserte Mesh-Upload-Fee-Antworten und LLSD-Kompatibilität mit Legacy-Viewern.
- Absicherung der ZLib-LOD- und Convex-Hull-Verarbeitung, zusätzliche ungültige Mesh-/LLSD-Negativtests.
- Korrekturen für PBR-Face-Indices, glTF-JSON-Material-/Texturreferenzen sowie ORM-/Emissive-UV-Overrides.
- Stärker abgesicherte Parser und Upload-Verarbeitung bei fehlerhaften Materialdaten.
- **Nicht enthalten:** Eine pauschal zugesicherte native glTF-Mesh-Import-Pipeline oder ein neu entwickelter Viewer-Renderer.

### Größere Sichtweite über Regionsgrenzen
- Neues `OGLExtendedRegionVisibility`-Preset: bis 768 m Region-View-Grenze und bis 1.024 m maximale Server-Draw-Distance als Konfigurationsrahmen; die Firestorm-Sichtweite bleibt maßgeblich.
- Sicherere Regionsbereichsabfragen an Grid-Koordinatengrenzen.
- In der Stadt-NexVerse-`bin/OpenSim.ini` aktiviert; im `OpenSim.ini.example` und in den generischen Defaults standardmäßig deaktiviert.
- Beachten: höhere Render-, Child-Agent- und Netzwerklast ist möglich.

### LLUDP-Anmeldung und Verbindungen stabilisiert
- Fix für konkurrierende `UseCircuitCode`-Handshakes mit verschiedenen UDP-Quellports; gültige bestehende Sessions werden nicht mehr unnötig verdrängt.
- Sicherer Umgang mit entfernten/stalen AgentCircuits, fehlenden Circuit-Daten und Login-Cleanup.
- Zusätzliche automatisierte UDP-/Login-Regressionstests; Betreiber meldete nach Deployment keine Wiederholung des ursprünglichen Fehlers.

### FastMap 2.0: entkoppelter Regionsstart
- Optionale asynchrone Kartenberechnung nach Regionsregistrierung; bereits vorhandene Maptiles können beim Start weiter genutzt werden.
- Global begrenzte Map-Render-Jobs statt gleichzeitiger teurer Render-Aufrufe.
- Im Betreiberprofil `FastMapAsyncStartup=true` mit 45 s Startverzögerung; in konservativen Vorlagen ausgeschaltet.
- Kein pauschales Performanceversprechen ohne vergleichbare Lastmessungen.

### World API, Administration und Grid-Integration
- Erweiterte World-API-/Operator-Control-Center-Oberflächen: Admin-Dashboard, Diagnostik, Audit-Logs, Node-Log-Weiterleitung und GitHub-Release-Übersicht.
- Ergänzte API-Endpunkte für verwaltete Medien/Entitäten und abgesicherte serverseitige Datenoperationen; jeweils nur gemäß eigenen Berechtigungen/Scopes aktiv.
- Verbesserungen an NexGroups-/Gruppenlandabfragen, Sicherheits- und Berechtigungsprüfungen sowie an den CI-Regressionen.
- Zusätzliche Vorbereitungen für sichere Portal-Integration und Password-Recovery; Portal-Frontend und externe Dienste werden dadurch nicht automatisch aktiviert.

### Deployment und Konfiguration
- `bin/OpenSim.ini` an die bestätigte Stadt-NexVerse-Serverkonfiguration angeglichen, einschließlich erweiterter Sichtweite, IAR-Live-Fortschritt, FastMap-Async und gesicherter Gruppenland-API-Konfiguration.
- Eindeutige Simulator-`NodeId` über `NEXVERSE_NODE_ID` statt hart kodierter fremder Instanzkennung.
- Erneute NexVerse-CI einschließlich Release-Build, LLUDP-, Storage-/Asset-, Mesh-/LSL-, Hypergrid- und NV$-Datenbankmatrix-Regressionsläufen.

## Scope und Betriebsgrenzen

OpenGenesisLINK 0.9.3.13 wird auf ausdrückliche Betreiberentscheidung als **Stable** veröffentlicht. Die ursprüngliche 30-Fälle-Live-Matrix enthält zum Veröffentlichungszeitpunkt weiterhin nicht unabhängig nachgeprüfte Einzelbelegreferenzen; der bewusste Ausnahmeentscheid ist in `doc/NexVerse/09313_OPERATOR_RELEASE_DECISION.md` dokumentiert. **Dies ist keine Behauptung einer unabhängig verifizierten 30/30-Evidence.**

OGLSeasons-Neuaufbau, nativer Viewer, NexExperiences 2.0 und optionale neue Plattformmodule gehören nicht automatisch zum aktivierten Releaseumfang. Bestehende lokale Konfigurationen, Secrets, Datenbank-/FSAssets-/Spool-Daten und Regionen bleiben unverändert, bis der Betreiber ein gesondertes Deployment ausführt.

**Upgrade:** Konfigurationen und Daten konsistent sichern, ROBUST und Simulator-Binaries gemeinsam aktualisieren, keine lokale `OpenSim.ini` blind überschreiben. Für Rollback zur `v0.9.3.12` passende Datensnapshots verwenden.

[Release-Entscheidung](09313_OPERATOR_RELEASE_DECISION.md) · [Live-Akzeptanz-Matrix](09313_RELEASE_ACCEPTANCE.md) · [Scope](09313_SCOPE_AND_BLOCKERS.md)
