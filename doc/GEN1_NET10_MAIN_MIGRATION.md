# OpenGenesisLINK Generation 1: pure .NET 10 migration to main

**Status:** source migration ready for CI verification and staging acceptance. This is NOT a production deployment approval.

The upgrade is based on existing Generation 1 main commit `7a745a90b44b20d1317a5a1283b05cfd32fdf9c3`. It transfers only the reviewed Generation 1.1 **.NET 10 foundation**:

- `net10.0` project targets for ROBUST, OpenSim simulator, YEngine, services, city and OGL addons and regression tools; the compatible YEngine C# code change.
- `global.json` targets .NET 10; framework-dependent runtimeconfig files require Microsoft.NETCore.App 10.x.
- Historical bundled Prebuild DLL still generates net8.0 and is retargeted deterministically by the supported prebuild scripts. Do not run raw Prebuild without `runprebuild.sh`/`runprebuild.bat`.
- Main NexVerse CI and five addon workflows select .NET 10; Linux x64, ARM64 and Windows x64 smoke builds compile the server and start ROBUST from isolated temporary INIs.
- File manifest staging packages do not include active INIs, runtime secrets, MariaDB, FSAssets, inventory or existing regions.

**Explicitly excluded:** G1.1 Image Codec Engine, native OpenJPEG2 shim, codec provider/J2C modifications, additional World API/TLS restructuring or any modification of existing Grid/Region INI files.

## Release freeze during upgrade

All `.github/workflows/publish-*.yml` jobs require `vars.OGL_GEN1_NET10_RELEASE_APPROVED == 'true'`. This newly introduced repository-variable check is fail-closed if the variable is unset. **Do not set the variable during the source migration.** The rolling `gen1-main` development ZIP uses .NET 10 in its manifest but will not republish while the approval variable is unset. Immutable stable tags are not rewritten.

Source changes to GitHub `main` do **not** install new .NET versions, replace binaries on active servers, migrate a database, or restart a grid.

## Real-world acceptance before a release or production server update

1. Create and verify consistent, rollback-ready **offline backups** of MariaDB, FSAssets, avatar inventories, IAR/OAR archives, region files, estate references and all INI/deployment settings.
2. Test **complete ROBUST service configuration** with an isolated MariaDB copy. The existing headless ROBUST HTTP smoke is only a process start check and not a login test.
3. Start at least one full `OpenSim.dll` region under .NET 10; load region modules, physics and YEngine; confirm no missing native or NuGet dependencies.
4. Validate real Firestorm/TrixieStorm login, assets/meshes/textures, inventory, teleports, LSL/OSSL, groups/permissions, OGL addons and Hypergrid interoperability.
5. Exercise a tested restore and rollback of the previous G1 binary against separately preserved data, then explicitly approve publication/deployment.

Until these are complete, this is a **source/CI upgrade only**, not a tested production grid migration. Do not set automatic release approval or deploy it to the live Stadt-NexVerse grid.

## Operator commands

```bash
# In a disposable clone of the upgrade branch, not in the live installation:
bash runprebuild.sh
python3 tools/gen11/verify_net10.py
python3 tools/ci/verify_gen1_main_net10_scope.py
dotnet restore OpenSim.sln
dotnet build OpenSim.sln --configuration Release --no-restore
python3 tools/gen11/robust_runtime_smoke.py
```

Build requires .NET 10 SDK and an installed .NET 10 runtime. The test does not access production secrets.
