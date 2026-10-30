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

Ein echtes `MEZ/MESZ`-Label mit Berliner Uhrzeit oben im Viewer
muesste im Viewer selbst (z. B. OGL-Viewer-Fork) integriert werden.
Das Serverflag darf nicht als Beleg fuer eine solche Funktion
missverstanden werden.

## Aktivierung auf der produktiven Installation

1. Auf dem ROBUST-Host die **tatsaechlich geladene**
   `bin/Robust.HG.ini` pruefen. Sie kann lokale Aenderungen
   enthalten, die bei einem `git pull` nicht ueberschrieben
   werden duerfen.
2. Im Abschnitt `[LoginService]` die Zeile oben setzen.
3. **Nur ROBUST** neu starten. Regionen muessen dafuer nicht
   pauschal neu gestartet werden.
4. Firestorm neu anmelden, da `daylight_savings` nur beim
   Login uebermittelt wird. Viewer-Anzeige als Pacific Time
   behandeln, nicht als Berliner Uhrzeit.

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
