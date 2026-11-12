using System;
using System.IO;
using System.Security.Cryptography;
using OpenSim.Data;
using System.Threading;
using OpenMetaverse;
using Nini.Config;
using OpenSim.Services.FSAssetService;
using OpenSim.Framework;
using OpenSim.Framework.Serialization.External;
using OpenSim.Services.Interfaces;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Compatibility-first OGL AssetService entry point.
    /// Delegates all asset operations to the existing FSAssetConnector.
    /// No migration, relocation or deletion of existing fsassets occurs.
    /// </summary>
    public sealed class OGLAssetConnector : FSAssetConnector, IAssetService
    {
        private static readonly log4net.ILog OglLog =
            log4net.LogManager.GetLogger(typeof(OGLAssetConnector));

        private readonly OGLObjectCache cache;
        private readonly OGLDiskCache diskCache;
        private readonly object cacheSync = new object();
        private readonly OGLRedisAssetCoordinator redis;
        private readonly bool redisEnabled;
        private readonly bool localCacheEnabled;
        private readonly bool atomicUpdatesEnabled;
        private readonly OGLParallelAssetStore parallelStore;
        private readonly OGLS3ObjectStore s3Store;
        private readonly bool parallelReadPreferred;
        private long parallelMirrored;
        private long parallelReadHits;
        private long parallelReadMisses;
        private long parallelWriteFailures;
        private long redisInvalidationFailures;
        private long atomicUpdateAttempts;
        private long atomicUpdateSucceeded;
        private long atomicUpdateRejected;
        private long atomicUpdateExceptions;
        private long atomicUpdateElapsedTicks;
        private long localInvalidationAttempts;
        private long diskInvalidationFailures;

        // Read-only operational counters; no asset IDs or payloads are exposed.
        public long AtomicUpdateAttempts => System.Threading.Interlocked.Read(ref atomicUpdateAttempts);
        public long AtomicUpdateSucceeded => System.Threading.Interlocked.Read(ref atomicUpdateSucceeded);
        public long AtomicUpdateRejected => System.Threading.Interlocked.Read(ref atomicUpdateRejected);
        public long AtomicUpdateExceptions => System.Threading.Interlocked.Read(ref atomicUpdateExceptions);
        public long AtomicUpdateElapsedTicks => System.Threading.Interlocked.Read(ref atomicUpdateElapsedTicks);
        public long LocalInvalidationAttempts => System.Threading.Interlocked.Read(ref localInvalidationAttempts);
        public long DiskInvalidationFailures => System.Threading.Interlocked.Read(ref diskInvalidationFailures);
        public bool ExperimentalAtomicUpdatesEnabled => atomicUpdatesEnabled;
        public bool LocalCacheEnabled => localCacheEnabled;
        public bool RedisCoordinationEnabled => redisEnabled;
        public long RedisInvalidationFailures => System.Threading.Interlocked.Read(ref redisInvalidationFailures);
        public long MemoryCacheHits => cache.Hits;
        public long MemoryCacheMisses => cache.Misses;
        public long MemoryCacheUsedBytes => cache.UsedBytes;
        public long DiskCacheHits => diskCache?.Hits ?? 0;
        public long DiskCacheMisses => diskCache?.Misses ?? 0;
        public long DiskCacheEvictions => diskCache?.Evictions ?? 0;
        public bool ParallelStoreEnabled => parallelStore != null;
        public bool S3StorageEnabled => s3Store != null;
        // Operator-local diagnostics; no S3 endpoint or credentials are exposed.
        public string S3HealthStatus => s3Store?.HealthStatus ?? "disabled";
        public long S3HealthChecks => s3Store?.HealthChecks ?? 0;
        public long S3HealthFailures => s3Store?.HealthFailures ?? 0;
        public long ParallelMirrored => Interlocked.Read(ref parallelMirrored);
        public long ParallelReadHits => Interlocked.Read(ref parallelReadHits);
        public long ParallelReadMisses => Interlocked.Read(ref parallelReadMisses);
        public long ParallelWriteFailures => Interlocked.Read(ref parallelWriteFailures);
        public OGLAssetConnector(IConfigSource config, string configName)
            : base(config, configName)
        {
            IConfig settings = config.Configs["OGLAssetService"];
            if (settings == null || !settings.GetBoolean("Enabled", false))
                throw new InvalidOperationException(
                    "OGL AssetService is disabled. Set [OGLAssetService] Enabled = true only on a test instance.");

            int cacheMiB = settings.GetInt("MemoryCacheMiB", 64);
            if (cacheMiB < 0 || cacheMiB > 4096)
                throw new ArgumentOutOfRangeException("MemoryCacheMiB", "Must be between 0 and 4096 MiB.");
            // Process-local cache entries cannot be invalidated by other writers.
            localCacheEnabled = settings.GetBoolean("SingleWriterCacheEnabled", false);
            atomicUpdatesEnabled = settings.GetBoolean("ExperimentalAtomicUpdatesEnabled", false);
            cache = new OGLObjectCache((long)cacheMiB * 1024 * 1024);
            if (settings.GetBoolean("DiskCacheEnabled", false) && !localCacheEnabled)
                throw new InvalidOperationException(
                    "DiskCacheEnabled requires SingleWriterCacheEnabled=true and exclusive asset writers.");
            if (settings.GetBoolean("DiskCacheEnabled", false))
            {
                int diskMiB = settings.GetInt("DiskCacheMaxMiB", 1024);
                if (diskMiB < 1 || diskMiB > 1048576)
                    throw new ArgumentOutOfRangeException("DiskCacheMaxMiB", "Must be between 1 and 1048576 MiB.");
                diskCache = new OGLDiskCache(
                    settings.GetString("DiskCacheDirectory", ""),
                    (long)diskMiB * 1024 * 1024);
            }

            redisEnabled = settings.GetBoolean("RedisEnabled", false);
            if (redisEnabled && localCacheEnabled)
                throw new InvalidOperationException(
                    "SingleWriterCacheEnabled and RedisEnabled cannot be combined until distributed invalidation is verified.");
            if (redisEnabled)
            {
                string redisConnection = Environment.GetEnvironmentVariable("OGL_ASSET_REDIS_CONNECTION");
                if (String.IsNullOrWhiteSpace(redisConnection))
                    throw new InvalidOperationException("OGL_ASSET_REDIS_CONNECTION must be set when RedisEnabled=true.");
                redis = new OGLRedisAssetCoordinator(redisConnection, settings.GetInt("RedisDatabase", 2));
            }

            bool parallelEnabled = settings.GetBoolean("ParallelStoreEnabled", false);
            parallelReadPreferred = settings.GetBoolean("ParallelReadPreferred", false);
            if (parallelReadPreferred && !parallelEnabled)
                throw new InvalidOperationException("ParallelReadPreferred requires ParallelStoreEnabled.");
            if (parallelEnabled)
            {
                if (localCacheEnabled || redisEnabled || atomicUpdatesEnabled)
                    throw new InvalidOperationException(
                        "Parallel mode requires local cache, Redis and experimental atomic updates to remain off.");
                string objects = settings.GetString("ParallelObjectDirectory", "");
                string index = settings.GetString("ParallelIndexPath", "");
                if (String.IsNullOrWhiteSpace(objects) || String.IsNullOrWhiteSpace(index))
                    throw new InvalidOperationException("Dedicated ParallelObjectDirectory and ParallelIndexPath are required.");
                string fsbase = Path.GetFullPath(m_FSBase);
                string spool = Path.GetFullPath(m_SpoolDirectory);
                string objectPath = Path.GetFullPath(objects);
                string indexPath = Path.GetFullPath(index);
                if (OGLParallelAssetStore.IsInside(objectPath, fsbase) ||
                    OGLParallelAssetStore.IsInside(objectPath, spool) ||
                    OGLParallelAssetStore.IsInside(fsbase, objectPath) ||
                    OGLParallelAssetStore.IsInside(spool, objectPath) ||
                    OGLParallelAssetStore.IsInside(indexPath, fsbase) ||
                    OGLParallelAssetStore.IsInside(indexPath, spool))
                    throw new InvalidOperationException(
                        "Parallel OGL storage must be disjoint from the authoritative FSAssets directories.");
                parallelStore = new OGLParallelAssetStore(objectPath, indexPath);
            }

            string mode = settings.GetString("Mode", "compatibility");
            if (!String.Equals(mode, "compatibility", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("OGL AssetService only supports Mode=compatibility; use StorageBackend=s3 for explicit S3 opt-in.");

            // Two independent opt-in switches avoid accidental S3 activation
            // when a future configuration section is copied to a live grid.
            string selectedBackend = settings.GetString("StorageBackend", "fsassets").Trim();
            OGLS3StorageOptions s3 = OGLS3StorageOptions.Read(config);
            if (!String.Equals(selectedBackend, "fsassets", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(selectedBackend, "s3", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("OGL AssetService StorageBackend must be fsassets or s3.");
            bool s3Selected = String.Equals(selectedBackend, "s3", StringComparison.OrdinalIgnoreCase);
            if (s3Selected != s3.Enabled)
                throw new InvalidOperationException(
                    "OGL S3 requires BOTH [OGLAssetService] StorageBackend=s3 and [OGLS3Storage] Enabled=true; no implicit fallback.");
            if (s3Selected)
            {
                if (parallelStore != null || atomicUpdatesEnabled)
                    throw new InvalidOperationException("S3 is incompatible with ParallelStoreEnabled or legacy ExperimentalAtomicUpdatesEnabled.");
                s3Store = new OGLS3ObjectStore(s3);
                try
                {
                    s3Store.CheckBucket();
                    // One-shot probe always; optional periodic health checks
                    // start only with both S3 opt-ins and HealthCheckEnabled.
                    s3Store.StartHealthMonitoring();
                }
                catch
                {
                    s3Store.Dispose();
                    throw; // A missing/unreachable bucket must prevent startup.
                }
            }
            if (!s3Selected)
            {
                // Preserve the legacy startup diagnostics verbatim. Existing
                // ROBUST smoke checks and operational monitors depend on them.
                OglLog.InfoFormat(
                    "[OGL-ASSETS]: OGL AssetService geladen; authoritative=FSAssets; " +
                    "mode=compatibility; local_cache={0}; redis_coordination={1}; atomic_updates={2}",
                    localCacheEnabled, redisEnabled, atomicUpdatesEnabled);
                OglLog.InfoFormat(
                    "[OGL-ASSETS]: parallel_store={0}; parallel_read_preferred={1}; authoritative=FSAssets",
                    parallelEnabled, parallelReadPreferred);
            }
            else
            {
                OglLog.InfoFormat(
                    "[OGL-ASSETS]: OGL AssetService geladen; authoritative=S3; mode=compatibility; " +
                    "local_cache={0}; redis_coordination={1}", localCacheEnabled, redisEnabled);
            }
        }

        /// <summary>
        /// Creates a role-gated local administration view. The ROBUST host
        /// must authenticate the caller and supply a trusted ClaimsPrincipal.
        /// No HTTP route is registered by this method.
        /// </summary>
        public OGLAssetServiceAdmin CreateAdmin(System.Security.Claims.ClaimsPrincipal principal)
            => new OGLAssetServiceAdmin(this, principal);

        /// <summary>
        /// Obtain a point-in-time diagnostics snapshot without exposing
        /// credentials, asset identifiers or payload contents.
        /// </summary>
        public OGLAssetServiceDiagnostics GetDiagnostics()
        {
            return new OGLAssetServiceDiagnostics(
                atomicUpdatesEnabled, localCacheEnabled, redisEnabled,
                AtomicUpdateAttempts, AtomicUpdateSucceeded, AtomicUpdateRejected,
                AtomicUpdateExceptions, AtomicUpdateElapsedTicks,
                MemoryCacheHits, MemoryCacheMisses, MemoryCacheUsedBytes,
                DiskCacheHits, DiskCacheMisses, DiskCacheEvictions,
                LocalInvalidationAttempts, DiskInvalidationFailures,
                RedisInvalidationFailures,
                ParallelStoreEnabled, parallelReadPreferred, ParallelMirrored,
                ParallelReadHits, ParallelReadMisses, ParallelWriteFailures);
        }

        private void TryInvalidateRedis(string id)
        {
            if (!redisEnabled || String.IsNullOrWhiteSpace(id)) return;
            try { redis.InvalidateAsync(id).GetAwaiter().GetResult(); }
            catch (Exception)
            {
                // The authoritative write already succeeded. Never report it as failed.
                // Distributed caches remain disabled until a durable outbox exists.
                System.Threading.Interlocked.Increment(ref redisInvalidationFailures);
            }
        }

        // Keep all virtual read entry points serialized with local writes.
        // Base.Get may populate an asset from fallback storage through Store;
        // Monitor locks are reentrant, so this remains safe on the same thread.
        // This does NOT provide cross-process transactional isolation.
        public override AssetBase Get(string id)
        {
            lock (cacheSync)
            {
                if (s3Store != null)
                {
                    string hash;
                    AssetMetadata metadata = m_DataConnector.Get(id, out hash);
                    if (metadata == null) return null;
                    byte[] bytes = ReadS3Data(id, hash);
                    if (bytes == null)
                        throw new InvalidDataException("S3 payload is missing for an existing asset record.");
                    var result = new AssetBase { Metadata = metadata, Data = bytes };
                    if (result.Type == (int)AssetType.Object)
                        result.Data = Utils.StringToBytes(
                            ExternalRepresentationUtils.SanitizeXml(Utils.BytesToString(bytes)));
                    return result;
                }
                if (parallelStore != null && parallelReadPreferred)
                {
                    // The SQL metadata remains authoritative; never resurrect
                    // an asset deleted from FSAssets or return stale OGL bytes.
                    string hash;
                    AssetMetadata metadata = m_DataConnector.Get(id, out hash);
                    if (metadata != null)
                    {
                        byte[] bytes = TryReadParallel(id, hash);
                        if (bytes != null)
                        {
                            var asset = new AssetBase { Metadata = metadata, Data = bytes };
                            if (asset.Type == (int)AssetType.Object)
                            {
                                string xml = ExternalRepresentationUtils.SanitizeXml(Utils.BytesToString(bytes));
                                asset.Data = Utils.StringToBytes(xml);
                            }
                            return asset;
                        }
                    }
                }
                return base.Get(id);
            }
        }

        public override AssetMetadata GetMetadata(string id)
        {
            lock (cacheSync) return base.GetMetadata(id);
        }

        public override bool[] AssetsExist(string[] ids)
        {
            lock (cacheSync)
            {
                if (s3Store == null) return base.AssetsExist(ids);
                if (ids == null) return Array.Empty<bool>();
                var results = new bool[ids.Length];
                for (int i = 0; i < ids.Length; i++)
                {
                    if (!Guid.TryParse(ids[i], out Guid guid) || guid == Guid.Empty)
                        continue;
                    string hash;
                    if (m_DataConnector.Get(ids[i], out hash) != null)
                        results[i] = s3Store.Exists(hash); // HEAD, fail closed on 403/timeout
                }
                return results;
            }
        }

        public override byte[] GetData(string id)
        {
            if (s3Store != null)
            {
                lock (cacheSync)
                {
                    if (String.IsNullOrWhiteSpace(id)) return null;
                    string hash;
                    if (m_DataConnector.Get(id, out hash) == null) return null;
                    byte[] bytes = ReadS3Data(id, hash);
                    if (bytes == null)
                        throw new InvalidDataException("S3 payload is missing for an existing asset record.");
                    return bytes;
                }
            }
            if (String.IsNullOrWhiteSpace(id)) return base.GetData(id);
            lock (cacheSync)
            {
                // Redis connectivity or revision failures must never return stale cached data.
                // In this experimental mode all reads bypass local caches until a
                // distributed writer protocol has been verified end-to-end.
                if (parallelStore != null && parallelReadPreferred)
                {
                    string hash;
                    if (m_DataConnector.Get(id, out hash) == null)
                        return null;
                    byte[] bytes = TryReadParallel(id, hash);
                    return bytes ?? base.GetData(id);
                }
                if (redisEnabled || !localCacheEnabled) return base.GetData(id);
                if (cache.TryGet(id, out byte[] result)) return result;
                if (diskCache != null)
                {
                    try
                    {
                        if (diskCache.TryGet(id, out result))
                        {
                            cache.Put(id, result);
                            return result;
                        }
                    }
                    catch (System.IO.IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                result = base.GetData(id);
                if (result != null && !String.IsNullOrEmpty(id))
                {
                    cache.Put(id, result);
                    try { diskCache?.Put(id, result); }
                    catch (System.IO.IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                return result;
            }
        }

        // The legacy base UpdateContent is nonvirtual and remains unchanged.
        // Explicit IAssetService dispatch is the opt-in update entry point.
        bool IAssetService.UpdateContent(string id, byte[] data)
        {
            lock (cacheSync)
            {
                // Keep the legacy compatibility path unchanged. Counters only
                // describe the explicitly enabled atomic update feature.
                long started = 0;
                if (atomicUpdatesEnabled)
                {
                    System.Threading.Interlocked.Increment(ref atomicUpdateAttempts);
                    started = System.Diagnostics.Stopwatch.GetTimestamp();
                }
                try
                {
                    bool updated = s3Store != null
                        ? TryUpdateS3Content(id, data)
                        : atomicUpdatesEnabled
                            ? TryUpdateContentAtomic(id, data)
                            : base.UpdateContent(id, data);
                    if (atomicUpdatesEnabled)
                    {
                        if (updated)
                            System.Threading.Interlocked.Increment(ref atomicUpdateSucceeded);
                        else
                            System.Threading.Interlocked.Increment(ref atomicUpdateRejected);
                    }
                    if (updated && parallelStore != null)
                        TryMirrorCommitted(id);
                    return updated;
                }
                catch (Exception)
                {
                    if (atomicUpdatesEnabled)
                        System.Threading.Interlocked.Increment(ref atomicUpdateExceptions);
                    throw;
                }
                finally
                {
                    if (atomicUpdatesEnabled)
                        System.Threading.Interlocked.Add(ref atomicUpdateElapsedTicks,
                            System.Diagnostics.Stopwatch.GetTimestamp() - started);
                    // Invalidate even when disk staging, SQL CAS or normalization
                    // throws. The caller must not retain a potentially stale copy.
                    InvalidateLocal(id);
                    // The SQL write may have committed before a subsequent error.
                    // Redis is a best-effort hint; Redis-backed reads are disabled.
                    if (atomicUpdatesEnabled) TryInvalidateRedis(id);
                }
            }
        }

        public override bool Delete(string id)
        {
            lock (cacheSync)
            {
                try
                {
                    // S3 objects are immutable/content-addressed and may be
                    // shared by several UUIDs. Only the authorized metadata
                    // mapping is removed; physical GC is a separate offline task.
                    return s3Store == null ? base.Delete(id) : m_DataConnector.Delete(id);
                }
                finally
                {
                    // Legacy Delete reports success even when metadata removal
                    // fails; never use that boolean as cache-coherence evidence.
                    InvalidateLocal(id);
                    TryInvalidateRedis(id);
                }
            }
        }

        public override string Store(AssetBase asset)
        {
            lock (cacheSync)
            {
                string originalId = asset?.ID;
                string id = null;
                try
                {
                    id = s3Store == null ? base.Store(asset) : StoreS3Asset(asset);
                    if (parallelStore != null &&
                        Guid.TryParse(id, out Guid committedId) && committedId != Guid.Empty)
                        TryMirrorCommitted(id);
                    return id;
                }
                finally
                {
                    // FSAssetConnector can write payload bytes before metadata
                    // commit and can also assign an ID during Store. Even if
                    // it throws, never retain potentially obsolete local data.
                    InvalidateLocal(originalId);
                    InvalidateLocal(asset?.ID);
                    InvalidateLocal(id);
                    // Redis remains a best-effort hint only; distributed cache
                    // reads are disabled until all writers use a durable protocol.
                    if (!String.IsNullOrWhiteSpace(id) &&
                        !String.Equals(id, "00000000-0000-0000-0000-000000000000",
                            StringComparison.OrdinalIgnoreCase))
                        TryInvalidateRedis(id);
                }
            }
        }

        // The same SQL metadata provider as FSAssets retains UUID, type,
        // name, description, flags, creation date and creator. Only immutable
        // payload bytes move to the explicitly selected S3 backend.
        private string StoreS3Asset(AssetBase asset)
        {
            if (asset?.Metadata == null || asset.Data == null)
                return UUID.Zero.ToString();
            if (String.IsNullOrWhiteSpace(asset.ID) || asset.FullID.IsZero())
            {
                if (!Guid.TryParse(asset.ID, out Guid parsed) || parsed == Guid.Empty)
                {
                    asset.FullID = UUID.Random();
                    asset.ID = asset.FullID.ToString();
                }
                else
                    asset.ID = parsed.ToString("D");
            }
            if (!Guid.TryParse(asset.ID, out Guid uuid) || uuid == Guid.Empty)
                throw new ArgumentException("S3 assets require a valid nonzero UUID.");
            if (asset.Name.Length > AssetBase.MAX_ASSET_NAME)
                asset.Name = asset.Name.Substring(0, AssetBase.MAX_ASSET_NAME);
            if (asset.Description.Length > AssetBase.MAX_ASSET_DESC)
                asset.Description = asset.Description.Substring(0, AssetBase.MAX_ASSET_DESC);

            string hash = s3Store.Put(asset.Data); // Must complete before SQL commit.
            if (!m_DataConnector.Store(asset.Metadata, hash))
                return UUID.Zero.ToString(); // Orphan blobs are safe; never return success.
            string committed;
            AssetMetadata verified = m_DataConnector.Get(asset.ID, out committed);
            if (verified == null || !String.Equals(hash, committed, StringComparison.Ordinal))
                throw new IOException("Concurrent S3 asset metadata change; write result is uncertain.");
            return asset.ID;
        }

        private byte[] ReadS3Data(string id, string hash)
        {
            // Cache keys include the authoritative SQL digest. A changed UUID
            // mapping cannot return stale bytes from RAM or disk.
            string key = id + ":" + hash;
            byte[] result = null;
            if (localCacheEnabled && !redisEnabled)
            {
                if (!cache.TryGet(key, out result) && diskCache != null)
                {
                    try
                    {
                        if (diskCache.TryGet(key, out result))
                            cache.Put(key, result);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            if (result == null)
                result = s3Store.Get(hash);

            // Remote writers can change or delete an asset after the initial
            // UUID->hash lookup, even while this instance holds cacheSync.
            // Always recheck SQL before returning or populating a cache.
            string freshHash;
            if (m_DataConnector.Get(id, out freshHash) == null ||
                !String.Equals(hash, freshHash, StringComparison.OrdinalIgnoreCase))
            {
                cache.Invalidate(key);
                try { diskCache?.Invalidate(key); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw new IOException("OGL S3 asset metadata changed during read; caller must retry.");
            }

            if (result != null && localCacheEnabled && !redisEnabled)
            {
                cache.Put(key, result);
                try { diskCache?.Put(key, result); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            // No FSAssets fallback on missing S3 objects or I/O failures.
            return result;
        }

        private bool TryUpdateS3Content(string id, byte[] data)
        {
            if (String.IsNullOrWhiteSpace(id) || data == null ||
                !(m_DataConnector is IFSAssetHashCompareExchange compareExchange))
                return false;
            string oldHash;
            if (m_DataConnector.Get(id, out oldHash) == null)
                return false;
            // No write-back without a valid previously committed object.
            byte[] previous = s3Store.Get(oldHash);
            if (previous == null)
                throw new InvalidDataException("S3 update rejected: existing payload missing.");
            string nextHash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            if (String.Equals(oldHash, nextHash, StringComparison.Ordinal))
                return true;
            string storedHash = s3Store.Put(data);
            // CAS prevents lost updates. A failed CAS leaves an immutable,
            // unreferenced blob for a future explicit offline GC.
            return compareExchange.TryReplaceHash(id, oldHash, storedHash);
        }

        // Reads use a SQL hash guard both before and after loading the OGL
        // object. If the directory is absent, corrupt or unavailable, the
        // authoritative FSAssets read is always used as fallback.
        private byte[] TryReadParallel(string id, string hash)
        {
            try
            {
                byte[] result = parallelStore.TryRead(id, hash);
                if (result != null)
                {
                    string freshHash;
                    if (m_DataConnector.Get(id, out freshHash) != null &&
                        String.Equals(hash, freshHash, StringComparison.OrdinalIgnoreCase))
                    {
                        Interlocked.Increment(ref parallelReadHits);
                        return result;
                    }
                }
            }
            catch (Exception error)
            {
                OglLog.Warn("[OGL-ASSETS]: Parallel read failed; using authoritative FSAssets.", error);
            }
            Interlocked.Increment(ref parallelReadMisses);
            return null;
        }

        // FSAssets commits first; mirror failure is not permitted to turn a
        // successful asset upload into an API error. The unmigrated asset stays
        // fully accessible through FSAssets and can be reconciled offline.
        private void TryMirrorCommitted(string id)
        {
            try
            {
                string expectedHash;
                if (m_DataConnector.Get(id, out expectedHash) == null)
                    return;
                byte[] source = base.GetData(id);
                if (source == null || !parallelStore.Mirror(id, expectedHash, source))
                {
                    Interlocked.Increment(ref parallelWriteFailures);
                    OglLog.Warn("[OGL-ASSETS]: Parallel mirror skipped; legacy FSAssets remains authoritative.");
                    return;
                }
                string freshHash;
                if (m_DataConnector.Get(id, out freshHash) == null ||
                    !String.Equals(expectedHash, freshHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Mapping is safe to leave: reads only accept an exact match
                    // with the current authoritative SQL hash.
                    Interlocked.Increment(ref parallelWriteFailures);
                    return;
                }
                Interlocked.Increment(ref parallelMirrored);
            }
            catch (Exception error)
            {
                Interlocked.Increment(ref parallelWriteFailures);
                OglLog.Warn("[OGL-ASSETS]: Parallel mirror failed; FSAssets commit preserved.", error);
            }
        }

        private void InvalidateLocal(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return;
            System.Threading.Interlocked.Increment(ref localInvalidationAttempts);
            cache.Invalidate(id);
            try { diskCache?.Invalidate(id); }
            catch (System.IO.IOException)
            {
                System.Threading.Interlocked.Increment(ref diskInvalidationFailures);
            }
            catch (UnauthorizedAccessException)
            {
                System.Threading.Interlocked.Increment(ref diskInvalidationFailures);
            }
        }

    }
}
