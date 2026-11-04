# OpenGenesisLINK G1.1 – .NET 10 Server-Runtime: Prüfstand 04.11.2026

**Status: G11-01 Build + Grundlaufzeit + Staging-Paketierung grün. Keine Produktionsfreigabe.**

Die .NET-10-Migration ist ein eigenständiges, verpflichtendes Fundament der Generation 1.1. Sie ist **nicht** an die optionale JPEG2000-Engine (G11-02) oder andere Feature-Addons gekoppelt.

## Was bereits tatsächlich umgesetzt ist

- .NET 10 SDK-Konfiguration über `global.json`; die Server-/Addon-Projektdateien sind auf `net10.0` ausgerichtet, und die Host-`runtimeconfig.json`-Dateien verlangen Microsoft.NETCore.App 10.
- `runprebuild.sh` und `runprebuild.bat` generieren die Projektdateien und korrigieren die `net8.0`-Ausgabe des gebündelten historischen Prebuild-Generators. Dieser Generator ist technisch noch ein Zwischenstand und darf nicht separat ohne Retargeting benutzt werden.
- ROBUST, Simulatoren und die in `OpenSim.sln` eingebundenen Addons werden unter .NET 10 auf Linux x64, Linux ARM64 und Windows x64 kompiliert.
- `tools/gen11/robust_runtime_smoke.py` startet die **echte** `Robust.dll` mit einer temporären INI und einem lokalen HTTP-Port ohne Datenbank, Service-Connectoren oder produktive Konfiguration. Der Prozess muss mit .NET 10 starten, eine HTTP-Anfrage beantworten und stabil weiterlaufen.
- `tools/gen11/stage_net10_runtime.py` erstellt plattformspezifische, **frameworkabhängige** Staging-Pakete mit SHA-256-Dateimanifest. Ein zweiter ROBUST-Starttest läuft aus **dem separaten Paket**, nicht aus dem Build-Arbeitsverzeichnis. Die optionale Codec-Bibliothek ist **keine** Voraussetzung für die G11-01-Pakete.
- Aktive INIs, `.env`, MariaDB-Daten, FSAssets, Regionsdaten, Logs, Caches und IAR-Archive werden nicht mitverpackt. Nur Beispielkonfigurationen, statische Defaults, Laufzeit-Assemblies und plattformspezifische Bibliotheken werden übernommen.

## Nachweis durch GitHub Actions

| Plattform | Gesamte Solution | ROBUST-HTTP-Start | Paket-Runtime-Start | Staging-Artefakt |
|---|---|---|---|---|
| Linux x64 | Bestanden | Bestanden | Bestanden | `ogl-g11-net10-core-linux-x64` |
| Linux ARM64 | Bestanden | Bestanden | Bestanden | `ogl-g11-net10-core-linux-arm64` |
| Windows x64 | Bestanden | Bestanden | Bestanden | `ogl-g11-net10-core-windows-x64` |

Vollständig geprüfter Stand: `9ebed4e75ff5b43ae7bf8b81d7980ea540196029`.

- [Linux x64 + Windows x64: .NET 10 Core Build und Runtime-Pakete](https://github.com/opengenesislink/opengenesislinkserver/actions/runs/41842109638)
- [Linux ARM64: .NET 10 Core Build und Runtime-Paket](https://github.com/opengenesislink/opengenesislinkserver/actions/runs/41842109747)

Die Actions-Artefakte sind **Staging-Builds**, keine Produktions-Releases und keine selbstständig ausführbaren .NET-10-Runtime-Installer. Auf dem Zielsystem muss die passende **.NET-10-Runtime** installiert sein. Die Quellkonfiguration ist vor einem echten Start manuell an eine isolierte Testumgebung anzupassen.

## Wiederholbarer manueller Staging-Test

```bash
# Im G1.1 Git-Checkout, SDK + .NET 10 Runtime installiert
bash runprebuild.sh
dotnet restore OpenSim.sln
dotnet build OpenSim.sln -c Release --no-restore
python3 tools/gen11/verify_net10.py
python3 tools/gen11/robust_runtime_smoke.py
python3 tools/gen11/stage_net10_runtime.py \
  --rid linux-x64 --output /tmp/ogl-g11-net10-stage
python3 tools/gen11/robust_runtime_smoke.py \
  --bin /tmp/ogl-g11-net10-stage/bin
```

ARM64: `--rid linux-arm64`; Windows: `--rid win-x64` und `python` statt `python3`. Das Ausgabeziel muss vorher nicht existieren und außerhalb des Source-Checkouts liegen. Zur Vermeidung unbeabsichtigter Überschreibungen verweigert der Stager bestehende Ziele.

## Was dieser Nachweis ausdrücklich **noch nicht** beweist

- Der **Simulator** wurde unter .NET 10 erfolgreich kompiliert; sein kompletter **Regionstart und eine echte Viewer-Sitzung** sind noch nicht im CI bewiesen.
- ROBUST wird ohne Datenbank und ohne Dienstconnectoren gestartet. Ein komplettes Grid mit **MariaDB**, AssetService, Inventar, Hypergrid, World API, Gruppen, NPCs und echten Usern ist noch nicht im Staging durchgetestet.
- IAR-/WebDAV-/LSL-/OSSL-/Addons-Integration, Transportsicherheit, Performance und Lastverhalten sowie Backend-Datenintegrität bleiben separate G11-05/G11-06-Abnahmekriterien.
- Der mitgelieferte Prebuild-Binary-Generator erzeugt intern noch `net8.0`; die unterstützten Scripts beheben diese Generierung unmittelbar. Ein modernes, direktes `net10_0`-Prebuild-Binary wäre eine Verbesserung, darf aber keine produktiven Serveränderungen erzwingen.

**Nächster G11-01/G11-06-Pflichtschritt:** Isoliertes Staging mit einer konsistenten MariaDB-/FSAssets-Kopie, vollständigem ROBUST-Servicegraphen, mindestens einer OpenSim-Region und realem Firestorm-/TrixieStorm-Login. Danach Backup/Restore, Rollback und eine dokumentierte Kompatibilitätsmatrix. Bis dahin bleibt `main` unverändert und keine G1.1-Binary wird automatisch live bereitgestellt.
