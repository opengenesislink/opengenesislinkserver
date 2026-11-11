# OpenGenesisLINK – Versionskennung

**Aktuelle Entwicklungslinie: OpenGenesisLINK Generation 1 (0.9.4.0)**

OpenGenesisLINK bezeichnet die eigenstaendige Serverplattform. Die
Produktgeneration ist **Generation 1**. Die technische Versionsnummer
`0.9.4.0` wird im Quellcode unter
`OpenSim/Framework/VersionInfo.cs` gefuehrt. Die Plattform wird mit
**.NET 10** gebaut und betrieben.

- **Quellstand:** `main` enthaelt den laufenden Generation-1-Stand.
- **Build:** `global.json`, `runprebuild.sh` und `OpenSim.sln`
  definieren den .NET-10-Buildpfad.
- **Release:** Ein Commit auf `main` ist nicht automatisch ein
  freigegebener Stable-Release. Offizielle Veroeffentlichungen und
  herunterladbare Archive stehen unter
  [GitHub Releases](https://github.com/opengenesislink/opengenesislinkserver/releases).
- **Historische Versionen:** Bereits veroeffentlichte Tags und
  Dokumentationen bleiben fuer Nachvollziehbarkeit und Betreiber-
  Migrationen erhalten. Historische Versionsnummern werden nicht
  erneut als aktuelle Plattformversion beworben.
- **Kompatibilitaet:** Bestehende interne Namespaces, Konfigurations-
  schluessel und Protokollbezeichner duerfen aus Kompatibilitaetsgruenden
  weiterhin abweichende Namen tragen.

Weitere Informationen: **https://opengenesislink.de**.

## Dauerhafte Stable-Linie ab 0.9.4.0

**Entscheidung:** `v0.9.4.0` wird nach nachgewiesener Produktivabnahme
genau einmal als offizielles Stable-Release erstellt. Die oeffentliche
Produktgeneration heisst auch danach weiter `OpenGenesisLINK Generation 1`.
Innerhalb dieser Generation sind keine weiteren fortlaufenden
0.9.4.1-/0.9.5.0-Versionen geplant.

Neue Fehlerbehebungen, Sicherheitsupdates und kompatible Verbesserungen
werden ueber den aktualisierbaren Download `gen1-updates` verteilt.
Der unveraenderliche Stable-Tag `v0.9.4.0` bleibt als Referenz erhalten.
Jedes Update weist einen eindeutigen Git-Commit-SHA und eine SHA256-Pruefsumme
aus. Betreiber sollten diese Build-ID statt allein der Versionsnummer
fuer Diagnose, Rollback und Support verwenden.

Der aktuelle `main` ist bis zur formellen Abnahme **kein freigegebener
Stable-Stand**. `Flavour.Dev` bleibt bis zum Release-Gate erhalten.
Die technische Dokumentation zu Freigaben, Sicherheitsgrenzen und
CI-Gates liegt unter
[GEN1_STABLE_UPDATE_POLICY.md](doc/OpenGenesisLINK/GEN1_STABLE_UPDATE_POLICY.md).
