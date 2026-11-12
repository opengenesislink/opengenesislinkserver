using System;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Immutable point-in-time operational snapshot. No asset identifiers,
    /// connection strings, credentials or payload bytes are included.
    /// </summary>
    public sealed class OGLAssetServiceDiagnostics
    {
        public DateTimeOffset CapturedAtUtc { get; }
        public bool AtomicUpdatesEnabled { get; }
        public bool LocalCacheEnabled { get; }
        public bool RedisEnabled { get; }
        public long AtomicAttempts { get; }
        public long AtomicSucceeded { get; }
        public long AtomicRejected { get; }
        public long AtomicExceptions { get; }
        public double AtomicTotalMilliseconds { get; }
        public long MemoryCacheHits { get; }
        public long MemoryCacheMisses { get; }
        public long MemoryCacheUsedBytes { get; }
        public long DiskCacheHits { get; }
        public long DiskCacheMisses { get; }
        public long DiskCacheEvictions { get; }
        public long LocalInvalidations { get; }
        public long DiskInvalidationFailures { get; }
        public long RedisInvalidationFailures { get; }
        public bool ParallelStoreEnabled { get; }
        public bool ParallelReadPreferred { get; }
        public long ParallelMirrored { get; }
        public long ParallelReadHits { get; }
        public long ParallelReadMisses { get; }
        public long ParallelWriteFailures { get; }
        // Strictly non-sensitive S3 observations (no endpoints or credentials).
        public bool S3Enabled { get; }
        public string S3HealthStatus { get; }
        public long S3HealthChecks { get; }
        public long S3HealthFailures { get; }

        public OGLAssetServiceDiagnostics(
            bool atomicUpdatesEnabled, bool localCacheEnabled, bool redisEnabled,
            long attempts, long succeeded, long rejected, long exceptions,
            long elapsedTicks, long memoryHits, long memoryMisses, long memoryBytes,
            long diskHits, long diskMisses, long diskEvictions, long localInvalidations,
            long diskInvalidationFailures, long redisInvalidationFailures,
            bool parallelStoreEnabled = false, bool parallelReadPreferred = false,
            long parallelMirrored = 0, long parallelReadHits = 0,
            long parallelReadMisses = 0, long parallelWriteFailures = 0,
            bool s3Enabled = false, string s3HealthStatus = "disabled",
            long s3HealthChecks = 0, long s3HealthFailures = 0)
        {
            CapturedAtUtc = DateTimeOffset.UtcNow;
            AtomicUpdatesEnabled = atomicUpdatesEnabled;
            LocalCacheEnabled = localCacheEnabled;
            RedisEnabled = redisEnabled;
            AtomicAttempts = attempts;
            AtomicSucceeded = succeeded;
            AtomicRejected = rejected;
            AtomicExceptions = exceptions;
            AtomicTotalMilliseconds = elapsedTicks * 1000.0 /
                System.Diagnostics.Stopwatch.Frequency;
            MemoryCacheHits = memoryHits;
            MemoryCacheMisses = memoryMisses;
            MemoryCacheUsedBytes = memoryBytes;
            DiskCacheHits = diskHits;
            DiskCacheMisses = diskMisses;
            DiskCacheEvictions = diskEvictions;
            LocalInvalidations = localInvalidations;
            DiskInvalidationFailures = diskInvalidationFailures;
            RedisInvalidationFailures = redisInvalidationFailures;
            ParallelStoreEnabled = parallelStoreEnabled;
            ParallelReadPreferred = parallelReadPreferred;
            ParallelMirrored = parallelMirrored;
            ParallelReadHits = parallelReadHits;
            ParallelReadMisses = parallelReadMisses;
            ParallelWriteFailures = parallelWriteFailures;
            S3Enabled = s3Enabled;
            S3HealthStatus = s3HealthStatus;
            S3HealthChecks = s3HealthChecks;
            S3HealthFailures = s3HealthFailures;
        }
    }
}
