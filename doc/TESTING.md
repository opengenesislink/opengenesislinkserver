# OpenGenesisLINK Generation 1 – Tests mit .NET 10

Der alte NAnt-/NUnit-Console-Testweg ist nicht mehr das unterstuetzte
Build- und Testverfahren. Die Server-Loesung wird mit **.NET 10** gebaut.
Einen frischen Quellstand vor Tests mit `runprebuild.sh` erzeugen;
`global.json` legt die SDK-Version fest.

## Lokaler Build

```bash
bash runprebuild.sh
python3 tools/gen11/verify_net10.py
dotnet restore OpenSim.sln
dotnet build OpenSim.sln --configuration Release --no-restore
```

Alternativ kann auf Linux mit `make build` dieselbe Buildfolge
ausgefuehrt werden.

## Kernregressionen

```bash
dotnet run --configuration Release --project tools/ci/PublicSimulatorVersionRegression/PublicSimulatorVersionRegression.csproj
dotnet run --configuration Release --project tools/ci/FlotsamCacheNet10Regression/FlotsamCacheNet10Regression.csproj
```

Mit `make test` werden diese Tests nach dem Release-Build
ausgefuehrt.

## ROBUST-Laufzeittest

```bash
python3 tools/gen11/robust_runtime_smoke.py
```

Der Test verwendet eine isolierte Testkonfiguration ohne
produktive Service-Connectoren. Er ersetzt keine manuellen
E2E-Abnahmen fuer Viewer, Login, Regionen, Inventar,
Hypergrid, OGL-Addons oder MariaDB.

## GitHub Actions

Die wichtigsten Generation-1-Gates liegen unter `.github/workflows/`:

- `gen11-net10-build.yml`: Linux-/Windows-Build und ROBUST-Runtime.
- `gen11-net10-arm64.yml`: ARM64-Build und Laufzeit.
- `nexjast-ci.yml`: Server- und API-Regressionen sowie
  Datenbank-Integrationspruefungen.

Viele Datenbanktests benoetigen **eigene Testdatenbanken** und
Umgebungsvariablen. Diese niemals ohne Staging-Kontrolle auf
eine Produktionsdatenbank zeigen lassen.

Weitere Informationen: https://opengenesislink.de
