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
