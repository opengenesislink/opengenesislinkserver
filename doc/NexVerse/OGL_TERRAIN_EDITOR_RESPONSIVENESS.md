# OpenGenesisLINK G1 – Terrain-Editor im Viewer

Die Inworld-Landbearbeitung orientiert sich an den bekannten Second-Life-Werkzeugen
Raise, Lower, Flatten, Smooth, Roughen und Revert. Es werden **keine** Second-Life-internen
Leistungskennlinien behauptet; diese sind nicht öffentlich als verbindliche Sollwerte dokumentiert.

## Technische Änderungen

- Der bisherige **globale** 20-ms-Blocker von TerrainModule ließ konkurrierende
  Terrain-Pakete verschiedener Avatare fallen. Jetzt wird die Heightmap unter
  einem gemeinsamen Lock seriell bearbeitet; der 20-ms-Cooldown gilt **pro Avatar**.
- Auf Abmeldung wird der zuständige Avatar-Eintrag entfernt.
- Die vom Viewer übermittelte Paint-Stärke wird für Einzelpinsel standardmäßig
  verdoppelt, wobei 1.0 die bisherige OpenSim-Wirkung wiederherstellt.
  Bestehende Flood-/Auswahloperationen bleiben in ihrer Stärke unverändert.
- Die Patch-Sendeperiode sinkt von 50 auf 15 Frames. Bei einem
  tatsächlich laufenden 50-FPS-Regionstakt entspricht dies etwa
  3,3 statt 1 Update pro Sekunde; der reale Wert hängt von der Region ab.
- Ungültige Float-Werte und übergroße Brush-Werte werden begrenzt.
  Die Parzellenberechtigungen, Estate-Höhenlimits, Terrain-Speicherung
  und physikalische Neuberechnung bleiben unverändert.

## Konfiguration

```ini
[Startup]
    UpdateTerrainEveryNFrames = 15

[Terrain]
    OglTerrainBrushStrengthMultiplier = 2.0
```

Der Faktor wird serverseitig auf 1.0–4.0 begrenzt.
Mit `1.0` erhält man die klassische Brush-Stärke. Bei sehr
großen oder hochbelasteten Var-Regionen kann die Patch-Periode
auf 50 erhöht werden; Terrain-Persistenz und Physik aktualisieren
weiterhin in ihren bestehenden Zyklen.

## Abnahme vor Produktion

1. Zwei Avatare auf verschiedenen, bearbeitbaren Parzellen
   gleichzeitig Raise/Lower und Smooth verwenden lassen. Beide
   Änderungen müssen nach dem üblichen Patch-Intervall erscheinen.
2. Größe und Stärke im Viewer variieren und mehrere Sekunden
   bei gedrückter Maustaste malen. Höhenänderung soll sichtbar schneller
   erfolgen, ohne Berechtigungen zu umgehen.
3. Rechtefreier Avatar darf keine fremden Parzellen terraformen.
4. Revert, Flatten, Bereichsauswahl, Estate-Limits und
   Gelände-Restartpersistenz überprüfen.
5. Eine 512×512-Var-Region unter Last testen: Region-FPS, Terrainpakete,
   CPU-Last und physikalische Terrain-Kollisionen beobachten.
