# NexTransit 0.1.0 – Stadt-NexVerse Linien und Fahrplananzeigen

## Automatischer Standard-Build (Generation 1)

Dieses Addon wird zusammen mit OpenGenesisLINK automatisch über `./runprebuild.sh` und `dotnet build -c Release OpenSim.sln` erstellt. Die DLL `OpenGenesisLINK.Addons.NexTransit.dll` wird dabei automatisch in `bin/` abgelegt, **ohne einen separaten Addon-Build oder Installationsschritt**. Das funktioniert auch über die automatisch erzeugte `compile.sh`. Die Build-Integration installiert keine Dateien auf fremden Servern und aktiviert das Addon nicht automatisch; `Enabled = false` bleibt die sichere Vorgabe.

Die vorhandenen `addon-modules/.../build.sh`-Skripte bleiben ausschließlich für gezielte Einzel-Builds und separate Simulatorinstallationen bestehen.


**Eigenständiges opt-in Regionsaddon.** Konfigurierte Linien, Ziele, Taktabstände und Startminuten ergeben reproduzierbare Planabfahrten für eine bestimmte Region/Haltestellenumgebung. Das Wetter- und das alte Bus-Abfahrtstafel-Addon werden nicht verändert. Ein automatischer Bus, Echtzeit-GPS, Fahrscheinverkauf oder Teleport wird **nicht** vorgetäuscht.

## Einbau

```bash
bash addon-modules/NexTransit/build.sh Release
bash addon-modules/NexTransit/build.sh Release --sim-bin /pfad/zum/simulator/bin
```

Nur im Simulator; in `addons/NexTransit/NexTransit.ini.example` einstellen und `Enabled=true` wählen. `TimeZone=Europe/Berlin` verarbeitet die Sommerzeit automatisch. Eine Route ist `Linie,Ziel,TaktMinuten,Startminute,Haltestellenregion`. Die erste tägliche Abfahrt ist die Startminute nach 00:00; Folgelinien laufen bis zum Tagesende. Danach beginnt der nächste lokale Kalendertag.

```ini
[NexTransit]
Enabled = true
LSLChannel = -835422
RefreshSeconds = 30
TimeZone = Europe/Berlin
MaxDepartures = 5
Routes = 101,Magdeburg,10,0,*;150,Jenseits,20,15,Gummybear Island
```

`*` bedeutet alle Regionen; ein exakter Regionname begrenzt die Anzeige auf diese Region. Höchstens 100 Routen pro Prozess, 10 Abfahrten pro Nachricht. Die an eine Region gesendete Nachricht besteht aus `NEXTRANSIT|1|YYYY-MM-DD HH:mm|line,destination,HH:mm,minutes;...`. Sie wird als Ganzes gesendet und in die 1024×512-OSSL-Textur der `NexTransitDepartureBoard.lsl` eingetragen; keine unnötigen Zwischenreloads pro Zeile.

Die Tafel kann als Prim in beliebigen Regionen eingesetzt werden, `DISPLAY_FACE = 3` ist Standard für eine normale Box. `osSetDynamicTextureDataFace` benötigt Serverrechte und VectorRender-Unterstützung. Keine 100-%-Flackerfreiheit im unveränderten Firestorm garantiert. Im normalen Betrieb berechnen mehrere Simulatorprozesse bei identischem Fahrplan und Zeitzone dieselben Abfahrten.

Status: CI-/Build-Tests, keine echten Fahrzeug- oder Haltestellenkoordinaten integriert.
