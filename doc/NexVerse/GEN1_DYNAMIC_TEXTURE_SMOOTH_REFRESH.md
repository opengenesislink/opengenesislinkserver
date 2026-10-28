# Generation 1 – Flüssigere Aktualisierung dynamischer Texturen

## Problem

`osSetDynamicTextureDataFace("", "vector", ...)` berechnet eine **neue Asset-UUID** bei jeder Minute der Stadt-NexVerse-Bus-Anzeigetafel. Die neue Grafik wird zuerst als lokales/temporäres Asset gecacht und anschließend der Textur-Slot des Prims aktualisiert. Der bisherige `DISP_EXPIRE`-Pfad löschte das vorherige temporäre Asset unmittelbar aus dem lokalen Asset-Cache. Das kann ausstehende Viewer-Requests auf die bisherige UUID ins Leere laufen lassen, während neue Daten noch übertragen werden.

Die Generierung der neuen Grafik und das Prim-Update waren bereits in der richtigen Reihenfolge. Eine vom **Viewer sichtbare** vollständig atomare Übergabe zwischen zwei unterschiedlichen Texture-UUIDs ist über das bestehende LSL/OSSL-Protokoll hingegen nicht garantiert.

## Implementierung

Der DynamicTextureModule-Pfad behält die **vorherige temporäre Textur** bei aktiviertem `DISP_EXPIRE` optional für eine begrenzte Übergangszeit im Cache:

```ini
[Textures]
    DynamicTexturePreviousRetentionSeconds = 30
    DynamicTexturePreviousRetentionLimit = 256
```

- Standardprofil `OpenSimDefaults.ini`: `RetentionSeconds = 0` (bisheriges Verhalten).
- Stadt-NexVerse-Aktivprofile und `OpenSim.ini.example`: 30 Sekunden, höchstens 256 noch nicht abgelaufene Asset-Referenzen pro Simulatorprozess.
- Wertebereiche: 0–120 Sekunden, 1–2048 zeitgleich anstehende alte Assets.
- Der neue `DynamicTextureRetirementQueue` erledigt die teure Cache-Expiration **außerhalb des Queue-Locks**. Ein Timer prüft alle fünf Sekunden.
- Wird die Anzahlgrenze erreicht, wird die älteste gespeicherte Textur vorzeitig freigegeben, damit Speicherverbrauch nicht grenzenlos wächst.
- Beim Schließen des Moduls wird die Queue geleert und der Timer gestoppt.
- Ist `ReuseDynamicTextures = true`, bleibt die bestehende eigene Reuse-Logik erhalten (sie deaktiviert bereits `DISP_EXPIRE`). Die neue Retention soll die normale, nicht wiederverwendbare Minutengrafik unterstützen.
- Das Löschen eines Prims während eines asynchronen URL-Renders erzeugt beim Fehlerbericht keine NullReferenceException mehr.

Die neue Übergangsfrist gilt für Texturen, die tatsächlich per `DISP_EXPIRE` ersetzt werden und deren altes Asset als `Temporary` markiert ist. Andere Texturen, Berechtigungen, Viewer-CAPS und normale Asset-Routen werden nicht verändert.

## Beispiel-Skript

`tools/diagnostics/ogl_bus_departure_board_smooth.lsl` verwendet die vorhandene Stadt-NexVerse-Bustafel mit 1024×512-Vektorgrafik. Das Skript lässt die alte Face-Textur stehen und verhindert identische Neu-Renderings innerhalb derselben Minute und bei gleichem Vektor-Bildinhalt. `llMD5String` prüft, ob die Bildanweisungen tatsächlich geändert wurden.

Der Timer prüft weiterhin alle 10 Sekunden und rechnet die nächsten Abfahrten jede volle Minute neu aus. Die deutsche Winter-/Sommerzeit muss in diesem Beispiel wie bisher über `UTC_OFFSET = 1/2` eingestellt werden.

## Was diese Änderung **nicht** leistet

**Keine 100-%-flackerfreie Umschaltung:** Der Firestorm-/OpenGenesisLINK-Viewer entscheidet selbst, wann er die **neue** Texture-UUID herunterlädt und auf die GPU bringt. Er kann bis dahin eine graue/weiße Platzhalterfläche zeigen, auch wenn die vorherige UUID serverseitig noch gültig ist. Die Serveränderung verhindert nur eine Ursache für verlorene alte Texture-Fetches. Für einen garantierten sichtbaren Bildwechsel benötigt man eine zusätzliche Viewer-Unterstützung (Swap nach bestätigtem Cache-Prefetch) oder einen geeigneten zweilagigen primseitigen Anzeigeaufbau mit vorgepufferter Textur.

Die Retention vergrößert den temporären Asset-Cache und kann bei großer Last begrenzt werden. Deshalb ist die Funktion im generischen Default deaktiviert und im Stadt-NexVerse-Profil auf 30 Sekunden / 256 Referenzen begrenzt.

## Tests

- Deterministische Runtime-Regression der Queue: Grace-Period, Grenzfall, FIFO-Overflow, Immediate-Expire, Flush und Fehlerisolation.
- CI überprüft Parameter, Cache-vor-Prim-Reihenfolge, nur temporäre Expiration und bestehende OSSL-API.
- Auf echter Region prüfen: ein einzelner 1024×512-Anzeigetafel-Prim, anschließend viele parallel. Alle 60 Sekunden muss die Abfahrtstafel aktualisieren, ohne dass die vorige Textur sofort aus dem Cache verschwindet.
- Firestorm und OpenGenesisLINK Viewer separat beobachten: sichtbares Weiß-Flackern ist ein clientseitiger Abnahmetest und **nicht** durch die CI nachgewiesen.
- Nach Simulatorneustart dynamische Texte neu erzeugen; rein lokale Cache-Assets überleben den Neustart nicht dauerhaft.
