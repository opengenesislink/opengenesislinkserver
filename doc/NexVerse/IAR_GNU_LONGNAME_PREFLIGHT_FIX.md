# OGL G1 IAR – GNU TAR LongName in der Vorabprüfung

## Störungsbild (08.11.2026)

Nach erfolgreichem Export von 5634 Assets und TAR/GZip-Abschluss meldete
der Integritätscheck: `IAR enthaelt unerlaubte TAR-Typen (Links/PAX/Sparse)`.
Der Export-Job wurde daraufhin abgelehnt.

## Root Cause

Der native `OpenSim.Framework.Serialization.TarArchiveWriter.WriteFile()`
schreibt für Dateipfade mit mehr als 100 Zeichen zunächst einen
GNU-TAR-Metadatensatz **`././@LongLink` mit Typ `L`**. Direkt danach
kommt der reguläre Datei- oder Verzeichniseintrag mit dem
auf 100 ASCII-Bytes abgeschnittenen Header-Namen.

Die OGL-eigene Vorabprüfung `OglIarInspector` behandelte bisher **alle**
Typen außer `0`, NUL und `5` als verbotene Links/PAX/Sparse.
Damit lehnte sie legitime, vom eigenen Writer erzeugte IAR-Dateien ab.
Der Legacy-Importer `TarArchiveReader` unterstützt das
GNU-LongName-Format bereits.

## Sicherheitsbegrenzung

Es wird **nur** der Eintragstyp `L` zugelassen, wenn:

- Headerpfad exakt `././@LongLink` ist;
- die Nutzlastlänge zwischen 101 und 512 Bytes liegt;
- der Pfad strikt druckbares ASCII ohne NUL/Steuerzeichen enthält
  (kompatibel mit dem vorhandenen Writer);
- der vollständige Pfad weiterhin die bestehenden IAR-Pfadregeln erfüllt;
- der direkt folgende reguläre Datei- oder Verzeichniskopf
  tatsächlich den erwarteten 100-Byte-Präfix trägt;
- keine verschachtelten oder am TAR-Ende verwaisten LongName-Metadaten
  auftreten.

Es gibt **keine** Freigabe für Hardlinks (`1`), Symlinks (`2`),
GNU long *link targets* (`K`), PAX (`x`/`g`), Sparse (`S`),
unbekannte TAR-Typen, Traversal, doppelte Dateipfade oder unzulässige
Dateigrößen. LongName-Header fließen in Anzahl- und Größenlimits ein.

## Betrieb

Nach erfolgreich bestandener CI die Simulator-Binaries aktualisieren
und betroffene Regionen neu starten. ROBUST nur neu starten, wenn
es denselben geänderten Binary-Satz benutzt. Vorher **keine** gescheitert
gemeldete IAR blind importieren oder als gültiges Backup zählen.
Bereits vorhandene, vom vorigen Prüfer abgewiesene `.iar` können
mit dem aktualisierten `OglIarInspector` erneut geprüft werden;
damit ist noch nicht bewiesen, dass alle referenzierten Assets vorhanden sind.

Die getrennten Logwerte `832 fehlend/ungueltig` und `14 fehlend`
beziehen sich auf unterschiedliche Asset-Erfassungsphasen.
Die LongName-Korrektur löst **nicht** fehlende Asset-Referenzen.
