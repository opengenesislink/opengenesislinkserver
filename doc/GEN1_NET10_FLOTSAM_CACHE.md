# Flotsam Asset Cache: .NET 10 format migration

The Generation 1 Flotsam simulator-side **file cache** previously used `BinaryFormatter` to persist `AssetBase`. `BinaryFormatter` cannot be used on .NET 10. This made old cache entries unreadable and attempted writes fail.

The fix implements a bounded, versioned `FLC1` disk-cache format with a SHA-256 payload checksum, preserving asset metadata and binary payload without reconstructing arbitrary object graphs. It intentionally **does not read the old BinaryFormatter format**. On encountering an obsolete entry, the simulator removes **that cache file only** and re-fetches it through the configured authoritative asset service (for example ROBUST FSAssets).

This is **not** a database or asset-store migration. Do **not** delete the central FSAssets directory, asset SQL tables, inventory, OAR/IAR files, or arbitrary `bin/` files.

## Immediate workaround while binaries are not yet deployed

In the active simulator's `[AssetCache]` configuration (typically `bin/config-include/FlotsamCache.ini`), set `FileCacheEnabled = false` and restart the simulator normally. This disables only Flotsam's file cache. It may increase ROBUST asset-service load. Re-enable after deploying the corrected binary.

## Cache path and cleanup

The checked-in setting `CacheDirectory = ./opt/cache/assetcache` is intentionally relative to the simulator's process working directory, hence `/opt/robust/bin/opt/cache/assetcache` when launched from `/opt/robust/bin`. To use an external absolute path instead, change the active setting to `CacheDirectory = /opt/cache/assetcache` **only after** checking directory permissions and that no other service uses that path. The format fix does **not** automatically move files or change operator-owned settings.

Existing stale cache files will be discarded on access. A controlled offline cache reset may accelerate reclamation, but only after the active cache directory is confirmed and it is verified to be exclusively a disposable simulator-side Flotsam cache.

## Regression

```bash
dotnet run --configuration Release --project tools/ci/FlotsamCacheNet10Regression/FlotsamCacheNet10Regression.csproj
```

This checks metadata and binary payload fidelity, rejected legacy entries, cache file disposal, wrong-ID rejection, tampering and truncation. It does not require MariaDB and never connects to a live grid.
