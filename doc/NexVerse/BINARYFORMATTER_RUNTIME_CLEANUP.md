# OpenGenesisLINK G1: BinaryFormatter removal and YEngine state compatibility

**State:** new `.NET 10` safety changes are staged in a separate PR, not
merged or deployed to production.

## Runtime paths inventoried

| Area | Previously unsafe call | Current remediation |
| --- | --- | --- |
| KeyframeMotion | BinaryFormatter serializing persistent prim movements | Typed OGKF v1 and explicit offline NRBF converter in **PR #396** |
| YEngine script state | `Ser.SYSERIAL` / `Ser.THROWNEX` serialize/deserialize arbitrary CLR graphs | Allowlisted typed exception codec and dedicated `SYSERIALSAFE` / `THROWNEXSAFE` tags in this PR |
| Generic framework helpers | `Util.SerializeToFile`, `DeserializeFromFile` | No in-tree callers; fail closed with `NotSupportedException`. The two APIs stay as obsolete signatures to make external callers fail explicitly. |
| ubOdeMesh cache | Commented-out historical BinaryFormatter code | No active BinaryFormatter call; no mesh cache change in this PR |

## YEngine saved script state recovery requirements

YEngine legacy snapshots use a **different format** from the KeyframeMotion
BLOBs. Never run the KeyframeMotion converter against a YEngine `.state` file.

New YEngine state uses explicit, bounded serializations of known exception types.
Old `Ser.SYSERIAL` and `Ser.THROWNEX` records are **rejected** rather
than interpreted as .NET object graphs. The original persisted XML is kept:
startup does not delete the `.state` file, reset global variables or
rerun `state_entry` after a failed restore. State writes now use a
bounded 8-MiB XML document and atomic same-directory replacement.

**Existing legacy script state is NOT automatically migrated.** When a script
contains an unsupported historic exception graph, its restart must fail
loudly, preserving the original data. A runtime-side transparent migration
would reintroduce unsafe arbitrary object activation and is prohibited.

Before deployment: stop the staging simulator, snapshot all `.state`
files and inventories, restore a representative script archive on
`/opt/developer`, inspect which legacy states cannot load, and verify
critical scripts (economy, rentals, land, permissions, teleports, payments)
do not silently replay side effects. An approved per-state offline
conversion/restart policy is needed for such records.

This PR does **not** automatically rewrite or remove old script data, affect
the primary `main` branch or change the standalone Keyframe migration PR.

## Read-only inventory of saved YEngine scripts before staging

Run only against a copy of the YEngine `.state` directory:

```bash
python3 tools/ci/audit_yengine_states.py \
  --directory /path/to/backed-up-script-states \
  --report /tmp/ogl-yengine-state-audit.json
```

The report records file names relative to the supplied backup, SHA-256 of
the original XML, snapshot SHA-256, size and parsing status. It does not
contact the grid, database, object store or any remote URLs, and does not
alter original files. XML is limited to 8 MiB and DTD/entity declarations
are refused.

**Important:** YEngine migration snapshots are opaque binary event/state
streams. The audit cannot conclusively determine whether an individual
snapshot contains a `BinaryFormatter` graph without interpreting the
full format. It marks valid snapshots `inspect-required`, not
`legacy-detected`. The only valid compatibility verdict comes from a
controlled, backed-up staging restore; **do not assume all snapshots are
upgrade-safe because the audit succeeded.**

## Runtime and build-time unsafe opt-in removed

The historic Prebuild generator had an unconditional
`EnableUnsafeBinaryFormatterSerialization=true` setting in generated
projects. The checked-in `prebuild.dll` is older than the generator
source; it still emits the switch when `runprebuild.sh` runs.

The Generation 1.1 retarget step now **always replaces that generated
opt-in with false** before project restore/build, and `--check` rejects
an unsafe regenerated project. The generator source is also corrected
for future rebuilds. Runtime config templates for ROBUST and OpenSim and
the five checked-in application runtime configs explicitly disable unsafe
BinaryFormatter deserialization. All of this is checked by CI, including
a second check immediately *after* regeneration.

The intentionally isolated .NET 8 fixture for legacy KeyframeMotion
migration in PR #396 lives outside the generated project tree; its
compatibility setting is not enabled in any .NET 10 production process.

### Direct-build defense in depth

A repository-level `Directory.Build.targets` overrides stale checked-in
`.csproj` files when an operator runs `dotnet build` directly, bypassing
`runprebuild.sh`. ROBUST uses a nested `OpenSim/Server/Directory.Build.targets`;
that nested file now imports the root security policy. CI queries the *effective*
MSBuild property of OpenSim, ROBUST and YEngine after the Prebuild step.

## Additional safety: failed YEngine snapshots and OAR imports

A snapshot failure while `suspendOnCheckRunHold` was set could leave
a running script stuck. `XMRInstance.GetExecutionState` now clears
this suspension flag via `finally`, including errors from unsupported
historic objects and oversized state.

`Yengine.SetXMLState` (used for OAR/import/region-crossing state) previously
used unbounded `XmlDocument.LoadXml` and `File.Create`, which could
truncate a last known-good `.state` file before successfully writing the
replacement. It now uses the same DTD-free 8 MiB XML reader as ordinary
state restore, checks the YEngine base64 snapshot, and writes atomically.
Legacy XEngine imports retain their distinct Variables-based format.

Script timers are not changed until the YEngine binary snapshot was
decoded successfully. No automatic historic graph execution is enabled.

Import preflight also rejects YEngine snapshot headers whose migration version
does not match `XMRInstance.migrationVersion`. This protects existing state
files from obviously incompatible payloads; it **does not** prove that the
remaining object graph is valid or compatible. Only a controlled staged
restore with its compiled script can establish that. No unsafe object
deserialization is used in preflight.
