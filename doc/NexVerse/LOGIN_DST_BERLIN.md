# Stadt-NexVerse – Login-DST-Zone Europe/Berlin

Die OpenGenesisLINK-Login-Konfiguration fuer die Stadt-NexVerse-Grid-
Installation verwendet deutsche Sommer-/Winterzeitregeln.

```ini
[LoginService]
    DSTZone = "Europe/Berlin;W. Europe Standard Time"
```

- `Europe/Berlin` ist der IANA-Zeitzonenname fuer Linux/.NET.
- `W. Europe Standard Time` ist der passende Windows-Zeitzonenname.
- Winter: **MEZ (UTC+1)**. Sommer: **MESZ (UTC+2)**.
- Beide Namen werden von `LLLoginResponse` der Reihe nach versucht.
  Der Login-Service berechnet damit das boolesche
  `daylight_savings`-Kennzeichen fuer einen **neuen Login**.

## Wichtige Grenze fuer Firestorm

Ein normaler Firestorm-Viewer bekommt ueber diese Loginantwort
**keine** vollstaendige Zeitzonenkennung und keinen UTC-Offset
fuer Berlin. `DSTZone` aendert deshalb nicht die Viewer-Uhr
von PST/PDT in MEZ/MESZ. Die Anzeige kann beim Wechsel auf
europaeische Sommerzeit (vor oder nach dem US-Wechsel) eine Stunde
von der gewohnten Pacific-Time-Darstellung abweichen.

Das **neue TrixieStorm-Viewer-Feature** nutzt darum *nicht* das
einzelne `daylight_savings`-Flag: der OGL-Discovery-Endpunkt
`/ogl/v1/grid-info` gibt zusaetzlich die aktuell geladene
`[LoginService] DSTZone`, den wirksamen UTC-Offset und die UTC-Serverzeit
im optionalen `clock`-Objekt bekannt. TrixieStorm verwendet die
konfigurierte Zone fuer die Statusleisten-Uhr. Im Sommer wird
`MESZ` und im Winter `MEZ` ausgegeben. Alle anderen, nicht angepassten
Firestorm-Viewer zeigen weiterhin Pacific Time.
Siehe [OGL_VIEWER_CLOCK.md](OGL_VIEWER_CLOCK.md).

**Hinweis:** Eine Aenderung an der lokalen `Robust.HG.ini` erfordert
weiterhin einen ROBUST-Neustart. Die Viewer-Aktualisierung folgt dann
ohne eigenen Neubau. Die neue Discovery-Erweiterung muss vorher
auf dem aktiven ROBUST-Server bereitgestellt worden sein.

## Aktivierung auf der produktiven Installation

1. Auf dem ROBUST-Host die **tatsaechlich geladene**
   `bin/Robust.HG.ini` pruefen. Sie kann lokale Aenderungen
   enthalten, die bei einem `git pull` nicht ueberschrieben
   werden duerfen.
2. Im Abschnitt `[LoginService]` die Zeile oben setzen.
3. **Nur ROBUST** neu starten. Regionen muessen dafuer nicht
   pauschal neu gestartet werden.
4. Klassische Firestorm-Viewer neu anmelden, da `daylight_savings`
   weiterhin nur beim Login uebermittelt wird und ihre eigene PST-Uhr
   nicht geaendert wird. TrixieStorm uebernimmt die neu bereitgestellte
   `clock`-Antwort aus der OGL-Discovery und aktualisiert sie
   laufend; das erste Abrufen braucht eine erreichbare Discovery.

Die Systemuhr muss NICHT auf Berliner Zeitzone gestellt werden,
damit `DSTZone` richtig funktioniert. Die bisherigen OGLWeather-,
NexTransit-, NexSmartLight- und OGLEnergy-Konfigurationen mit
`TimeZone=Europe/Berlin` sind unabhaengig und bleiben unveraendert.

## CI

```bash
python3 tools/ci/verify_nexverse_berlin_dst.py
```

Das prueft die aktiven Login-Konfigurationen sowie CET/MESZ
im Winter und Sommer. Es ersetzt keinen manuellen Firestorm-Test.
