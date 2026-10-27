# OpenGenesisLINK 0.9.3.13 RC1 — historischer Abnahmeplan

> **Aktualisierung vom 27.10.2026:** OpenGenesisLINK 0.9.3.13 wurde auf ausdrückliche Betreiberentscheidung als Stable freigegeben. Die frühere formale 30-Gates-Evidence-Sperre wurde durch eine dokumentierte Betreiber-Ausnahmeentscheidung übersteuert; fehlende Einzelbelege werden nicht als verifiziert dargestellt. Siehe [Stable-Freigabeentscheidung](09313_OPERATOR_RELEASE_DECISION.md) und [Release Notes](RELEASE_0.9.3.13.md). Dieser Abschnitt bleibt als historischer Plan erhalten.

> **Statusaktualisierung 27.10.2026:** Auf ausdrücklichen Betreiberwunsch wurde der
> bisherige RC1-Code für die interaktive Testphase wieder auf
> **OpenGenesisLINK 0.9.3.13 Dev** gesetzt. Dieses Dokument bleibt als
> historischer RC1-Vorbereitungsplan erhalten und belegt **keinen**
> aktuell aktiven RC1. Für eine spätere Stable-Freigabe sind ein neuer
> eingefrorener RC-Commit, erneute CI und alle 30 dokumentierten
> Live-Gates erforderlich. Die jetzige Dev-Abnahme dient der
> Fehlerfindung und Vorbereitung, nicht der endgültigen Stable-Freigabe.

Stand 27.10.2026. Dieses Dokument **bereitet** die Abnahme vor;
es bestätigt keine erfolgreichen Betreiber-, Firestorm- oder Storage-Tests.
Quellquelle: `main` zum Zeitpunkt der RC1-Zusammenführung; der
**maßgebliche SHA ist stets der tatsächliche gemergte Commit** und muss
im interaktiven Prüfprotokoll und den Logbelegen dokumentiert werden.

## Release-Scope: verbindlich

- **17.1 OGL AssetService:** FSAssets bleibt autoritativ. Parallel-Store,
  OGL-first, SQLite-Index, Cache und Offline-Migration im Code vorhanden,
  aber niemals ohne isolierten, konsistenten SQL-/FSAssets-/Spool-Restore
  produktiv aktivieren. Es existiert kein freigegebener automatischer
  Cutover und keine Freigabe zur Löschung der Legacy-Assets.
- **17.2 Mesh / PBR / Materialpipeline:** Fixes und Regressionen im
  Quellcode; echter Firestorm-Upload, LOD/Physik, Materialroundtrip,
  Rechte und Relog müssen auf dem RC getestet werden.
- **17.3 Extended Region Visibility:** Opt-in
  `[Startup] OGLExtendedRegionVisibility=true` für 768m
  Regionssicht / 1024m maximale Darstellungsdistanz. Die Basiswerte
  bleiben unverändert. >9 Regionen als Terrain **und** Prims, Grenz-,
  VAR-/HG- und Lasttests sind Pflicht.
- **OGLSeasons:** eingefrorenes Alt-Addon; Neuaufbau erst nach Stable.
- NexMarketplace/NexMedia, OGL Profile Feed #316, Password-Recovery
  Public R509 #309, nativer Viewer/.NET10 sind **kein** notwendiger
  Bestandteil der 0.9.3.13-Stable-Freigabe. Aufnahmen sind separat zu entscheiden.

## GitHub-Stand und nicht blind zu mergende PRs

Vor Freeze Review auf `main` gegen aktuelle PR-/CI-Daten ausführen:
- #238 historischer, ungetesteter ZLib-Draft; #293 ist bereits integriert.
- #316 Profilfeed-Draft: Konto-/Datenlöschung und Firestorm-Sicherheit offen.
- #309 öffentlicher Recovery-Flow: Staging, noch nicht sicher freigegeben.
- #220 .NET10, #187 eigener Viewer, #189 Registrierung alt: nicht im 0.9.3.13-Scope.
- #288 Instanzübergreifende API-Key-Scope-Aktualität: eigenständiger
  Security-Fix, nur nach neuem Review und Integrations-CI mergen.
- #126 Thumbnail-Persistenz: bekannter Alt-PR, ggf. eigener
  Inventar-/Firestorm-Regressionsblock; nicht stillschweigend als gelöst markieren.
- #239 Startposition und #280 Portal-only-Doku: separater Scopeentscheid.
Kein offener PR wird durch diesen RC-Vorbereitungsschritt automatisch gemergt.

## Pflichtevidenz

Maßgeblich ist `doc/NexVerse/09313_RELEASE_ACCEPTANCE.md`:
**30 Pflichtprüfungen** C01–C05, A01–A09, M01–M07, V01–V04,
R01–R05. Sie stehen bis zur Prüfung weiter auf NOT_RUN.

Benötigte Angaben für **jedes** Gate: Tester, SHA, Versionen, Umgebung,
Testanleitung, tatsächliches Ergebnis, anonymisierte überprüfbare
Belegreferenz, ggf. Messwerte. Ein FAIL/PARTIAL/NOT_RUN verhindert Stable.

Die browserbasierte Abnahme stellt einen `ogl-09313-1`-JSON-Export
bereit. Der strikt auf die RC-SHA gebundene Validator:

```bash
python3 tools/release/ogl_09313_acceptance_gate.py --self-test
python3 tools/release/ogl_09313_acceptance_gate.py \
  --json operator-acceptance-09313.json --sha <40-ZEICHEN-RC-SHA>
```

Das zweite Kommando ist nur eine **formale** Struktur-/Statusprüfung.
Es ersetzt weder externe Belegprüfung noch Betreiberzustimmung.

## Freigabeprozess (nach tatsächlicher Abnahme)

1. Release-Zweig / Git-SHA festlegen; vollständige CI und alle
   verpflichtenden AssetService-Workflows auf demselben Commit verifizieren.
2. Isoliertes Snapshot-/Restore-/Migrationslabor; keine produktiven
   Backend-Pfade für destruktive Testoperationen verwenden.
3. Firestorm-, Multi-Sim-/HG-, glTF-/PBR-, Regionen- und Lasttests
   durch den Betreiber; Evidence und Rollback dokumentieren.
4. 30/30 PASS + Belege im JSON, externe Echtheitsprüfung, ausdrücklicher
   operatorischer Freigabeentscheid.
5. **Separater Stable-PR**: einzig notwendige Änderungen an
   `VersionInfo.cs` (`Flavour.Release`), Dokumentation und
   freigegebenem Belegmanifest. Relevante Codeänderungen erzwingen einen
   neuen vollständigen Abnahmedurchlauf.
6. Release-Build und CI auf dem tatsächlichen Stable-Commit,
   kein neuer Fehler. Erst danach unveränderlichen Tag
   `v0.9.3.13` und GitHub Release mit Freigabeprotokoll erzeugen.
7. Dokumentierten Rückfall auf vorherige Binärdateien und FSAssets
   bereithalten. Betreiber kann Veröffentlichung trotz grünem CI
   jederzeit ablehnen.

**Nicht jetzt:** Kein `v0.9.3.13`-Tag und keine GitHub-Stable-Veröffentlichung,
solange Freigabegates offen oder Belege ungeprüft sind.
