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
