# OpenGenesisLINK Generation 1 – Dokumentation

Die Projektuebersicht steht in der [README](../README.md). Das
Buildverfahren fuer Linux und Windows steht in [BUILDING.md](../BUILDING.md).
Aktuelle Produktinformationen: **https://opengenesislink.de**.

## Einstieg und Betrieb

| Thema | Dokument |
| --- | --- |
| Build unter .NET 10 | [Buildanleitung](../BUILDING.md) |
| Tests unter .NET 10 | [Tests und CI](TESTING.md) |
| Migration und Laufzeit | [.NET-10-Migration](GEN1_NET10_MAIN_MIGRATION.md) |
| Grid, Portale und Funktionen | [NexVerse-Dokumentation](NexVerse/) |
| OpenGenesisLINK-Module | [OGL-Dokumentation](OpenGenesisLINK/) |
| World API und Integration | [World-API-Uebersicht](NexVerse/WORLD_API.md) |

## Wichtige Fachbereiche

- [Profile Feed: interaktive Webprofile](OpenGenesisLINK/OGL_PROFILE_FEED_INTERACTIVE_V2.md)
- [Profile Feed: kontrollierter Rollout](OpenGenesisLINK/OGL_PROFILE_FEED_ROLLOUT.md)
- [OGL IAR Self-Service](NexVerse/OGL_IAR_SELF_SERVICE.md)
- [Oekonomie und Ledger](NexVerse/ECONOMY_LEDGER.md)
- [NPC Owner World API](NexVerse/WORLD_NPC_OWNER_API_R617.md)

## Bereinigter Dokumentationsstand

Die frueheren Vorabplanungen, RC-Entwuerfe, alten Checkpoints und
nicht mehr verwendeten Release-Handbuecher wurden **aus dem aktuellen
Branch entfernt**, statt sie in ein weiteres Archiv zu verschieben.

Erhalten bleiben nur Dokumente mit aktuellen Funktionen, laufenden
Betriebs- oder Sicherheitsvertraegen, Migrationen und nachweislich
weiterhin benoetigten Abnahmeinformationen. Alte veroeffentlichte
Git-Tags bleiben unveraendert; sie sind nicht der aktuelle Buildstand.

Fuer weitere Aenderungen gelten die
[deutschen Beitrags- und Commit-Regeln](../CONTRIBUTING.md).
Registrierte Regionskonfigurationen gehoeren ausschliesslich in die
jeweilige Installation, nicht in Git.

## Repository-Struktur

```text
OpenSim/          Simulator-, ROBUST- und Dienstquellcode
NexVerse/         OpenGenesisLINK-G1-Erweiterungen und World API
addons/           Erweiterungen und Addons
addon-modules/    Separat baubare Modulintegrationen
services/         Begleitdienste
bin/              Laufzeit und notwendige mitgelieferte Abhaengigkeiten
tools/            Build-, Test-, Migrations- und Releasewerkzeuge
doc/              Aktuelle Technik-, Betriebs- und Testdokumentation
.github/workflows/ CI und Releaseautomatisierung
```

**Wichtig:** `bin/` enthaelt notwendige native und verwaltete
Abhaengigkeiten. Das Verzeichnis kann nicht pauschal geloescht
oder als Ganzes ignoriert werden. Laufzeitlogs und lokale
Operator-Secrets gehoeren dagegen nicht in die Versionsverwaltung.
