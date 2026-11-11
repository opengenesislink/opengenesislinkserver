# OpenGenesisLINK Generation 1 - fester Stable- und Update-Kanal

Die oeffentliche Produktbezeichnung bleibt **OpenGenesisLINK Generation 1**.
Die technische Generation-1-Versionsnummer bleibt **0.9.4.0**. Es gibt keine
fortlaufende 0.9.4.1/0.9.5.0-Release-Serie fuer diese Generation.

## Release-Kanaele

- `v0.9.4.0`: einmaliger offizieller Stable-Git-Tag und unveraenderliches GitHub Release. Niemals umtaggen, loeschen oder Artefakte ersetzen. Ein Stable-ZIP wird nur nach Staging-Abnahme gebaut.
- `gen1-updates`: aktualisierbarer, als Prerelease gekennzeichneter GitHub-Download fuer neue Generation-1-Updates. Je Build ein Commit-SHA im `BUILD-MANIFEST.json` und SHA256-Pruefsumme. Der GitHub-Release-Tag ist bewusst beweglich, **nicht** das offizielle Stable-Tag.
- `gen1-main`: bisheriger Entwicklungs-Snapshot (historisch). Solange die Stable-Freigabe aussteht, ist er KEIN Produktionsdownload. Nach Freigabe veroeffentlicht der neue Update-Kanal; der alte Publisher wird durch `Flavour.Release` automatisch inaktiv.

Build-Identitaet: Git-Commit-SHA im Manifest und fuer Betreiber sichtbare Build-ID, nicht in der oeffentlichen Viewer-Version. API-/DB-Schemata und Protokollkompatibilitaet koennen separat versioniert werden. Bricht ein Update die Kompatibilitaet, muss die Ein-Generationen-Politik neu bewertet werden, statt einen stillen Bruch unter 0.9.4.0 auszuliefern.

## Freigaberegeln

1. `main` muss fuer die freizugebende SHA eine vollstaendige gruene NexVerse CI, Linux-x64, Windows-x64 und ARM64 Build-/Boot-CI besitzen.
2. Echte Staging-/Viewer-/Hypergrid-/Inventar-/Mesh-/MariaDB-/FSAssets-/Backup-/Rollback-Abnahmen sind mit konkreten Nachweisen in `doc/release-acceptance/GEN1_0.9.4.0.md` dokumentiert.
3. `OpenSim/Framework/VersionInfo.cs` wird **erst mit Freigabe** von `Flavour.Dev` auf `Flavour.Release` umgestellt. Die Nummer 0.9.4.0 bleibt unveraendert.
4. Der manuelle Workflow `g1-stable-0940.yml` muss vom Betreiber explizit mit dem aktuellen HEAD aufgerufen werden. Er prueft das Freigabeprotokoll, die CI-Checks, .NET10-Build und Paketinhalt und erstellt nur bei noch nicht vorhandenem `v0.9.4.0`-Tag das Stable-Release.
5. Der Update-Publisher `g1-updates.yml` wird nach erfolgreicher CI nur aktiv, wenn Stable-Tag und `Flavour.Release` existieren und die Repository-Variable `OGL_GEN1_UPDATES_APPROVED=true` gesetzt ist.
6. Updates duerfen keine INIs, Datenbanken, Assets oder Betreiber-Secrets im ZIP enthalten; vor Installation Backup, Rollback-Moeglichkeit und Kompatibilitaet pruefen.

**GitHub-Einstellungen nachziehen:** `main` mit Ruleset/Branch-Protection absichern; fuer `v0.9.4.0` ein unveraenderliches Tag-RuleSet setzen; `generation1-stable` als Environment mit Required Reviewers konfigurieren; die beiden Freigabevariablen erst nach Abnahme setzen. Ohne diese Einstellungen bleibt der Workflow durch sein Freigabeprotokoll und den Quellcode-Check gesperrt.

## Betrieb

Operatoren sollen Update-ZIPs anhand `BUILD-MANIFEST.json.source_commit`, SHA256 und Changelog identifizieren. Den letzten funktionierenden Commit und den Datenstand fuer Rollback notieren. Eine Versionnummer allein zeigt nicht den installierten Patchstand. Keine automatischen unkontrollierten Updates der live laufenden ROBUST-/Simulator-Prozesse.
