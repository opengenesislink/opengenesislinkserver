# OpenGenesisLINK Generation 1 – OGL Performance Engine: RAM-Pressure Trim

**Status:** opt-in, konservativer RAM-Entlastungsblock. Keine Aenderung an
Physics, 55-FPS-Normalisierung, Region-Scheduling oder Runtime-GC.

## Problem und Umsetzung

Der mit PR #416 eingefuehrte Flotsam-LRU haelt zwischengespeicherte Assets
innerhalb des konfigurierten RAM-Budgets bis zum Ablauf ihrer TTL oder
bis zur LRU-Verdraengung. Bei hoher System-Speicherlast
kann dieser optionale Cache entbehrliche Payloads vorzeitig freigeben.

Mit `MemoryPressureTrimEnabled=true` ueberprueft ein Timer alle 30 Sekunden
die von .NET ueber `GC.GetGCMemoryInfo()` gemeldete GC-Speicherlast.
Ab `MemoryPressureTrimThresholdPercent` (Default 85 %) werden ausschliesslich
die am laengsten nicht verwendeten Eintraege aus dem **optional aktivierten,
byte-begrenzten Flotsam-RAM-LRU** entfernt, bis maximal
`MemoryPressureTrimTargetPercent` (Default 50 %) des konfigurierten
`MemoryCacheMaxMiB`-Budgets belegt ist.

- Die Quelle ist die GC-MemoryLoad-Schaetzung, **nicht** eine Messung des
  gesamten Linux-RAM-Verbrauchs oder der RSS dieser Node.
- Kein `GC.Collect()`, keine Aenderung des GC-Modus und kein Zugriff auf
  die autoritativen Assets, den Disk-Cache oder die laufende Physik.
- Der Timer ist nur bei aktivem byte-begrenzten RAM-Cache vorhanden.
  Beim Entfernen der letzten Region wird er entsorgt; beim Neustart einer
  Region kann er erneut gestartet werden.
- Ohne ausdrueckliches Opt-in bleibt das bisherige Cache-Verhalten
  unveraendert. Die Cache-Abfragen bleiben synchronisiert und die
  LRU-Entfernung greift nur auf kurz gesperrte interne Metadaten zu.
- Der Status `fcache status` meldet bei aktivierter Funktion
  Schwelle, Zielbudget und die Zahl der aus dem Cache entfernten Assets.

## Beispiel fuer eine Test-Node

In **der geladenen** `[AssetCache]`-Sektion (nicht nur in einer
`.ini.example`-Datei):

```ini
[AssetCache]
    MemoryCacheEnabled = true
    MemoryCacheMaxMiB = 256
    MemoryCacheMaxEntries = 8192
    MemoryPressureTrimEnabled = true
    MemoryPressureTrimThresholdPercent = 85
    MemoryPressureTrimTargetPercent = 50
```

Die Schwelle wird auf 70..99 % und das Ziel auf 10..90 % begrenzt.
Ein hoher Schwellenwert fuehrt nicht automatisch zu geringerer
Gesamtprozess-RSS: Im Cache koennen nur eigene starke Referenzen geloest
werden; andere Referenzen und die .NET-GC-Freigabe bleiben separat.

## Abnahme

Die `OglPerformanceEngineRegression` prueft die Schwelle, ungueltige
Drucksamples, LRU-Verdrängungsreihenfolge und fehlende Uebertrimmung.
Vor produktiver Aktivierung bitte in einem isolierten Simulator die
Speicher-/GC-Metriken, Cache-Hit-Rate und Asset-Ladezeit unter gleicher
Mesh-/Texturlast mit und ohne Trim vergleichen. Entlastung gegen hoehere
Cache-Miss-Rate abwaegen. Das ist kein automatisches Optimieren
aller Prozessspeicher und keine CPU-Kern-Parallelisierung.
