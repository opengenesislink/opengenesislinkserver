# NexSmartLight 0.1.0 – automatische Stadtbeleuchtung

## Automatischer Standard-Build (Generation 1)

Dieses Addon wird zusammen mit OpenGenesisLINK automatisch über `./runprebuild.sh` und `dotnet build -c Release OpenSim.sln` erstellt. Die DLL `OpenGenesisLINK.Addons.NexSmartLight.dll` wird dabei automatisch in `bin/` abgelegt, **ohne einen separaten Addon-Build oder Installationsschritt**. Das funktioniert auch über die automatisch erzeugte `compile.sh`. Die Build-Integration installiert keine Dateien auf fremden Servern und aktiviert das Addon nicht automatisch; `Enabled = false` bleibt die sichere Vorgabe.

Die vorhandenen `addon-modules/.../build.sh`-Skripte bleiben ausschließlich für gezielte Einzel-Builds und separate Simulatorinstallationen bestehen.


**Eigenständiges opt-in OpenGenesisLINK-Addon.** Der Simulator sendet den gewünschten Lampenstatus nach lokaler Uhrzeit an alle geladenen Regionen. Der Zustand wird mit Deutschland-Zeitzone `Europe/Berlin` einschließlich Sommerzeit berechnet. Er greift **nicht** in EEP, Estate, Terrain oder bestehende Straßenlaternen ein.

## Einbau

```bash
bash addon-modules/NexSmartLight/build.sh Release
bash addon-modules/NexSmartLight/build.sh Release --sim-bin /pfad/zum/simulator/bin
```

Beispiel-INI im Simulator aktivieren (Standard ist deaktiviert):

```ini
[NexSmartLight]
Enabled = true
LSLChannel = -835423
RefreshSeconds = 60
TimeZone = Europe/Berlin
NightStarts = 18:00
NightEnds = 06:00
```

Ein/Aus wird mindestens jede Minute auf dem konfigurierten Kanal als `NEXLIGHT|1|ON|YYYY-MM-DD HH:mm` bzw. `...|OFF|...` gesendet. Zeiten werden über Mitternacht hinweg korrekt verarbeitet (`18:00` bis `06:00`); identische Anfangs-/Endzeit ergibt sicher OFF. Die ausgewählte Uhrzeit ist **die reale lokale Zeit**, nicht zwangsläufig der visuelle EEP-Sonnenstand einer individuell eingestellten Region.

`NexSmartLightLamp.lsl` kommt direkt in den leuchtenden Prim der Straßenlaterne. Es schaltet `PRIM_POINT_LIGHT`, Fullbright und Glow, ohne Texturen, Linksetpositionen oder Objekt-Inventar zu ändern. Das Skript verwendet einen 120-Sekunden-Watchdog, **keinen 30-Sekunden-Poller**. Fällt der Serverdienst aus, geht die Lampe nach spätestens fünf Minuten ohne Daten aus; optional `USE_OS_SUN_FALLBACK = TRUE` für eine lokale `llGetSunDirection().z`-Prüfung bei Dienst-Ausfall. Regionale EEP-Tageszyklen können dann von der konfigurierten Civil-Time-Schedule abweichen.

Status: CI-/Build-Tests, visuelle Firestorm-Abnahme offen. Keine automatische Rez-Logik oder Änderung bestehender Objekte.
