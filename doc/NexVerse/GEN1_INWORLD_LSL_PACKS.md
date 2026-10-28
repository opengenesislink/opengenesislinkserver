# Generation 1 – Inworld-LSL-Skriptpaket

Alle Skripte befinden sich direkt unter `addons/<AddonName>/` und werden
zusammen mit dem Server-Quellcode versioniert. **Die LSL-Dateien sind Quelltexte
für den OpenSim-/Firestorm-Skripteditor**; sie werden nicht zu .NET-DLLs
kompiliert, nicht automatisch in Inventare geladen und nicht ungefragt in
Bewohnerobjekte eingesetzt. Die vier eigenständigen **.NET8-DLLs** entstehen
weiterhin automatisch bei `bash runprebuild.sh && bash compile.sh`.

## Voraussetzungen

1. **Regionssimulator**, nicht ROBUST, aktualisieren und sicherstellen, dass
   `bin/OpenGenesisLINK.Addons.{NexTraffic,NexTransit,NexSmartLight,OGLWeather}.dll`
   vorhanden ist. Die Dateiklammern sind hier symbolisch gemeint.
2. Die jeweiligen `.ini.example`-Dateien in die tatsächlich geladene
   Simulator-Konfiguration übernehmen. `Enabled=true` nur bei den gewünschten
   Addons setzen. Die **vier LSLChannel-Werte dürfen nicht geändert werden**,
   ohne die Skripte ebenfalls anzupassen. Simulator neu starten.
3. Im Firestorm-Inworld-Skripteditor die gewünschte `.lsl` in das
   passende Objekt kopieren, speichern und aktivieren. Die Skripte setzen auf
   **LSL**, ausgenommen die zwei dynamischen Vektortexturtafeln mit OSSL.
4. Die Module senden **Region-Chat** aus `IWorldComm`. Jeder Listener arbeitet
   in seiner Region, auch wenn mehrere Regionen im selben Simulator laufen.
   Kein globales Chat-Relay und keine Installation von Skripten in ROBUST.

## Skriptübersicht und Aufbau

| Modul | Datei | Einsatz |
| --- | --- | --- |
| NexTraffic | `NexTrafficSignal.lsl` | Linkset aus drei benannten Lampenprims `RED`, `YELLOW`, `GREEN`; je Fahrtrichtung ein Skript in der Root-Prim |
| NexTraffic | `NexTrafficCountdown.lsl` | Eigenständiger Countdown-Prim für Richtung A oder B; aktualisiert Zähler pro Sekunde |
| NexTraffic | `NexTrafficJunctionMonitor.lsl` | Diagnosetafel mit Status beider Achsen und Offlinewarnung |
| NexTransit | `NexTransitDepartureBoard.lsl` | 1024×512-Bildtafel; benötigt `osSetDynamicTextureDataFace` und VectorRender-Freigabe |
| NexTransit | `NexTransitStopInfo.lsl` | Haltestellenschild mit den nächsten drei Abfahrten und privater Touch-Auskunft, nur LSL |
| NexTransit | `NexTransitRouteHeadSign.lsl` | Linien-/Zielanzeige für ein bestehendes Bus- oder Haltestellenobjekt, kein physisch fahrender Bus |
| NexTransit | `NexTransitDepartureAnnouncement.lsl` | Optionale lokale Abfahrtsansage **als Chat-Text**, kein TTS; standardmäßig AUS |
| NexSmartLight | `NexSmartLightLamp.lsl` | Einzelne Straßenlaterne, direkt im leuchtenden Lampenprim |
| NexSmartLight | `NexSmartLightLinkset.lsl` | Gemeinsam geschaltete Lampenprims mit Linknamen `LIGHT_*` in einem Objekt |
| NexSmartLight | `NexSmartLightStatus.lsl` | Diagnose-Anzeige mit ON/OFF, Uhrzeit und Status des Dienstes |
| OGLWeather | `WeatherForecastBoard.lsl` | 1024×512-Wettertafel mit fünf Tagen, benötigt OSSL/VectorRender |
| OGLWeather | `WeatherCurrentSign.lsl` | Kompaktes Wetterschild mit aktueller Temperatur und Wind, reines LSL |
| OGLWeather | `WeatherForecastText.lsl` | Fünf-Tage-Vorschau als Hovertext, reines LSL |

## NexTraffic – Kreuzungsbeispiel

Im `NexTraffic.ini` etwa `Junctions=Hauptstrasse:0` aktivieren.
Zwei gegenüberliegende Ampel-Linksets anlegen:

- Linkset 1: Child-Prims `RED`, `YELLOW`, `GREEN`, in der Root-Prim
  `NexTrafficSignal.lsl` mit `JUNCTION="Hauptstrasse"`, `AXIS="A"`.
- Linkset 2: gleiche Lampennamen, aber `AXIS="B"`.
- Zusätzlich können `NexTrafficCountdown.lsl` und
  `NexTrafficJunctionMonitor.lsl` auf **eigenen Primobjekten** laufen.

Die Ampel erhält die serverseitige Farbe; ohne Daten schaltet sie sichtbar auf
Rot. Das ist eine **Inworld-Anzeige**, keine physische/behördliche
Verkehrssicherung. Eine Fußgängerfreigabe ist **nicht** implementiert; das
kurze serverseitige beidseitige Rot darf nicht als WALK-Phase interpretiert
werden. Es werden keine Autos oder NPCs gesteuert.

## NexTransit – Linien und Haltestellen

Serverbeispiel:
```ini
[NexTransit]
Enabled = true
LSLChannel = -835422
TimeZone = Europe/Berlin
RefreshSeconds = 30
Routes = 101,Magdeburg,10,0,*;150,Jenseits,20,15,Gummybear Island
```

Ein normales Haltestellenprim mit `NexTransitStopInfo.lsl` benötigt **keine
OSSL-Berechtigungen**. Touch liefert die nächsten Planabfahrten als private
Nachricht. Für eine große Anzeigetafel `NexTransitDepartureBoard.lsl`
verwenden; bei Bedarf `DISPLAY_FACE` ändern (in Standard-OpenSim-Boxen
kann die Front je nach Rotation unterschiedlich ausgerichtet sein).

`NexTransitRouteHeadSign.lsl` filtert die Nachricht nach `LINE="101"`.
Wenn diese Linie am Aufenthaltsort des Objekts nicht in den nächsten
Abfahrten vorkommt, zeigt das Skript **Keine Planabfahrt** und **erfindet
kein Ziel**. Das ist **keine Fahrzeugsteuerung**.

`NexTransitDepartureAnnouncement.lsl` enthält den Schalter
`ANNOUNCE_IN_CHAT = FALSE`: für Textansagen auf eigene Verantwortung
aktivieren. Das Skript ruft bei einer bevorstehenden Planabfahrt `llSay(0)`
auf (nur lokaler Hörkreis), **keine TTS-Engine, keine vorhandenen WAV-Dateien
vorausgesetzt**; der zeitliche Abstand zwischen Chatansagen ist begrenzt.

## NexSmartLight – Einzelprim oder Linkset

Beim Einzelprim-Skript dürfen sich mehrere andere Scripte am gleichen
Leuchtenprim **nicht ebenfalls gegenseitig überschreibende**
`PRIM_POINT_LIGHT`-Befehle senden. Das Linkset-Skript gehört **nur in
ein** Root-Prim und schaltet ausschließlich alle Child-/Root-Prims, deren
**Linkname mit `LIGHT_` beginnt**, also zum Beispiel `LIGHT_VORNE`,
`LIGHT_HINTEN`. So bleiben Straßenmast, Schilder und andere Teile unberührt.
Die Sonne-/Uhrzeit-Fallback-Option ist standardmäßig AUS, da die vom
Server veröffentlichte Lokalzeit (Europe/Berlin) nicht automatisch dem
regionalen EEP-Himmel entspricht.

## OGLWeather – ohne manuelle Partikelquellen

Für das **Regnen und Schneien** werden **keine LSL-Skripte** benötigt.
Die kompatiblen Legacy-Emitter werden bereits vom C#-Addon temporär und
automatisch während Regen/Schnee erzeugt und gelöscht. Die drei Wetter-LSLs
sind ausschließlich **optionale Wetteranzeigen**. Die beiden einfachen
Hovertext-Varianten benötigen keine hochprivilegierten OSSL-Funktionen.

Die Temperaturen und fünf Tage sind **simulierte deterministische
Wettervorhersagen**, keine Live-DWD-Wetterwerte. Standard-Firestorm zeigt
Regentropfen/Schnee über serververwaltete Partikel-Emitter, der native
OGL-Viewer-Renderer bleibt ein separates Entwicklungsziel.

## Grenzen und Abnahme

Alle Skripte prüfen die bisherige Server-Wire-Version, den jeweiligen
negativen Chatkanal, den exakten Absendernamen und `NULL_KEY`. Der
Kanal ist **kein kryptographischer Identitätsnachweis**, und diese Skripte
dürfen nicht als Sicherheits-, Zahlungs- oder Zugangskontrollsystem
verwendet werden.

Die automatischen CI-Tests prüfen Syntaxstruktur, beabsichtigte
LSL-Funktionsnamen, gemeinsam verwendete Kanäle und Protokollkennungen.
**Ein echter Inworld-LSL-Compilerlauf und Firestorm-Abnahmetests sind
vor einem Produktivrollout noch notwendig.** Insbesondere benötigt
VectorRender die richtige OSSL-Funktionsfreigabe des Script-Owners.
