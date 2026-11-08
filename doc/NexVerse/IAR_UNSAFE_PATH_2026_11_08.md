# OGL IAR G1 – Ablehnung „Unsicherer IAR-Pfad“ (08.11.2026)

## Beobachtung
Der native Inventarexport hat TAR und GZip erfolgreich abgeschlossen, die
nachgelagerte Prüfung meldet jedoch `IAR-Vorabpruefung abgelehnt:
Unsicherer IAR-Pfad`. Diese Meldung unterscheidet sich vom bereits
behobenen GNU-TAR-LongName-Problem aus PR #406.

## Ursachen im Quellcode
- Der alte IAR-Writer übernimmt `:` und Backslash `\` **ungefiltert**
  aus Inventarordner- bzw. Itemnamen in TAR-Einträge.
- Die Sicherheitsprüfung verbietet diese Zeichen ausdrücklich (zu Recht,
  weil TAR-Pfade unabhängig vom Betriebssystem sicher bleiben sollen).
- Verschachtelte Inventarordner tragen zusätzlich einen UUID-Suffix und
  können den früheren 512-Zeichen-Pfadgrenzwert überschreiten.
- **Die konkrete Ursache im gemeldeten Export lässt sich ohne eine
  betroffene Archiv-Pfadprobe nicht eindeutig feststellen.**

## Korrektur
- `InventoryArchiveUtils.EscapeArchivePath` schreibt `:` als `&#58;`
  und Backslash als `&#92;` in Archiv-Pfadbestandteile. Die bereits
  vorhandenen Kodierungen für `&` und `/` bleiben bestehen.
- `InventoryArchiveUtils.UnescapeArchivePath` dekodiert diese Namen
  beim Inventarimport exakt einmal zurück. Originalnamen im
  Inventar-/Item-XML bleiben unverändert.
- OGL-Inspektor erlaubt bis zu 4096 **ASCII-Zeichen** pro TAR-Pfad (statt
  512), inklusive regulärer GNU-LongName-Metadaten. Das Limit bleibt
  hart, einschließlich bestehender Gesamt-/Dateigrößen-/Eintragsgrenzen.
- Für erneut auftretende Ablehnungen wird jetzt nach Ursache
  unterschieden: leerer/absoluter Pfad, Backslash, Doppelpunkt,
  Nullzeichen, Traversal oder Überlänge. Keine privaten Inventarnamen
  werden ungefragt ins Log geschrieben.
- Unverändert **verboten**: rohe Backslashes/Doppelpunkte in TAR-Pfaden,
  absolute Pfade, `..`-Traversal, GNU Link Targets (`K`),
  Symlinks/Hardlinks, Sparse, PAX, unbekannte TAR-Typen.

## Überprüfung nach Deployment
1. Neuen Code bauen und **Simulator mit Inventory-Archiver neu starten**
   (nicht nur ROBUST).
2. Mit einem lokalen Testaccount Ordner und Objekte mit `:`, Backslash,
   Slash, Ampersand sowie verschachtelten langen Ordnernamen exportieren.
3. Auf `Archivpruefung erfolgreich` und `completed` achten.
4. IAR im **Staging** importieren; Ordnernamen inklusive Sonderzeichen
   verifizieren, keine Asset- oder Inventar-Duplikate.
5. Fehlende Asset-Referenzen getrennt prüfen. Ein formal akzeptiertes
   IAR ist noch kein vollständiges Asset-Backup.

Bereits erzeugte, **roh unsichere** TAR-Pfade werden aus Sicherheitsgründen
nicht nachträglich freigegeben. Betroffene Inventare **neu exportieren**.
Der auf GitHub geänderte Code bedeutet noch keine Produktivaktualisierung.

## Testabdeckung
`tools/ci/OglIarPreflightRegression` prüft sichere kodierte Namen,
Überlänge, reguläre lange Inventarpfade und die Ablehnung roher
unsicherer TAR-Namen. Zusätzlich existiert ein direkter
`InventoryArchiveUtils`-Roundtrip-Test für Inventarnamen.
