# OpenGenesisLINK G1 – OGL Performance Engine 1.0 (foundation)

**Status:** opt-in Entwicklungsblock; kompatibler Standardbetrieb bleibt unveraendert.

## Was bereits implementiert ist

1. **Read-only NodeAgent-Diagnostik:** CPU-Auslastung des Simulatorprozesses relativ zu `Environment.ProcessorCount`, Prozess-Privatspeicher, Managed Heap, GC-Heap/Fragmentierung, GC-Pausenanteil zwischen Samples, Allokationsrate, GC-Collection-Counter, Threadpool-Threads, aktive Worker/Pending Jobs sowie **tatsaechliche** Heartbeat-Hz pro Region. Wird als `perf_*`-Felder in die bestehende HMAC-authentifizierte `node.heartbeat`-Nachricht geschrieben. Die Zahl der berichteten Regionen ist auf 32 begrenzt. Kein neuer oeffentlicher HTTP-Endpunkt.
2. **Unabhaengige Disk-Cache-Schreibjobs:** `[AssetCache] FileWriteConcurrency` (1..min(8,CPU-Kerne)) erlaubt mehrere Flotsam-Schreibworker. Jede Datei wird weiterhin durch `m_CurrentlyWriting` vor doppelten Schreibauftraegen geschuetzt.
3. **Schreibwarteschlangen-Backpressure:** `[AssetCache] MaxPendingFileWrites` (>0) begrenzt die Zahl wartender Jobs und damit das durch diese Jobs gepinnte Asset-Memory **nach Jobanzahl, nicht nach Bytegroesse**. Bei Ueberlast wird nur der *optionale Disk-Cache-Write* uebersprungen; das autoritative Asset bleibt abrufbar. Nicht angenommene Jobs entfernen den Dateinamen aus der In-Progress-Tabelle.
4. **Kompatibilitaet:** Bestehende Simulator-Physik, Skript-Threading, Runtime-GC-Modus, UDP und 55-FPS-Normalisierung bleiben unveraendert. Flotsam verwendet weiter 1 Writer und unbegrenzte Queue, bis der Betreiber die neuen Werte explizit setzt.

## Aktivierung fuer einen isolierten Testsimulator

```ini
[OGLPerformance]
    Enabled = true

[AssetCache]
    ; Bestehende, funktionierende Werte der lokalen AssetCache-Sektion beibehalten.
    FileWriteConcurrency = 2
    MaxPendingFileWrites = 1024
```

Die `[AssetCache]`-Einstellungen gehoeren in die **wirklich geladene** Flotsam-Konfiguration; Werte der `bin/config-include/FlotsamCache.ini.example` allein werden nicht automatisch aktiv. Die Startup-Konfiguration in `bin/OpenSim.ini` muss nicht fuer diesen Block geaendert werden. Falls `NexVerseNodeAgent.Enabled = false`, werden keine Telemetriefelder ueber NexBus publiziert.

## Welche Werte relevant sind

- `perf_process_cpu_percent`: Prozess-CPU-Prozent relativ zu aktuell verfuegbaren logischen Prozessoren. 100 % bedeutet durchschnittlich alle verfuegbaren Kerne voll ausgelastet, nicht eine einzige Region.
- `perf_region_heartbeats_json`: tatsaechliche Szene-Heartbeat-Rate (keine normalisierte Viewer-Sim-FPS). Das erste Sample fuer eine Region ist 0 als Baseline.
- `perf_allocated_bytes_per_second`, `perf_gc_pause_percent`, `perf_gc_fragmented_bytes`: Allokation und GC-Druck.
- `perf_threadpool_pending_items`: Groesse der globalen .NET-Threadpool-Warteschlange. Die Flotsam-Queue besitzt eine *eigene* Grenze.
- `perf_region_sample_omitted`: Anzahl nicht inkludierter Regionen, falls mehr als 32 Regionen auf einem Node laufen.

Die Felder werden als Teil des bestehenden intern signierten NodeAgent-Events gesendet; sie enthalten technische Betriebsinformationen und sollten nur autorisierten Admins zugaenglich sein.

## Messplan und Abnahmekriterien

1. Gleiche OAR/Region und gleiche Avatarzahl auf identischer Hardware und gleichen Disk-/Netzwerkbedingungen verwenden.
2. Baseline (1 Writer / Legacy-Queue) und Test (2/4 Writer / 1024 Queue) jeweils mit realistischem Mesh-/Textur-Load messen.
3. Quantile der echten Heartbeat-Hz, CPU, Allokationsrate, GC-Pause, I/O-Latenz, Job-Queue, Asset-Cache-Hit-Rate und Viewer-Ladezeiten vergleichen.
4. Cache-Schreibjobs duerfen das **autoritative** Asset niemals veraendern. Beim Queue-Limit duerfen keine Dateien permanent als 'writing' markiert bleiben.
5. Unter realer Last auf mehrere Regionen pruefen, dass Physics, EntityCrossing, YEngine und Backups unveraendert funktionieren.

## Noch nicht implementiert

- Parallele Mutation einer Region / Multi-Core-Physik.
- Dynamische Heartbeat-Anhebung auf 30/60/100/120 Hz.
- RAM-Byte-Budgets, LRU-Eviction fuer den *MemoryAssetCache*, NUMA-Platzierung, GC-Fremdsteuerung, automatischer adaptiver Scheduler.
- Verbindliche Performance-Gewinne: diese werden erst durch Benchmarks bestaetigt.

Diese Aufgaben brauchen getrennte Regressionen, insbesondere fuer Objektreihenfolge, Physikdeterminismus und regionsuebergreifende Synchronisierung.
