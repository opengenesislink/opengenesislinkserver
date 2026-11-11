# OpenGenesisLINK Generation 1 – Daten/SL/Performance/Architektur Abschluss

Stand 2026-11-11. Ziel: ein reproduzierbarer Integrationsstand statt
vier konkurrierender Aenderungszweige. Betrifft die technischen
Arbeitspakete 2, 3, 4 und 6 des Codeaudits.

## 2 – Datenintegritaet

- PostgreSQL #414: Estate-Bans und UUID-Listen atomar; Profil-Upserts
  und Default-Eintraege gegen unabsichtliche Ueberschreibung abgesichert.
- PBR/SQL #425: glTF-Overrides vor Objektmutation validiert; zwei
  bisher leere Gruppen-Mitgliedschaftsabfragen mit MySQL und
  PostgreSQL realisiert; keine Schemaaenderung.
- Vor Produktionsmigration: getrennte DB-/Asset-Backups, Restore
  auf Staging, Row-Count-/Hash-Vergleich, eindeutige Migrations-ID,
  Wiederanlauf- und Rollback-Plan. Weder das Tool noch CI schreibt
  automatisch produktive Assets oder Benutzerdaten.
- Password-Recovery, OGL AssetService / FSAssets-Cutover werden
  **nicht** als produktiv abgenommen bezeichnet, solange die
  realen SMTP-, NAS/WebDAV-, Snapshot- und Restore-Nachweise fehlen.

## 3 – Second-Life- und Firestorm-Kompatibilitaet

- `llSetRenderMaterial`, `llSetLinkRenderMaterial` und
  `llGetRenderMaterial` sowie die Listenregel
  `PRIM_RENDER_MATERIAL` setzen/lesen PBR-Face-Materialien.
- UUID-/Inventarname, `ALL_SIDES`, `NULL_KEY`, Linksets und
  getrennte Faces mit Copy-on-Write. UV-Transformationen bleiben
  erhalten.
- Konformitaet wird durch `OglRenderMaterialLslRegression` und
  `OglPbrMaterialRegression` automatisiert, jedoch ohne echten
  Viewer. Reale Firestorm-/TrixieStorm-Abnahme:
    1. Upload mit glTF-Material, Rez, Link-Child, Face/ALL_SIDES.
    2. Besitzer und Inventarberechtigung, NULL_KEY, invalides JSON.
    3. Viewer-Cache Refresh, Regionwechsel, Logout/Relog, OAR/Reload.
    4. Performance-/Objektbestand vor/nach Materialwechsel vergleichen.
- Vollstaendiger GLB-/glTF-Modellimport und NavMesh sind weiterhin
  eigenstaendige neue Features, keine regressionsfreien Kleinf fixes.

## 4 – Warp3D / Speicher / CPU

- Rendercalls pro Region-Modul serialisiert, damit Kamera- und
  Textur-Caches nicht parallel ueberschrieben werden.
- Native Warp3D-Szenen und Caches werden bei Exceptions freigegeben.
- `[Map]` `Warp3DForceFullGC=false` und
  `Warp3DWriteMapPng=false` sind bewusste OPT-IN-Schalter. Die
  Kompatibilitaets-Defaults sind `true`.
- Vor Umschalten je Simulator OGLPerformance-Baseline erfassen:
  95./99. Perzentil Frames, GC-Pausen, RSS/Working Set, CPU,
  Bildfehler, Kartenzeit und Mesh-/Assetwarnungen.
- Kein pauschales Abschalten des GC bei Regionsstart/OAR-Export,
  bis Benchmarks die entsprechenden Speicherlebenszeiten belegen.

## 6 – Architektur

- NodeAgent-Telemetriesammlung ist vom NexBus-Transport und dem
  Live-Updater getrennt, vorhandene signierte Heartbeatdaten bleiben.
- Fast 48 KiB Control-Center-CSS liegen in eigener C#-Ressourcenklasse.
  Das geschuetzte HTML und JS werden weiterhin mit gleichen
  IDs/CSP/Authentifizierungsgrenzen ausgeliefert.
- Eine vollstaendige JS- und WorldAPI-Routenaufteilung erfordert
  das gemeinsame Migrieren zahlreicher CI-Quellvertraege. Das
  bleibt ein eigener, nicht blockierender Refactoring-Meilenstein.
  Weder das DOM noch Berechtigungs-/Updater-Routing sind in diesem
  Integrationspaket absichtlich neu gestaltet.

## Technische Abnahmegates

```bash
python3 tools/ci/verify_g1_closeout.py
python3 tools/ci/verify_ogl_warp3d_lifecycle.py
python3 tools/ci/verify_ogl_node_performance_refactor.py
python3 tools/ci/verify_ogl_performance_sources.py
python3 tools/ci/verify_ogl_api_adminlte_viewers.py
dotnet run -c Release --project tools/ci/OglPbrMaterialRegression/OglPbrMaterialRegression.csproj
dotnet run -c Release --project tools/ci/OglRenderMaterialLslRegression/OglRenderMaterialLslRegression.csproj
```

Zusaetzlich alle bestehenden Linux-/Windows-x64-/ARM64-/NexVerse-CI-
und PostgreSQL-Regressionen. Bei `apt`-DNS-Problemen exakte
Fehlerursache aus dem Joblog dokumentieren, nicht als erfolgreichen
Inworld-/Datenbanktest deklarieren.

Rollout: auf Test-Node, Backup-Snapshot, mit bestehenden
INI-Einstellungen starten, Viewer- und Integritaetspruefung
protokollieren, anschliessend nach Freigabe auf produktive Nodes.
Kein automatischer Live-Update-Neustart.
