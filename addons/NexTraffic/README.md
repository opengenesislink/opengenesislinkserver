# NexTraffic 0.1.0 – simulatorweite Ampelsteuerung

**Eigenständiges, opt-in OpenGenesisLINK Generation-1-Addon.** Der Server berechnet auf Basis der UTC-Unixzeit und eines einheitlichen Takts synchronisierte Ampelphasen und sendet sie als Region-Chat an freiwillig platzierte Ampelobjekte. Das verhindert widersprüchliche Ampelphasen, sofern beide Seiten derselben Kreuzung den gleichen Serverkanal nutzen. Ein externer Verkehrsleitrechner, echte Fahrzeuge und physische Kollisionsvermeidung sind nicht Bestandteil dieses MVP.

## Einbau

```bash
bash addon-modules/NexTraffic/build.sh Release
bash addon-modules/NexTraffic/build.sh Release --sim-bin /pfad/zum/simulator/bin
```

Nur auf SIMULATOR, nicht ROBUST installieren. Beispiel-INI: `addons/NexTraffic/NexTraffic.ini.example`. Nach explizitem Setzen von `Enabled = true` und Simulatorneustart werden die Daten in allen geladenen Regionen mit gleichem `Junctions`-Satz bereitgestellt.

```ini
[NexTraffic]
Enabled = true
LSLChannel = -835421
RefreshSeconds = 5
GreenSeconds = 30
YellowSeconds = 5
AllRedSeconds = 3
Junctions = Hauptstrasse:0;Marktplatz:11
```

**Ampelbau:** Pro Richtung ein Linkset mit drei Lampen-Prims (Linknamen `RED`, `YELLOW`, `GREEN`) erstellen; `NexTrafficSignal.lsl` ins Root legen; `JUNCTION` auf den passenden Namen und `AXIS` auf `"A"` bzw. `"B"` setzen. Nicht benötigte Link-Prims im Skript unverändert lassen. Ohne 20 Sekunden Statusupdates schaltet die Skript-Ampel auf Rot (Fail-safe für **die Anzeige**, keine Verkehrs- oder physischen Sicherheitsgarantien).

Wire: `NEXTRAFFIC|1|JunctionName|A_STATE|B_STATE|remainingSeconds|unixTime`. Getrennte A/B-Phasen mit Gelb und beidseitigem Rot; Ampeln derselben Junction sind auch über Regionen hinweg zeitlich konsistent. Negative Chatkanäle sind **kein sicherer Authentisierungskanal**; niemand darf diese Darstellung als technische Verkehrs-Sicherheitseinrichtung einsetzen.

Status: CI-/Build-Tests, kein Live-Firestorm-Abnahmetest. Keine automatisch rezzten Ampeln, keine Bewegungs- oder Straßenobjektänderungen.
