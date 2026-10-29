// SPDX-License-Identifier: MPL-2.0
// Pure filename/offset/ETA policy for authenticated IAR transfer.
// Files live under SharedRoot/<trusted Region UUID>/<job GUID>.iar.
// The same SharedRoot must be mounted on ROBUST and on the target simulator.
using System;
using System.IO;

namespace NexVerse.Core.Archives
{
    public static class OglIarTransferPolicy
    {
        public const int MaximumChunkBytes = 4 * 1024 * 1024;
        public const long DefaultMaximumArchiveBytes = 2L * 1024 * 1024 * 1024;

        public static string Resolve(string sharedRoot, Guid regionId, Guid jobId)
        {
            if (string.IsNullOrWhiteSpace(sharedRoot) || regionId == Guid.Empty ||
                jobId == Guid.Empty)
                throw new ArgumentException("Shared root, region and job must be configured.");
            string root = Path.GetFullPath(sharedRoot);
            if (root == Path.GetPathRoot(root))
                throw new ArgumentException("Archive root must not be a filesystem root.");
            return Path.Combine(root, regionId.ToString("D"), jobId.ToString("D") + ".iar");
        }

        public static bool AcceptChunk(long currentBytes, long offset,
            long bytes, long expectedTotal, long maximumTotal)
        {
            return currentBytes >= 0 && offset == currentBytes &&
                bytes > 0 && bytes <= MaximumChunkBytes &&
                expectedTotal > 0 && expectedTotal <= maximumTotal &&
                currentBytes <= expectedTotal &&
                bytes <= expectedTotal - currentBytes;
        }

        // ETA is deliberately unavailable before real progress (or when
        // stalled), rather than presenting a fabricated countdown.
        public static long? EtaSeconds(long complete, long total,
            DateTimeOffset createdAt, DateTimeOffset now)
        {
            if (total <= 0 || complete <= 0 || complete >= total ||
                now <= createdAt)
                return null;
            double elapsed = (now - createdAt).TotalSeconds;
            if (elapsed < 2.0) return null;
            double remaining = (total - complete) * elapsed / complete;
            if (double.IsNaN(remaining) || double.IsInfinity(remaining) ||
                remaining > 7.0 * 86400.0)
                return null;
            return Math.Max(1L, (long)Math.Ceiling(remaining));
        }

        public static long? EtaFromJob(int percent, DateTimeOffset? startedAt,
            DateTimeOffset now)
        {
            if (!startedAt.HasValue || percent <= 0 || percent >= 100)
                return null;
            return EtaSeconds(percent, 100, startedAt.Value, now);
        }
    }
}
