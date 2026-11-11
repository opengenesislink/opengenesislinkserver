# OpenGenesisLINK Generation 1 (0.9.4.0) – .NET 10 Build

> **OpenGenesisLINK Generation 1 (0.9.4.0) wird mit .NET 10 gebaut und betrieben.** Lokale Builds ändern keine laufende Grid-Installation und ersetzen keine produktiven Daten oder INI-Dateien. Ältere OpenSimulator-Bauanweisungen für .NET 8 gelten nicht mehr.

## Voraussetzungen / Requirements

- **.NET 10 SDK** (siehe `global.json`; mindestens 10.0.100, Feature-Rollforward innerhalb von .NET 10).
- **.NET 10 Runtime** für ROBUST und Simulatoren; kein bloßes .NET-8-Runtime-Upgrade.
- Linux x64, Linux ARM64 oder Windows x64; aktuelle Linux-Distribution mit `libgdiplus`, `libc6-dev`. Für optionale native OpenJPEG2-Tests zusätzlich `libopenjp2-7-dev`, `libopenjp2-tools` und `pkg-config`.
- Für MariaDB-Livebetrieb weiterhin korrekte, separat verwaltete Zugänge, ein überprüftes Backup und eine eigene Staging-Konfiguration. **Keine Produktivzugänge im Repository.**

Prüfen:

```bash
dotnet --info
dotnet --list-runtimes
```

Es muss eine `Microsoft.NETCore.App 10.x`-Runtime vorhanden sein. Sicherheits- und Patchupdates innerhalb derselben Major-Version sind ausdrücklich zulässig.

## Linux x64 / ARM64

```bash
git clone --branch main https://github.com/opengenesislink/opengenesislinkserver.git
cd opengenesislinkserver
bash runprebuild.sh
python3 tools/gen11/verify_net10.py
dotnet restore OpenSim.sln
dotnet build OpenSim.sln --configuration Release --no-restore
```

**Wichtig:** Der im Repository mitgelieferte ältere `bin/prebuild.dll` erzeugt technisch noch `net8.0`-Projektdateien. `runprebuild.sh` konvertiert **direkt anschließend** die generierten TFMs zu `net10.0` und fügt die G1-Addons wieder zur Solution hinzu. Der Build selbst läuft anschließend unter .NET 10. Ein isolierter `dotnet bin/prebuild.dll`-Aufruf ohne Retarget-Schritt ist nicht das unterstützte Buildverfahren.

### ROBUST und Simulator starten

Nur in einer gesonderten, vorbereiteten **Staging-Installation** mit eigenen INI-Dateien und eigener Datenbank starten:

```bash
cd bin
dotnet Robust.dll -inifile=Robust.HG.ini
# Simulator in separater Sitzung:
dotnet OpenSim.dll
```

Die Example-INI-Dateien sind **Vorlagen** und müssen für die Testumgebung angepasst werden. Die eingecheckten aktiven `OpenSim.ini` und `Robust.HG.ini` sind **keine** Musterkonfiguration für eine Liveumstellung.

## Windows x64 (PowerShell)

```powershell
git clone --branch main https://github.com/opengenesislink/opengenesislinkserver.git
cd opengenesislinkserver
cmd /c runprebuild.bat
dotnet restore OpenSim.sln
dotnet build OpenSim.sln --configuration Release --no-restore
```

Start im vorbereiteten Staging: `dotnet bin/Robust.dll` bzw. `dotnet bin/OpenSim.dll` bei richtigem Arbeitsverzeichnis, laufenden Diensten und Konfigurationen.

## Verbindliche automatisierte .NET-10-Prüfungen

- `tools/gen11/verify_net10.py`: alle generierten Hauptprojekt-/Addon-TFMs und runtimeconfig-Dateien auf .NET 10 prüfen.
- `tools/gen11/robust_runtime_smoke.py`: echte `Robust.dll` unter .NET 10 mit **isoliertem temporärem INI ohne Service-Connectoren** starten und nur den Loopback-HTTP-Listener prüfen; keine Verbindung zur Live-Datenbank.
- `.github/workflows/gen11-net10-build.yml`: Linux-x64-Full-Solution-Build, ROBUST-Runtime-Test und Paketlaufzeitprüfung.
- `.github/workflows/gen11-net10-build.yml`: Windows-x64-Full-Solution-Build und ROBUST-Runtime-Test.
- `.github/workflows/gen11-net10-arm64.yml`: Linux-ARM64-Full-Solution-Build, ROBUST-Runtime-Test und Paketlaufzeitprüfung.

Der ROBUST-HTTP-Smoke ist ein **Start- und Laufzeitnachweis**, nicht die Abnahme eines produktiven Login-/Inventory-/Asset-/Hypergrid-Szenarios. Diese benötigt im produktiven Grid eine isolierte MariaDB/FSAssets-Staging-Kopie und echte Viewer-/Simulator-End-to-End-Tests.

## Checkliste fuer eine sichere Produktionsmigration

1. Integrationsbetrieb mit MariaDB, FSAssets/WebDAV, Inventar, Login, Simulator, ScriptEngine (LSL/OSSL), Addons und Fremd-Hypergrid.
2. Konsistentes Backup, Restore und vollständig getesteter Rückweg auf die bisherige G1-Version.
3. Migration aller Deployment-/systemd-/Docker-/Paketpfade auf die `dotnet`-10-Runtime; keine unkontrollierten alten `net8.0`-Binaries im Release.
4. TLS-/World-API-/Codec-Staging-Abnahme gemäß `doc/GEN1_NET10_MAIN_MIGRATION.md`.

---

**English quick reference:** Generation 1 (0.9.4.0) uses the .NET 10 SDK and runtime, **not .NET 8**. Regenerate projects with `runprebuild.sh` on Linux or `runprebuild.bat` on Windows (both include the legacy-Prebuild retargeting step), then `dotnet build -c Release OpenSim.sln`. Run `tools/gen11/verify_net10.py` and the isolated ROBUST runtime smoke. Never point CI test configs at the live NexVerse database or replace production INI files automatically.
