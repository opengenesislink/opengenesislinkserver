// SPDX-License-Identifier: MPL-2.0
// Network-mounted UGREEN NAS storage policy for resident IAR archives.
// Never create an archive if the expected remote NFS mount disappeared.
using System;
using System.IO;
using System.Linq;

namespace NexVerse.Core.Archives
{
    public static class OglIarNasStorage
    {
        public const int MaximumRetentionDays = 30;

        public static bool IsExpectedNfsMount(string directory, string host,
            string mountInfo)
        {
            if (string.IsNullOrWhiteSpace(directory) ||
                string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(mountInfo))
                return false;
            string root;
            try
            {
                root = Path.GetFullPath(directory).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (ArgumentException) { return false; }
            if (root.Length == 0) return false;

            foreach (string entry in mountInfo.Split('\n'))
            {
                int separator = entry.IndexOf(" - ", StringComparison.Ordinal);
                if (separator <= 0) continue;
                string[] left = entry.Substring(0, separator).Split(' ',
                    StringSplitOptions.RemoveEmptyEntries);
                string[] right = entry.Substring(separator + 3).Split(' ',
                    StringSplitOptions.RemoveEmptyEntries);
                if (left.Length < 5 || right.Length < 2) continue;
                string mountPoint = left[4].Replace("\\040", " ")
                    .Replace("\\011", "\t").Replace("\\012", "\n")
                    .Replace("\\134", "\\");
                if (!string.Equals(mountPoint, root, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(right[0], "nfs4", StringComparison.Ordinal) &&
                    !string.Equals(right[0], "nfs", StringComparison.Ordinal))
                    continue;
                if (!right[1].StartsWith(host.Trim() + ":", StringComparison.Ordinal))
                    continue;
                return true;
            }
            return false;
        }

        public static void RequireMounted(string directory, string host)
        {
            if (!OperatingSystem.IsLinux())
                throw new IOException("IAR NAS: NFS mount validation requires Linux.");
            string mountInfo;
            try { mountInfo = File.ReadAllText("/proc/self/mountinfo"); }
            catch (IOException)
            {
                throw new IOException("IAR NAS: cannot verify network mount.");
            }
            if (!IsExpectedNfsMount(directory, host, mountInfo))
                throw new IOException("IAR NAS: required NFS mount missing or wrong host.");
        }

        public static bool IsExpired(DateTimeOffset createdAt,
            DateTimeOffset now, int retentionDays)
        {
            if (retentionDays < 1 || retentionDays > MaximumRetentionDays)
                throw new ArgumentOutOfRangeException(nameof(retentionDays));
            return createdAt <= now.AddDays(-retentionDays);
        }

        // Refuse to delete user-named, nested or non-IAR paths. Only UUID job
        // files directly under a UUID region folder are eligible for cleanup.
        public static bool IsManagedIar(string root, string path,
            out Guid operationId)
        {
            operationId = Guid.Empty;
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
                return false;
            string full = Path.GetFullPath(path);
            string parent = Path.GetDirectoryName(full);
            string regionFolder = Path.GetFileName(parent);
            if (!Guid.TryParseExact(regionFolder, "D", out Guid region) ||
                region == Guid.Empty)
                return false;
            string fileName = Path.GetFileName(full);
            if (!fileName.EndsWith(".iar", StringComparison.Ordinal) ||
                !Guid.TryParseExact(fileName.Substring(0, fileName.Length - 4),
                    "D", out operationId) ||
                operationId == Guid.Empty)
                return false;
            string expected = OglIarTransferPolicy.Resolve(root, region, operationId);
            return string.Equals(expected, full, StringComparison.Ordinal);
        }
    }
}
