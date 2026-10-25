// SPDX-License-Identifier: MPL-2.0
// Signed NexBus chunks from registered simulator nodes are mirrored to
// bin/logs/OpenSim_<NodeId>_<dd_MM_yyyy>.log, never to arbitrary paths.
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using NexVerse.Core.ControlPlane;
using NexVerse.Core.Messaging;

namespace NexVerse.Server.Api
{
    internal sealed class NexBusRemoteLogArchive
    {
        private static readonly ILog s_Log = LogManager.GetLogger(typeof(NexBusRemoteLogArchive));
        internal static readonly Regex SafeNode = new Regex(
            @"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private const int MaxChunk = 32 * 1024;
        private readonly NexNodeRegistry m_Nodes;
        private readonly string m_Folder;
        private readonly object m_Sync = new object();

        internal NexBusRemoteLogArchive(NexNodeRegistry nodes)
        {
            m_Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            m_Folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "logs"));
        }

        /// <summary>
        /// Invoked only by the HMAC-verified NexBus endpoint, without regular
        /// event-bus fan-out. HTTP 202 is sent ONLY after durable persistence
        /// or byte-for-byte replay of an already persisted chunk.
        /// </summary>
        internal bool TryReceive(NexEvent ev)
        {
            if (ev == null || ev.Name != "node.log.chunk" ||
                ev.Source != "nexverse.simulator" || ev.Data == null ||
                !ev.Data.TryGetValue("node_id", out string node) || !SafeNode.IsMatch(node) ||
                !ev.Data.TryGetValue("date", out string day) ||
                !DateTime.TryParseExact(day, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) ||
                Math.Abs((DateTime.Today - date).TotalDays) > 14 ||
                !ev.Data.TryGetValue("offset", out string rawOffset) ||
                !long.TryParse(rawOffset, NumberStyles.None,
                    CultureInfo.InvariantCulture, out long offset) ||
                offset < 0 || offset > 512L * 1024 * 1024 ||
                !ev.Data.TryGetValue("bytes_base64", out string b64) ||
                b64.Length > (MaxChunk * 4 / 3) + 8 ||
                m_Nodes.Get(node) == null)
                return false;

            byte[] bytes;
            try { bytes = Convert.FromBase64String(b64); }
            catch (FormatException) { return false; }
            if (bytes.Length == 0 || bytes.Length > MaxChunk) return false;

            string name = "OpenSim_" + node + "_" +
                date.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture) + ".log";
            string path = Path.Combine(m_Folder, name);
            try
            {
                lock (m_Sync)
                {
                    Directory.CreateDirectory(m_Folder);
                    using var file = new FileStream(path, FileMode.OpenOrCreate,
                        FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.None);
                    // At-most-once reconstruction of an ordered byte stream.
                    // The sender resends the same chunk if HTTP acknowledgement
                    // is lost; reject gaps and non-identical overlapping data.
                    if (offset > file.Length) return false;
                    if (offset < file.Length)
                    {
                        if (offset + bytes.Length > file.Length) return false;
                        file.Position = offset;
                        byte[] previous = new byte[bytes.Length];
                        if (file.Read(previous, 0, previous.Length) != previous.Length ||
                            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(previous, bytes))
                            return false;
                        return true;
                    }
                    file.Position = file.Length;
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush();
                    if (!OperatingSystem.IsWindows())
                    {
                        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                s_Log.Warn("[OGL-LOG-RELAY]: Cannot persist remote simulator log chunk (" +
                    e.GetType().Name + ").");
                return false;
            }
        }

    }
}
