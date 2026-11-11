# OGLPerformance: Warp3D Map / GC / Reentrancy

Drei voneinander unabhaengige und **ausdruecklich abschaltbare** Legacy-
Lastquellen wurden in einem opt-in-Scope ueberarbeitet:

1. Map- und View-Rendering verwenden einen gemeinsamen Lock pro
   Region-Modul. Dadurch koennen konkurrierende Renderaufrufe nicht mehr
   dasselbe mutable Warp3D-Kamera-, Textur- und Mesh-Feld ueberschreiben.
2. Szene, Native-Warp3D-Renderer und Render-Caches werden auch bei
   Exceptions im `finally` abgebaut.
3. Diagnose-PNG `MAP-<RegionID>.png` und die doppelten Full-GC-
   Aufrufe bleiben zunaechst **standardmaessig an**, koennen aber
   auf isolierten Testnodes deaktiviert werden.

## Testkonfiguration

Im geladenen `[Map]`-Abschnitt:

```ini
[Map]
    Warp3DWriteMapPng = false
    Warp3DForceFullGC = false
```

Voraussetzung: `MapImageModule = Warp3DImageModule` und aktives
OGLPerformance/GC-Monitoring. Keine Aenderung an den Map-Assets selbst.

## Mess- und Rollbackplan

- Vorher/nachher gleiche Regionen, gleiche Meshes, gleiche Regionslast.
- Map-Tile-Zeit, GC-Pause, Prozess-RSS, Heap, Mesh-/Texturfehler, CPU,
  Region Frame P95/P99 sowie Viewer-WorldMap-Korrektheit vergleichen.
- Bei visuellen Fehlern, Heap-Anstieg oder Shutdown-Problemen wieder
  `Warp3DWriteMapPng = true` bzw. `Warp3DForceFullGC = true` setzen.
- Das Abschalten des Full-GC entfernt nicht die MemoryPressureTrim-
  Funktion von Flotsam, sondern verhindert nur diesen einen
  erzwungenen Sammlungsort.

Reale Lasttests im Grid stehen aus. OAR-Export und Scene-Start behalten
ihre bisherigen GC-Pfade und sind separate Folgepakete.
