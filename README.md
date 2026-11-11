# OpenGenesisLINK Server

**Generation 1 (0.9.4.0)** · **.NET 10** · **Linux x64 / ARM64 und Windows x64**

**Deutsch** · [English](#english)

OpenGenesisLINK ist eine eigenstaendig weiterentwickelte, modulare Serverplattform fuer virtuelle 3D-Welten. Sie stellt die zentralen Grid-Dienste und den Regionssimulator bereit und ermoeglicht den Betrieb vernetzter, interaktiver Welten mit kompatiblen Viewern.

Der Schwerpunkt liegt auf einer leistungsfaehigen, erweiterbaren Serverarchitektur, modernen Schnittstellen und einem zuverlaessigen Betrieb. Stadt NexVerse ist eine virtuelle Welt auf Basis von OpenGenesisLINK, nicht der Produktname des Servers.

## Funktionsueberblick

- **Grid und Regionen:** Zentrale ROBUST-Dienste, Simulatoren, Regionen, Estates, Avatare, Logins und Teleports.
- **Viewer und Hypergrid:** Anbindung kompatibler Viewer, griduebergreifende Verbindungen sowie Unterstuetzung fuer LSL- und OSSL-Skripte.
- **Inventar und Assets:** Dienste fuer Objekte, Texturen und Inventare, einschliesslich Archivierungs- und Import-/Exportfunktionen.
- **World API:** HTTP-Schnittstellen fuer Administration, Integration und automatisierte Ablaeufe mit Authentifizierung und Berechtigungspruefung.
- **Erweiterbare Plattform:** Module fuer Gruppen, Profile, Suche, Weltkarten, Kommunikation, Wirtschaft und weitere Funktionen.
- **Serverbetrieb:** Ereignisverarbeitung, Protokollierung, Sicherheitsmechanismen und automatisierte Build- und Regressionstests.

Einzelne Funktionen und Erweiterungen haengen von der eingesetzten Konfiguration und den aktivierten Diensten ab.

## Linux-Build mit .NET 10

Vorausgesetzt werden ein aktuelles Linux-System, **Git**, **Python 3** und das **.NET 10 SDK**. Die benoetigte SDK-Version ist in `global.json` festgelegt (ab 10.0.100 innerhalb der .NET-10-Version).

Das SDK kann nach der [offiziellen .NET-Installationsanleitung fuer Linux](https://learn.microsoft.com/de-de/dotnet/core/install/linux) installiert werden.

Auf Debian/Ubuntu koennen die grundlegenden Systempakete so installiert werden:

```bash
sudo apt update
sudo apt install -y git python3 libgdiplus libc6-dev
```

SDK kontrollieren:

```bash
dotnet --info
```

Repository holen und den Server bauen:

```bash
git clone --branch main https://github.com/opengenesislink/opengenesislinkserver.git
cd opengenesislinkserver

bash runprebuild.sh
python3 tools/gen11/verify_net10.py

dotnet restore OpenSim.sln
dotnet build OpenSim.sln --configuration Release --no-restore
```

`runprebuild.sh` erzeugt die Projektdateien, richtet sie fuer **.NET 10** ein und nimmt die integrierten Addons in die Solution auf. Der Release-Build erstellt unter anderem `bin/Robust.dll` und `bin/OpenSim.dll`.

### ROBUST und Simulator starten

Fuer einen Start muessen die Grid-, Datenbank-, Netzwerk- und Regionskonfigurationen zuvor passend eingerichtet sein. Beispielhaft aus dem `bin`-Verzeichnis:

```bash
cd bin
dotnet Robust.dll -inifile=Robust.HG.ini
```

Den Simulator anschliessend in einer **zweiten Sitzung** starten:

```bash
cd bin
dotnet OpenSim.dll
```

Vor Updates produktiver Systeme Konfigurationen, Datenbanken, Inventar und Assets sichern. Beispielkonfigurationen sind keine fertig eingerichtete Produktivinstallation.

## Stable-Release und laufende Updates

Die offizielle **Generation 1 (0.9.4.0)** wird nach produktionsnaher
Abnahme einmalig als Stable-Version mit unveraenderlichem Tag
`v0.9.4.0` veroeffentlicht. Kompatible Updates folgen ohne neue
Versionsreihe ueber den separaten Downloadkanal `gen1-updates`,
jeweils nachvollziehbar anhand des Commit-SHA und der SHA256-Pruefsumme.
Der aktuelle `main`-Entwicklungsstand ist bis zur Freigabe nicht
automatisch Stable.

Details: [Stable- und Update-Richtlinie](doc/OpenGenesisLINK/GEN1_STABLE_UPDATE_POLICY.md).

## Weitere Informationen

**Projektseite, technische Informationen und aktuelle Hinweise:** **[opengenesislink.de](https://opengenesislink.de)**

Fehler und Beitraege: [GitHub Issues](https://github.com/opengenesislink/opengenesislinkserver/issues) · [Pull Requests](https://github.com/opengenesislink/opengenesislinkserver/pulls)

Lizenz- und Urheberrechtshinweise: [LICENSE](LICENSE) · [LICENSE_POLICY.md](LICENSE_POLICY.md) · [NOTICE.md](NOTICE.md)

---

## English

**OpenGenesisLINK Server — Generation 1 (0.9.4.0), powered by .NET 10**

OpenGenesisLINK is an independently developed, modular server platform for interactive virtual 3D worlds. It provides central grid services and region simulators, compatible viewer and Hypergrid interfaces, scripting, inventory and asset services, and a secure World API.

Additional modules support profiles, groups, maps, communication, economy, administration, and operational monitoring. Features depend on the installed configuration and enabled services.

### Build on Linux

Install Git, Python 3, the **.NET 10 SDK** (see `global.json`) and required system libraries. Then run:

```bash
git clone --branch main https://github.com/opengenesislink/opengenesislinkserver.git
cd opengenesislinkserver

bash runprebuild.sh
python3 tools/gen11/verify_net10.py

dotnet restore OpenSim.sln
dotnet build OpenSim.sln --configuration Release --no-restore
```

The build generates the ROBUST and simulator assemblies in `bin/`. Configure the grid, database, and regions before starting any services. Back up production data and configuration before upgrading.

**Further information:** [opengenesislink.de](https://opengenesislink.de)

---

**OpenGenesisLINK · Generation 1 · 0.9.4.0 · .NET 10**
