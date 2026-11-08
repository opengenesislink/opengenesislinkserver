# NexVerse Legacy Component Inventory

This document tracks inherited OpenSimulator components during the **NEXJAST** cleanup.

Status values:

- **KEEP** — currently required and healthy.
- **COMPAT** — retained specifically for viewer, Hypergrid, LSL or protocol compatibility.
- **REPLACE** — retained temporarily until a NexVerse-native replacement reaches parity.
- **REMOVE** — approved for deletion.
- **REMOVED** — deleted from the active NexVerse source/build/configuration.

| Component | Status | NexVerse direction |
| --- | --- | --- |
| XML-RPC RemoteAdmin | **REMOVED** | Replaced by the NexVerse World API/control plane. |
| VivoxVoiceModule | **REMOVED** | Replaced by planned centralized OGLVoice WebRTC/LiveKit architecture. |
| FreeSwitch voice stack | **REMOVED** | Replaced by planned centralized OGLVoice WebRTC/LiveKit architecture. |
| IRC bridge | **REMOVED** | No longer part of the NexVerse communications architecture. |
| Generated `obj/` trees | **REMOVED** | Build intermediates are excluded through `.gitignore`. |
| OptionalModules example modules | **REMOVED** | BareBones and WebSocket echo sample modules are not part of the production NexVerse source line. |
| pCampBot / PhysicsCamperbot | **REMOVED** | Unmaintained stand-alone multi-login stress bot; 16 source and 7 bin files, prebuild and solution references deleted. |
| LaunchSLClient / legacy external-grid presets | **REMOVED** | Old Windows Forms/VS2008 launcher and `bin/LaunchSLClient.ini` no longer belong to G1; TrixieStorm is a separate viewer repository. |
| `OpenSim/Tools/classaudit.pl` | **REMOVED** | One-off 2007 Perl source scanner, not referenced by current build/CI tooling. |
| LSL XML-RPC RemoteData | **COMPAT** | Must remain while required for LSL RemoteData compatibility. It is not RemoteAdmin. |
| LLLogin / viewer login protocol | **KEEP** | Required for compatible viewers including Firestorm. |
| LLUDP / viewer protocol stack | **KEEP** | Required for viewer compatibility. |
| Hypergrid HG1.5 services | **KEEP** | Required until/supplemented by a future explicitly versioned federation layer. |
| OpenID server | **REMOVED** | Legacy OpenID provider/connector removed. Optional LLLogin protocol fields remain until Firestorm runtime validation. |
| UserStatistics/WebStats | **REMOVED** | Legacy `/SStats`/SQLite/AJAX statistics stack removed; replacement is NexMetrics/OpenTelemetry/API observability. |
| OfflineIM | **REPLACE** | Keep until NexMessaging provides equivalent IM/group/HG behavior. |
| OpenSim Groups addon | **REPLACE** | Keep until NexGroups reaches viewer/HG parity. |
| JsonStore | **REPLACE** | Evaluate against LSL/Experience requirements before removal. |
| Legacy Search services | **REPLACE** | Planned NexSearch service. |
| Existing FSAssets | **KEEP** | Current authoritative asset backend; future NexAsset migration must be compatible. |

## Removal rule

A component marked **REPLACE** must not be deleted until its dependencies have been audited and the NexVerse replacement has passed the relevant compatibility tests.

## Protected compatibility rule

Protocol age alone is not sufficient justification for deletion. LSL, Firestorm and Hypergrid behavior must be preserved intentionally.

## NEXJAST first cleanup wave

Completed source cleanup:

- RemoteAdmin source, project, configuration and standalone plugin artifacts removed;
- Vivox source and configuration removed;
- FreeSwitch source, service project, connectors, handlers, interface, configuration and standalone binaries removed;
- IRC bridge source and configuration removed;
- tracked .NET `obj/` intermediate trees removed;
- legacy OpenID server/provider removed;
- legacy UserStatistics/WebStats module and static `/SStats` assets removed;
- NexVerse.Core and NexVerse.Server.Api introduced as native platform assemblies;
- non-production BareBones/WebSocketEcho example modules removed.

Build and runtime verification remains required before NEXJAST is considered complete.

## G1 cleanup audit – 2026-11-08 (OpenGenesisLINK)

The following is a **source/configuration inventory**, not proof of which
optional services are enabled on all production nodes. pCampBot, LaunchSLClient and classaudit have been explicitly approved for removal
in separate focused PRs. Delete no other components without
checking staging, compatibility and test dependencies.

| Component / path | Current G1 evidence | Recommended action | Removal gate |
| --- | --- | --- | --- |
| `OpenSim/Tools/pCampBot/`, `bin/pCampBot*` | Legacy independent login-stress program; explicitly compiles in main solution; includes tracked old executable, DLL, PDB and config. | **REMOVED in this cleanup** | Full simulator, ROBUST and generated-solution CI must remain green. |
| `OpenSim/Tools/LaunchSLClient/` | Standalone Windows Forms launcher; old `secondlife` executable lookup, Nant / VS2008 / .NET 3.5 scripts; not part of root `prebuild.xml` / root `OpenSim.sln`. | **REMOVED** | User-approved G1 cleanup; TrixieStorm is maintained separately. |
| `bin/LaunchSLClient.ini` | Legacy launcher configuration included outdated DeepGrid, OpenlifeGrid and Linden Lab shortcuts instead of Stadt NexVerse. | **REMOVED** | No legacy launcher configuration remains in active G1 source. |
| `OpenSim/Tools/classaudit.pl` | One-off 2007 Perl script for namespace/class scanning; hard-coded relative source path. | **REMOVED** | Future namespace lint should be implemented as a modern CI check, if needed. |
| `OpenSim/Region/CoreModules/World/LegacyMap/` | Alternate `MapImageModule` legacy terrain renderer; currently configured map renderer is `Warp3DImageModule`. | **MEDIUM – candidate, NOT removed** | Verify static image fallback, world-map compatibility, migrations and custom region overrides. |
| `OpenSim/Region/PhysicsModules/POS/` | Older optional `POS` physics engine; G1 runtime profile uses `BulletSim`. | **MEDIUM – candidate, NOT removed** | Scan deployed region INIs and test basic physics / simulator compatibility. |
| `OpenSim/Region/PhysicsModules/BasicPhysics/` | Incomplete minimal physics engine; its own comments cite regression-test usage. | **MEDIUM – candidate, NOT removed** | Identify test references and provide a replacement test fixture before deletion. |
| `OpenSim/Region/PhysicsModules/ubOde/` and `ubOdeMeshing/` | Alternative engine / mesher not selected by the default G1 profile. | **HIGH IMPACT – keep pending migration audit** | Verify *all* participating/federated region configs, physics behavior and release target requirements. |
| `bin/OpenSimDefaults.ini` `[XEngine]` / historic engine comments | Default runtime engine is `YEngine` but descriptions still claim XEngine-only support. | **LOW RISK – documentation cleanup candidate** | Update comments only; preserve legacy script-state/import compatibility until separately tested. |
| `OpenSim/Region/OptionalModules/Scripting/JsonStore/` | Older JSON state feature; current NEXJAST inventory already marks it **REPLACE**. | **COMPAT – retain** | Audit OSSL/LSL integrations and migrate state/scripts before replacing. |
| `OpenSim/Region/OptionalModules/DataSnapshot/` and legacy search modules | Historic search / DataSnapshot API, legacy dependencies may remain. | **REPLACE – examine** | Confirm the replacement search API and viewer/Hypergrid consumption. |
| `OpenSim/Addons/OfflineIM/`, `OpenSim/Addons/Groups/` | Existing IM/group implementations may still be required for viewer and Hypergrid compatibility. | **KEEP / REPLACE LATER** | Replacement must pass IM, group, inventory and HG regressions. |
| `addons/OGLWeather/OglWeatherLegacyEmitters.cs` | Runtime weather particles for legacy Firestorm viewers, explicitly used for compatibility. | **KEEP** | Only retire when all supported viewers can render replacement weather state. |
| `bin/` compiled tracked artifacts beyond pCampBot | Many checked-in runtime binaries, native libraries and .pdb files; not all are obsolete. | **AUDIT ONLY** | Establish full dependency/SBOM inventory and reproducible builds before individual removals. |
| `tools/ci/LegacyKeyframeFixture/`, `tools/migration/KeyframeLegacyConverter/` | Migration / regression utilities, not ordinary runtime services. | **KEEP FOR NOW** | Retire only after all migrated assets/states and downgrade scenarios are validated. |

### Scope and safety

- Removed in the earlier PR: only the pCampBot standalone tool, its tracked binaries
  and its two root build integrations.
- Removed in this PR: `LaunchSLClient/` (21 source/support files),
  `bin/LaunchSLClient.ini` and `OpenSim/Tools/classaudit.pl`.
- Deliberately not removed: OpenSim / Hypergrid protocols, LLUDP, YEngine,
  Firestorm compatibility, FSAssets, OGLWeather legacy viewers, region modules,
  optional physics fallback implementations and migration fixtures.
- Local deployments are **not** updated by source removal. A stale
  `/opt/robust/bin/pCampBot*` may remain after `git pull` if deployment
  copies files rather than synchronizing/deleting them; check explicitly.
- The remaining follow-up candidates above require distinct PRs and CI/simulator tests.
