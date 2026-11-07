# Generation 1 / .NET 10 – Keyframe persistence and bounded shutdown

## Serialization

The legacy `KeyframeMotion` used `BinaryFormatter`, removed by the .NET runtime.
New and updated keyframe motion is written to a bounded, typed **OGKF v1** format.
The serializer covers the movement mode, data flags, saved/base transform, active
frame, frame queues, iteration and running state. Deserialization checks version,
frame count, payload length, finite numeric components and trailing data.

Existing BinaryFormatter records are **not** deserialized or executed in .NET 10.
Recognized, bounded records are preserved byte-for-byte during ordinary object
backups, rather than silently replaced by empty bytes. Such movements remain
inactive until the data is migrated; no automatic conversion can safely recover
an arbitrary BinaryFormatter object graph. Make a consistent DB/OAR backup first.
Any legacy offline conversion requires a trusted, isolated older-runtime converter
with a strict type allowlist. Do not deserialize untrusted objects.

## Region shutdown

During shutdown, each region receives its own close operation and error report.
Exceptions no longer prevent closing subsequent regions or disposing plugins.
`[OGL SHUTDOWN]` logs identify FastMap, grid/client phase, forced backup and
physics disposal; this helps identify the exact blocking operation.

`[OGLShutdown]` / `RegionCloseTimeoutSeconds` controls the per-region
fallback; default **180 seconds**, minimum 0, maximum 3600. Zero restores
unbounded synchronous close (safest when writes must always finish).
With a nonzero limit a timed-out region is NOT cancelled or considered closed:
its background worker can still be running while the manager moves to the next
region. The process may then exit before that region finishes persisting;
an operator must check logs and backups before reusing the region. No forceful
thread abort or deletion of region objects is performed.

It is not possible to guarantee a complete shutdown for blocked storage/network
calls in a process with arbitrary plugins. Do NOT treat timeout as successful
persistence. To find the exact blocking stage, collect a live dotnet-dump
on the staging process when the timeout occurs and inspect the worker stacks.
Test in staging with 40+ regions, FastMap rendering, active scripts,
blocked grid/SQL connections, connected avatars and repeated stop/start cycles.

## UDP disconnect

The packet ACK queues are replaced with empty instances instead of assigned
`null`. Late acknowledgements/packet callbacks can no longer throw solely
because the queues were nulled out on disconnect. Concurrent packet cleanup
still needs an in-world high-churn stress test.

## Headless main-thread shutdown order

`OpenSimBackground` previously signalled the main startup waiter *before* its
base shutdown completed. The completion event is now signalled from a
`finally` block **after** application cleanup, preventing the main
thread from treating an in-progress shutdown as completed.

## Shutdown service exit status and console loop

`SceneManager.Close(int)` now reports a boolean result. A scene timeout,
exception or plugin-disposal failure marks the simulator's exit status nonzero
instead of falsely reporting success to systemd. This does not recover unsaved
data; operators must inspect failed region names before restarting.

The console command loop stops requesting new prompts after a shutdown
request. The normal process still exits through the inherited server shutdown
handler after cleanup.
