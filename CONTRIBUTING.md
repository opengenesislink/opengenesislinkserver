# Mitarbeit an OpenGenesisLINK Generation 1

OpenGenesisLINK Generation 1 (0.9.4.0) wird mit **.NET 10** entwickelt.
Build und Tests: [BUILDING.md](BUILDING.md) und [doc/TESTING.md](doc/TESTING.md).

## Deutsche Commit-Meldungen

**Neue Commits, Pull-Request-Titel und Zusammenfassungen werden auf Deutsch geschrieben.**
Wir verwenden ab jetzt ein klares, einheitliches Schema:

- `Neu: <kurze, verstaendliche Beschreibung>` – neue Funktion oder Datei.
- `Update: <kurze, verstaendliche Beschreibung>` – Aktualisierung vorhandener Funktionen und Inhalte.
- `Verbesserung: <kurze, verstaendliche Beschreibung>` – Fehlerkorrektur, Aufraeumen, Sicherheit oder Optimierung.

Beispiele:

```text
Neu: Profilfeed mit Bildern erweitern
Update: Regionsverwaltung an .NET 10 anpassen
Verbesserung: Unnoetige Protokolldateien entfernen
```

Keine kryptischen Kuerzel wie `feat:`, `fix:`, `chore:` oder
undurchsichtige Ticketbezeichnungen als alleinige Beschreibung.
Die Zusammenfassung sollte auch fuer Nicht-Entwickler erklaeren,
was sich tatsaechlich geaendert hat.

**Bestehende Git-Historie wird nicht nachtraeglich umgeschrieben.**
Die neue Regel gilt fuer zukuenftige Aenderungen und Merge-Commits.

## Repository-Struktur

- Neue technische Dokumente unter `doc/` in den passenden Fachbereich
  einsortieren und den Index `doc/README.md` bei Bedarf anpassen.
- Keine Laufzeitlogs, OIDC-Schluessel, lokalen Region-INI-Dateien,
  Datenbanken, Caches oder generierten `obj/`-Verzeichnisse committen.
- Kompatibilitaetskritische Serverdateien, `bin/`-Abhaengigkeiten und
  Release-Tests nicht ohne nachvollziehbaren Ersatz loeschen.
- Keine Aenderung auf produktiven Grids ohne Backup und Test.

Weitere Informationen: https://opengenesislink.de
