# OpenGenesisLINK G1 – OGLPerformance 1.2: CPU-, RAM-, Regionen- und Assetcache-Diagnose

**Status:** optionale read-only Observability; keinerlei automatische
Physik-, GC- oder Thread-Affinity-Steuerung.

## Datenweg und Zugriffsberechtigung

Alle Werte stammen aus den bereits bestehenden, signierten
`node.heartbeat`-NexBus-Nachrichten. Der Simulator sendet sie nur mit
`[OGLPerformance] Enabled = true`. ROBUST verarbeitet ausschließlich
zugelassene Felder und gibt sie nur über die bestehenden autorisierten
`GET /api/v1/nodes` und `GET /api/v1/nodes/{nodeId}` mit
`simulators:read` aus. Kein zusätzlicher öffentlicher Endpoint.
Ohne aktualisierten NodeAgent, etwa auf einer noch alten Gummybear-Node,
bleiben die neuen Werte leer.

## Neuer Diagnosebereich im World API Control Center

Unter **Simulator-Nodes** den Node anklicken. Es erscheinen:

1. **CPU-Prozessauslastung** relativ zu `Environment.ProcessorCount`,
   **Working Set**, **GC-Fragmentierung** und **GC-Pausen**.
2. Vier Verlaufskurven: CPU, Working-Set/Managed Heap, GC-Pausen,
   Allokationsrate. Der Browser behält maximal 120 Messpunkte je Node,
   bis zu 24 Nodes. **Kein persistierter Serververlauf**; nach Seiten-Reload
   beginnt die Zeitreihe neu.
3. **Linux-Host-Auslastung pro logischem CPU-Kern** aus `/proc/stat`,
   in der UI deutlich als *Host*-Last bezeichnet. Diese Zahlen
   zeigen **nicht die Auslastung des OpenSim-Prozesses pro Kern** und
   keine sichere Zuordnung zu einzelnen Regionen/Threads. Auf Windows
   wird diese Zusatzmessung nicht angeboten. Der erste Messzeitpunkt
   ist eine Baseline; erst mit dem nächsten Heartbeat entstehen Raten.
4. **Pro Region** tatsächliche Heartbeat-Hz, Ziel-Hz, Framezahl,
   Work-P95, Frame-P95/P99 und beide Framebudget-Überschreitungsanteile.
   Maximal 64 Regionen je Node/Heartbeat (42 Regionen sind erfasst);
   überschüssige Regionen werden als Anzahl separat gemeldet.
   Diese Werte entstehen in der bereits integrierten
   `OglRegionTimingWindow`, ohne die Simulationsphysik zu ändern.
   Sie sind **keine CPU-Zeitmessung pro Region**.
5. **Flotsam-Cache**: RAM-LRU-Belegung/Budget, bisherige Requests,
   RAM- und Disk-Hits, Writerzahl, adaptive Ziel-/Aktiv-Worker,
   wartende Disk-Writes, Bytes-in-Flight, abgewiesene Cache-Writes
   und optional entlastete RAM-LRU-Einträge.
   Nur der aktive Flotsam-Cache liefert diese Werte über ein internes
   read-only Interface `IOglAssetCacheMetricsProvider`.
   Das ist kein globales Asset-, RAM- oder Prozesslimit.

Die API verwirft ungültige, negative, nicht-finite und übergroße
Metriken. Region- und Kern-JSON sind gegen übergroße Arrays und
Eingaben begrenzt. Fehlende Messungen löschen den bisherigen Snapshot.
Ein inaktiver Node zeigt keine neuen Livewerte.

## Konfiguration einer Test-Node

Im tatsächlich geladenen `OpenSim.ini`:

```ini
[OGLPerformance]
    Enabled = true
```

Optional im tatsächlich geladenen `[AssetCache]` (nicht in
`FlotsamCache.ini.example` allein), nach Prüfung der vorhandenen Werte:

```ini
[AssetCache]
    FileWriteConcurrency = 2
    MaxPendingFileWrites = 1024
    MaxInFlightFileWriteMiB = 256
    AdaptiveFileWrites = true
    MemoryCacheEnabled = true
    MemoryCacheMaxMiB = 256
    MemoryCacheMaxEntries = 8192
    MemoryPressureTrimEnabled = false
```

Aktivierung erfordert den regulären Neustart des Simulatorprozesses,
**nicht** einen unkontrollierten ROBUST-Update-Lauf.

## Grenzen und Validierung

`perf_process_cpu_percent` ist ein Durchschnitt über die aus Sicht
des Prozesses verfügbaren logischen Kerne. Ein einzelner überlasteter
Thread kann die Gesamtrate klein erscheinen lassen; hohe
`/proc/stat`-Werte beweisen wiederum nicht, dass OpenSim der
Verursacher ist. Eine tatsächliche Prozess-Thread/Core-Zuordnung
erfordert ein separates Profiling, beispielsweise mit `dotnet-trace`
und systemweitem `perf` auf einer Test-Node.

Für belastbare Optimierungen die gleichen OARs, Mesh-/Textur-Lasten,
Avatarzahlen und Host-Ressourcen verwenden. CPU, GC, Work-P95/P99,
Cache-Hit-Rate und Ladezeiten vor/nach einer isolierten
Konfigurationsänderung vergleichen. Erst nach Regressionstests und
realistischen Benchmarks lohnt sich eine neue Scheduling-Strategie.
