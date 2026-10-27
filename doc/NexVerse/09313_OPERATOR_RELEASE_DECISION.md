# OpenGenesisLINK 0.9.3.13 — Betreiberentscheidung zur offiziellen Stable-Freigabe

**Datum:** 27. Oktober 2026  
**Status:** Offizielle Betreiberfreigabe für Stable `v0.9.3.13`, mit dokumentierter Abweichung vom ursprünglichen Einzelbeleg-Verfahren.  
**Betreibererklärung:** Im Chat bestätigte der Betreiber ausdrücklich, dass **alle Abnahmen OK** sind und verlangte unmissverständlich die offizielle Stable-Veröffentlichung.

## Technisch belegter Stand und Freigabeentscheidung

- Der letzte Dev-Ausgangscommit `14252fb2fc1ae04fee88fcf42984a4ad4784ce26` wurde von der NexVerse-Gesamt-CI einschließlich NV$-Datenbankmatrix mit SUCCESS geprüft. Die finale Stable-Kennung benötigt erneut CI auf dem **eigenen** Commit.
- Die Betreiber-Rückmeldung nach dem LLUDP-`UseCircuitCode`-Fix war positiv: keine erneuten ursprünglichen `Scene.AddNewAgent`-Fehler im gemeldeten Betriebsfenster.
- Der Betreiber erklärt die Abnahme für abgeschlossen und ordnet die Stable-Publikation ausdrücklich an.

**Transparente Nachweisausnahme:** Die bestehende 30-Gates-Matrix `doc/NexVerse/09313_RELEASE_ACCEPTANCE.md` (C01–C05, A01–A09, M01–M07, V01–V04, R01–R05) enthält weiterhin `NOT_RUN`-Markierungen und keine vollständigen einzeln überprüfbaren Live-Belegreferenzen. Das vorliegende GitHub-Projekt kann eine 30/30 unabhängig geprüfte Live-Evidence deshalb nicht belegen. Diese Release-Entscheidung **ersetzt die formale Dokumentationsbedingung durch die ausdrückliche Betreiber-Risikoentscheidung**; sie erklärt fehlende Nachweise **nicht** rückwirkend zu dokumentierten Tests. Der Validator bleibt fail-closed und wird nicht mit erfundenen Angaben umgangen.

Die offenen formalen Einzelbelege können nachgereicht werden, sind aber auf ausdrückliche Betreiberanweisung **keine Veröffentlichungssperre** mehr. Der Betreiber trägt die Entscheidung, mit den hier dokumentierten Restunsicherheiten zu veröffentlichen.

## Unverzichtbare technische Veröffentlichungsbedingungen

1. Der Stable-Quellstand meldet zur Laufzeit `OpenGenesisLINK 0.9.3.13` ohne Dev-Suffix (`Flavour.Release`).
2. Der finale Stable-Commit wird mit der vollständigen NexVerse-CI auf **demselben SHA** validiert, inklusive Build und Datenbankmatrix.
3. Der Publisher arbeitet ausschließlich nach erfolgreichem `push`/`main`-CI, kontrolliert den unveränderten Commit, erstellt den eindeutigen `v0.9.3.13`-Tag und GitHub Stable Release ohne `--prerelease`.
4. Bestehende Tags/Releases werden niemals überschrieben oder force-gepusht.
5. Der historische Dev-Tag `v0.9.3.13-dev` bleibt bestehen und ist unabhängig.

## Grenzen des ausgelieferten Umfangs

- **OGL AssetService** mit Opt-in-Cache-/Parallel-Store-/Migrationscode: FSAssets und SQL bleiben bis zu einer separat getesteten Migration autoritativ. Die Stable-Publikation startet **keinen** automatischen Cutover, keine Lösch-/GC-Automatik und keine Änderung an produktiven Daten.
- **Mesh/PBR/glTF-Materialpfade:** Die enthaltenen Parser-/Materialkorrekturen sind freigegeben; keine Behauptung eines vollständigen nativen glTF-Mesh-Importers oder separat validierter S3-/CDN-/Terrain-Reflection-Pipeline.
- **Extended Region Visibility:** Im konkreten `bin/OpenSim.ini`-Gridprofil opt-in aktiv; generische Defaults bleiben deaktiviert. Erweiterte Sichtweite kann zusätzliche Child-Agents/Netzwerk- und Renderlast erzeugen.
- **FastMap Async Startup:** Nur im konkreten Gridprofil aktiv; standardmäßige Example-/Defaults bleiben konservativ.
- **NodeID/Secrets:** Die jeweilige Simulatorinstanz benötigt `NEXVERSE_NODE_ID`; lokale INI- und Secret-Dateien sind vor einem Update zu erhalten.
- **LLUDP:** Ein einzelner zeitgleicher Watchdog-Timeout auf einem vServer ist keine unabhängig ermittelte Fehlerursache; der dokumentierte konkurrierende `UseCircuitCode`-Handshake ist gesondert gefixt.
- **NexExperiencesViewer/Viewer-2.0/OGLSeasons-Redesign** sind nicht mit dieser Stable-Freigabe aktiviert.

## Deployment und Rückfall

GitHub-Publikation aktualisiert keinen laufenden Server automatisch. Vor Deployment vollständige, zusammenpassende Backups von Datenbank, FSAssets, Region-/Inventarbestand, lokalen Konfigurationen und Signatur-/NexBus-Schlüsseln anfertigen. ROBUST und Simulatoren im kompatiblen Buildverbund aktualisieren. Für den Rückfall auf `v0.9.3.12` passende Snapshots und etwaige Schema-/Migrationseinflüsse prüfen.

**Referenzen:** `doc/NexVerse/RELEASE_0.9.3.13.md`, `doc/NexVerse/09313_RELEASE_ACCEPTANCE.md`, `doc/NexVerse/09313_SCOPE_AND_BLOCKERS.md`.
