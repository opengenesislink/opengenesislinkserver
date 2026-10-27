# OpenGenesisLINK 0.9.3.13 Dev — GitHub-Pre-release

**Stand:** 27. Oktober 2026  
**Release-Typ:** Dev/Pre-release, **keine Stable-Freigabe**.  
**Code-Basis:** das nach erfolgreich abgeschlossener NexVerse-CI veröffentlichte `main`-Commit; der konkrete SHA ist am GitHub-Release-Tag abzulesen.

## Inhalt

- Eingebauter LLUDP-Fix gegen konkurrierende bzw. doppelte `UseCircuitCode`-Handshakes aus PR #321.
- Betreiber meldet nach Installation des Fixes keine erneuten `Scene.AddNewAgent`-Fehler; dies ist eine Live-Rückmeldung, **kein** vollständiger Regressionstest.
- Die produktionsnahe `bin/OpenSim.ini` wurde an die am 27.10.2026 bereitgestellte Stadt-NexVerse-Simulator-Konfiguration angepasst:
  - `[Startup] OGLExtendedRegionVisibility = true` als explizites opt-in im aktiven Hauptprofil;
  - `[Startup] OGLIarLiveProgress = true`;
  - `[Map] FastMapAsyncStartup = true` mit 45 Sekunden Startverzögerung;
  - `[NexDiscoveryViewer] GroupLandApiKey` als Environment-Platzhalter;
  - `[NexVerseNodeAgent] NodeId` als `${Environment|NEXVERSE_NODE_ID}` statt fest eingebauter Node-Kennung.
- Die generische `bin/OpenSim.ini.example` behält ihre konservativen Opt-in-Defaults. Andere INI-Dateien sowie vorhandene Runtime-Secrets und Datenbanken werden nicht verändert.

## Betreiberkonfiguration / Vorsicht

- Für die konkrete Hauptinstanz muss `NEXVERSE_NODE_ID=Mainland_Server_001` gesetzt sein, **falls** die Repository-Datei als Runtime-INI verwendet wird. Auf weiteren Simulatorinstanzen eine eigene eindeutige NodeId benutzen. Vorhandene lokale, korrekt konfigurierte `OpenSim.ini` niemals blind überschreiben.
- `NEXVERSE_GROUP_LAND_API_KEY`, NexBus-, World-API-, SMTP- und andere vertrauliche Werte bleiben außerhalb des Repositorys.
- `OGLExtendedRegionVisibility=true` kann Last und Child-Agent-Zahl erhöhen. `FastMapAsyncStartup=true` verlagert das Rendern von Maptiles in die Startphase im Hintergrund. Beide Einstellungen nach Last, Firestorm, Hypergrid und VarRegion bewerten; in `bin/OpenSim.ini.example` bleiben sie deaktiviert.
- Auf einem gemeinsamen vServer wurden zeitgleiche Watchdog-Timeouts mehrerer LLUDP-Threads gemeldet. Ursache und Langzeitverhalten sind nicht bewiesen; dieser Befund ist **nicht** als behoben dokumentiert.

## Freigabe- und Evidenzgrenzen

Die 30 verpflichtenden Gates C01–C05, A01–A09, M01–M07, V01–V04 und R01–R05 haben weiterhin **keinen vollständig nachgewiesenen Stable-Abschluss**. Die ursprünglichen PASS-Eingaben ohne Belegreferenzen genügen für die Stable-Freigabe nicht. Insbesondere gilt diese Veröffentlichung nicht als Nachweis für sichere AssetService-Cutover-/Restore-Pfade, glTF-/PBR-Live-Roundtrips, Multi-Sim-/HG-Kompatibilität oder die geforderte Lastprobe.

**Nicht durchführen:** keine automatischen produktiven Storage-Migrationen oder Umschaltung von FSAssets; keine Aktivierung unfertiger NexExperiences-CAPS oder Masterbank. Die Dokumente `doc/NexVerse/RELEASE_0.9.3.13_RC1.md` und `doc/NexVerse/09313_RELEASE_ACCEPTANCE.md` bleiben maßgeblich.

Ein späteres **Stable `v0.9.3.13`** erfordert einen getrennten Freeze, die vollständigen 30 belegten Prüfungen, die Betreiberfreigabe und erfolgreichen Build/CI für den exakt getaggten Commit. Dieses Dev-Tag darf nicht nachträglich in Stable umetikettiert oder umgebogen werden.

## Installation und Rollback

Vor einer Installation konsistente Snapshots von SQL/FSAssets/Inventar/Regionen, lokalen INI-Dateien und Schlüsseln anlegen. Binaries für ROBUST und Simulatoren gemeinsam auf den freigegebenen SHA abgleichen; GitHub-Publikation installiert **nichts** auf laufenden Servern. Für einen Rollback zur letzten vorherigen Stable-Linie `v0.9.3.12` stets die **passenden** Daten-Snapshots und Schemastände berücksichtigen.
