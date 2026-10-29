// SPDX-License-Identifier: MPL-2.0
// Authenticated resident-owned IAR transfer over the World API.
// Bounded binary blocks support archives well over 100 MiB without
// buffering the complete HTTP body in ROBUST. Requires explicit
// ROBUST/simulator shared storage, no bus transport of archive bytes.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Diagnostics;
using NexVerse.Core.Archives;
using NexVerse.Core.ControlPlane;
using NexVerse.Core.Jobs;
using NexVerse.Core.Messaging;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using Nini.Config;

namespace NexVerse.Server.Api
{
    internal sealed class OglIarSelfServiceApi
    {
        private readonly NexApiAuthenticator m_Auth;
        private readonly NexNodeRegistry m_Nodes;
        private readonly OglJobEventBridge m_Bridge;
        private readonly IOglJobStore m_Jobs;
        private readonly INexEventBus m_Bus;
        private readonly OglFeatureControlApi m_Features;
        private readonly bool m_Enabled;
        private readonly string m_SharedRoot;
        private readonly long m_MaxBytes;
        private readonly string m_NasHost;
        private readonly int m_RetentionDays;
        private readonly Timer m_CleanupTimer;
        private readonly ConcurrentDictionary<string, object> m_Locks = new();

        public OglIarSelfServiceApi(IConfigSource config,
            NexApiAuthenticator auth, NexNodeRegistry nodes,
            OglJobEventBridge bridge, IOglJobStore jobs, INexEventBus bus,
            OglFeatureControlApi features)
        {
            m_Auth = auth; m_Nodes = nodes; m_Bridge = bridge;
            m_Jobs = jobs; m_Bus = bus; m_Features = features;
            IConfig section = config?.Configs["OglIarSelfService"];
            string root = section?.GetString("SharedStorageRoot", "")?.Trim() ?? "";
            long maxMib = section?.GetLong("MaximumArchiveMiB", 2048) ?? 2048;
            m_NasHost = section?.GetString("NasHost", "")?.Trim() ?? "";
            int days = section?.GetInt("RetentionDays", 30) ?? 30;
            m_RetentionDays = Math.Clamp(days, 1, OglIarNasStorage.MaximumRetentionDays);
            m_Enabled = section?.GetBoolean("Enabled", false) == true &&
                root.Length > 0 && maxMib >= 101 && maxMib <= 10240 &&
                string.Equals(m_NasHost, "10.78.0.4", StringComparison.Ordinal) &&
                days >= 1 && days <= OglIarNasStorage.MaximumRetentionDays;
            m_SharedRoot = m_Enabled ? Path.GetFullPath(root) : string.Empty;
            m_MaxBytes = checked(Math.Clamp(maxMib, 101, 10240) * 1024L * 1024);
            if (m_Enabled && m_SharedRoot == Path.GetPathRoot(m_SharedRoot))
                m_Enabled = false;
            if (m_Enabled)
                m_CleanupTimer = new Timer(_ => CleanupExpiredArchives(),
                    null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (!m_Enabled)
            {
                Error(response, HttpStatusCode.ServiceUnavailable, "iar_self_service_disabled");
                return;
            }
            string method = request?.HttpMethod ?? "";
            string path = (request?.UriPath ?? "").TrimEnd('/');
            bool read = method.Equals("GET", StringComparison.OrdinalIgnoreCase);
            string scope = read ? NexScopes.InventoryRead : NexScopes.InventoryWrite;
            if (path == "/api/v1/iar/me/export" && method == "POST")
                scope = NexScopes.InventoryRead;
            if (!m_Auth.TryAuthenticate(request, scope,
                out NexPrincipal principal, out UserAccount user,
                out int authStatus, out string authError))
            {
                response.AddHeader("WWW-Authenticate", "Bearer");
                Error(response, (HttpStatusCode)authStatus, authError);
                return;
            }
            // A service/API-key principal is not an authenticated avatar.
            // NEVER accept a user_id/owner_id from request data.
            if (user == null || user.PrincipalID == UUID.Zero ||
                !string.Equals(principal.Subject, user.PrincipalID.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                Error(response, HttpStatusCode.Forbidden, "resident_token_required");
                return;
            }
            try
            {
                // Never create local backup files if NFS is not mounted.
                OglIarNasStorage.RequireMounted(m_SharedRoot, m_NasHost);
                if (path == "/api/v1/iar/me/export" && method == "POST")
                { Start(request, response, user, "export"); return; }
                if (path == "/api/v1/iar/me/import" && method == "POST")
                { Start(request, response, user, "import"); return; }
                const string prefix = "/api/v1/iar/me/operations/";
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                { Error(response, HttpStatusCode.NotFound, "unknown_iar_route"); return; }
                string[] parts = path.Substring(prefix.Length).Split('/');
                if (parts.Length == 0 || !Guid.TryParseExact(parts[0], "D", out Guid id))
                { Error(response, HttpStatusCode.NotFound, "invalid_operation_id"); return; }
                OglJobSnapshot job = OwnJob(user, id);
                if (job == null)
                { Error(response, HttpStatusCode.NotFound, "operation_not_found"); return; }
                if (parts.Length == 1 && read)
                { Status(response, job); return; }
                if (parts.Length == 1 && method == "DELETE")
                { DeleteArchive(response, job); return; }
                if (parts.Length == 2 && parts[1] == "chunks" && method == "PUT")
                { UploadChunk(request, response, job); return; }
                if (parts.Length == 2 && parts[1] == "start" && method == "POST")
                { StartUploaded(response, job); return; }
                if (parts.Length == 2 && parts[1] == "file" && read)
                { DownloadChunk(request, response, job); return; }
                if (parts.Length == 2 && parts[1] == "md5" && read)
                { Md5Manifest(response, job); return; }
                Error(response, HttpStatusCode.MethodNotAllowed, "unsupported_operation");
            }
            catch (JsonException)
            { Error(response, HttpStatusCode.BadRequest, "invalid_json"); }
            catch (IOException)
            { Error(response, HttpStatusCode.ServiceUnavailable, "archive_io_unavailable"); }
            catch (UnauthorizedAccessException)
            { Error(response, HttpStatusCode.ServiceUnavailable, "archive_access_denied"); }
            catch (ArgumentException)
            { Error(response, HttpStatusCode.BadRequest, "invalid_archive_request"); }
            catch (InvalidOperationException)
            { Error(response, HttpStatusCode.Conflict, "archive_operation_conflict"); }
        }

        private void Start(IOSHttpRequest request, IOSHttpResponse response,
            UserAccount user, string action)
        {
            // Small metadata envelope only. Raw archive data is sent via PUT.
            if (request.ContentLength < 0 || request.ContentLength > 8192)
            { Error(response, HttpStatusCode.RequestEntityTooLarge, "metadata_too_large"); return; }
            using JsonDocument doc = JsonDocument.Parse(request.InputStream,
                new JsonDocumentOptions { MaxDepth = 8 });
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            { Error(response, HttpStatusCode.BadRequest, "invalid_metadata"); return; }

            string region = ReadString(root, "region_id");
            if (!Guid.TryParseExact(region, "D", out Guid regionGuid) ||
                regionGuid == Guid.Empty)
            { Error(response, HttpStatusCode.BadRequest, "invalid_region_id"); return; }
            string inventoryPath = ReadString(root, "inventory_path");
            if (string.IsNullOrEmpty(inventoryPath) || inventoryPath.Length > 255 ||
                inventoryPath.Contains("..") || inventoryPath.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0)
            { Error(response, HttpStatusCode.BadRequest, "invalid_inventory_path"); return; }

            long size = action == "import" ? ReadInt64(root, "archive_size_bytes") : 0;
            string sha = action == "import" ? ReadString(root, "sha256").ToLowerInvariant() : "";
            if (action == "import" &&
                (size <= 0 || size > m_MaxBytes || sha.Length != 64 ||
                 !sha.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            { Error(response, HttpStatusCode.BadRequest, "invalid_archive_size_or_sha256"); return; }
            bool merge = root.TryGetProperty("merge", out JsonElement merging) &&
                merging.ValueKind == JsonValueKind.True;
            bool dry = root.TryGetProperty("dry_run", out JsonElement checking) &&
                checking.ValueKind == JsonValueKind.True;

            NexNodeSnapshot node = m_Nodes.FindNodeForRegion(regionGuid.ToString("D"));
            if (node == null || !string.Equals(node.State, "online", StringComparison.OrdinalIgnoreCase))
            { Error(response, HttpStatusCode.ServiceUnavailable, "region_node_unavailable"); return; }
            int feature = action == "import"
                ? OglArchiveFeatureGate.IarImport : OglArchiveFeatureGate.IarExport;
            if (m_Features != null && !m_Features.Permitted(node.NodeId, feature))
            { Error(response, HttpStatusCode.Forbidden, "iar_feature_not_licensed"); return; }
            ExpireStaleUploads(user);
            // One live IAR job per resident; prevent unbounded disk reservation.
            if (m_Jobs.List(1000).Any(j => j.Actor == Actor(user) &&
                (j.State == OglJobState.Queued || j.State == OglJobState.Running)))
            { Error(response, HttpStatusCode.Conflict, "iar_operation_already_active"); return; }

            Guid id = Guid.NewGuid();
            string file = OglIarTransferPolicy.Resolve(m_SharedRoot, regionGuid, id);
            // Do not allow overwriting any existing archive, including symlinks.
            if (action == "import")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                using (new FileStream(file, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None)) { }
            }

            Dictionary<string, string> metadata = new()
            {
                ["user_id"] = user.PrincipalID.ToString(),
                ["region_id"] = regionGuid.ToString("D"),
                ["node_id"] = node.NodeId,
                ["action"] = action,
                ["file_name"] = id.ToString("D") + ".iar",
                ["inventory_path"] = inventoryPath,
                ["expected_bytes"] = size.ToString(CultureInfo.InvariantCulture),
                ["sha256"] = sha,
                ["merge"] = merge.ToString(),
                ["dry_run"] = dry.ToString(),
                ["self_service"] = "true",
                ["retention_days"] = m_RetentionDays.ToString(CultureInfo.InvariantCulture)
            };
            OglJobSnapshot job;
            try
            {
                job = m_Bridge.RegisterOperation(
                    "archive.iar." + action, id.ToString("D"), Actor(user),
                    id.ToString("D"), metadata);
            }
            catch
            {
                if (action == "import") File.Delete(file);
                throw;
            }
            // Retention marker survives job-store rotation. It is created
            // only for this resident-owned job, never for operator archives.
            try
            {
                string marker = FileFor(job) + ".ogl-retention";
                Directory.CreateDirectory(Path.GetDirectoryName(marker));
                using (new FileStream(marker, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None)) { }
            }
            catch
            {
                m_Jobs.Fail(job.JobId, "retention_marker_failed");
                if (action == "import") File.Delete(file);
                throw;
            }
            if (action == "export")
                Publish(job);
            Json(response, HttpStatusCode.Accepted,
                new { operation_id = job.JobId, state = "queued",
                    upload_chunk_max_bytes = OglIarTransferPolicy.MaximumChunkBytes,
                    status_url = "/api/v1/iar/me/operations/" + job.JobId });
        }

        private void Publish(OglJobSnapshot job)
        {
            Dictionary<string, string> payload = new()
            {
                ["operation_id"] = job.JobId,
                ["target_node_id"] = job.Metadata["node_id"],
                ["region_id"] = job.Metadata["region_id"],
                ["user_id"] = job.Metadata["user_id"],
                ["action"] = job.Metadata["action"],
                ["file_name"] = job.Metadata["file_name"],
                ["inventory_path"] = job.Metadata["inventory_path"],
                ["merge"] = job.Metadata["merge"],
                ["dry_run"] = job.Metadata["dry_run"],
                ["self_service"] = "true",
                ["nas_host"] = m_NasHost
            };
            m_Bus.Publish(new NexEvent("archive.iar.requested",
                "opengenesislink.world-api", payload, job.JobId));
        }

        private OglJobSnapshot OwnJob(UserAccount user, Guid id)
        {
            OglJobSnapshot job = m_Bridge.Get(id.ToString("D"));
            if (job == null || job.Actor != Actor(user) ||
                !job.Metadata.TryGetValue("user_id", out string userId) ||
                userId != user.PrincipalID.ToString() ||
                !job.Metadata.TryGetValue("self_service", out string self) ||
                self != "true")
                return null;
            return job;
        }

        private static string Actor(UserAccount user) =>
            "iar-self-service:" + user.PrincipalID;

        private string FileFor(OglJobSnapshot job)
        {
            if (!Guid.TryParseExact(job.Metadata["region_id"], "D",
                    out Guid region) ||
                !Guid.TryParseExact(job.JobId, "D", out Guid id))
                throw new InvalidOperationException("Invalid stored IAR job.");
            return OglIarTransferPolicy.Resolve(m_SharedRoot, region, id);
        }

        private void UploadChunk(IOSHttpRequest req, IOSHttpResponse res, OglJobSnapshot job)
        {
            if (job.Metadata["action"] != "import" ||
                job.State is OglJobState.Completed or OglJobState.Failed or OglJobState.Cancelled ||
                (job.State == OglJobState.Running && job.ProgressPhase != "uploading"))
            { Error(res, HttpStatusCode.Conflict, "upload_not_active"); return; }

            if (!long.TryParse(req.QueryString?["offset"],
                    NumberStyles.None, CultureInfo.InvariantCulture, out long offset) ||
                req.ContentLength <= 0 || req.ContentLength > OglIarTransferPolicy.MaximumChunkBytes)
            { Error(res, HttpStatusCode.BadRequest, "invalid_chunk_request"); return; }
            long expected = long.Parse(job.Metadata["expected_bytes"], CultureInfo.InvariantCulture);
            string path = FileFor(job);
            lock (m_Locks.GetOrAdd(job.JobId, _ => new object()))
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Write, FileShare.None);
                long current = stream.Length;
                if (!OglIarTransferPolicy.AcceptChunk(current, offset,
                    req.ContentLength, expected, m_MaxBytes))
                { Error(res, HttpStatusCode.Conflict, "offset_or_size_mismatch",
                    new { next_offset = current }); return; }
                stream.Position = current;
                byte[] buffer = new byte[65536];
                long remaining = req.ContentLength;
                while (remaining > 0)
                {
                    int n = req.InputStream.Read(buffer, 0,
                        (int)Math.Min(remaining, buffer.Length));
                    if (n <= 0) throw new IOException("Upload stream interrupted.");
                    stream.Write(buffer, 0, n);
                    remaining -= n;
                }
                stream.Flush(true);
                long next = stream.Length;
                if (job.State == OglJobState.Queued)
                    m_Jobs.Start(job.JobId, "uploading");
                int percent = (int)Math.Min(99L, next * 100 / expected);
                m_Jobs.Report(job.JobId, percent, "uploading", "archive upload");
                Json(res, HttpStatusCode.OK,
                    new { next_offset = next, expected_bytes = expected,
                        upload_percent = percent });
            }
        }

        private void StartUploaded(IOSHttpResponse response, OglJobSnapshot job)
        {
            if (job.Metadata["action"] != "import" ||
                job.State is OglJobState.Completed or OglJobState.Failed or OglJobState.Cancelled ||
                (job.State == OglJobState.Running && job.ProgressPhase != "uploading"))
            { Error(response, HttpStatusCode.Conflict, "upload_not_active"); return; }

            lock (m_Locks.GetOrAdd(job.JobId, _ => new object()))
            {
                string path = FileFor(job);
                long expected = long.Parse(job.Metadata["expected_bytes"],
                    CultureInfo.InvariantCulture);
                if (!File.Exists(path) || new FileInfo(path).Length != expected)
                { Error(response, HttpStatusCode.Conflict, "upload_incomplete"); return; }
                string actual = Digest(path, SHA256.Create);
                if (!string.Equals(actual, job.Metadata["sha256"], StringComparison.Ordinal))
                {
                    // Never leave a full corrupted archive consuming the
                    // resident's active-job quota or shared storage.
                    m_Jobs.Fail(job.JobId, "sha256_mismatch");
                    File.Delete(path);
                    DeleteMarker(path);
                    Error(response, HttpStatusCode.Conflict, "sha256_mismatch");
                    return;
                }

                // Uploaded file already lives where the simulator's IAR
                // inspector reads it. Never transfer binary bytes via NexBus.
                if (job.State == OglJobState.Queued)
                    m_Jobs.Start(job.JobId, "uploaded");
                else
                    m_Jobs.Report(job.JobId, 5, "uploaded", "archive received");
                Publish(job);
                Json(response, HttpStatusCode.Accepted,
                    new { operation_id = job.JobId, state = "running",
                        status_url = "/api/v1/iar/me/operations/" + job.JobId });
            }
        }

        private void Status(IOSHttpResponse response, OglJobSnapshot job)
        {
            bool uploading = job.Metadata["action"] == "import" &&
                (job.State == OglJobState.Queued || job.ProgressPhase == "uploading");
            long transferred = 0;
            long total = 0;
            if (uploading)
            {
                total = long.Parse(job.Metadata["expected_bytes"], CultureInfo.InvariantCulture);
                string path = FileFor(job);
                transferred = File.Exists(path) ? new FileInfo(path).Length : 0;
            }
            long? eta = uploading
                ? OglIarTransferPolicy.EtaSeconds(transferred, total,
                    job.CreatedAt, DateTimeOffset.UtcNow)
                : OglIarTransferPolicy.EtaFromJob(job.ProgressPercent,
                    job.StartedAt, DateTimeOffset.UtcNow);
            string phase = uploading ? "uploading" : job.ProgressPhase;
            bool completeExport = job.Metadata["action"] == "export" &&
                job.State == OglJobState.Completed;
            string file = completeExport ? FileFor(job) : null;
            bool ready = completeExport && !IsExpired(job) &&
                File.Exists(file) &&
                new FileInfo(file).Length > 0 &&
                new FileInfo(file).Length <= m_MaxBytes;
            Json(response, HttpStatusCode.OK, new
            {
                operation_id = job.JobId, state = job.State.ToString().ToLowerInvariant(),
                action = job.Metadata["action"], phase,
                progress_percent = uploading && total > 0
                    ? (int)Math.Min(99L, transferred * 100 / total)
                    : job.ProgressPercent,
                eta_seconds = eta,
                uploaded_bytes = uploading ? transferred : (long?)null,
                expected_bytes = uploading ? total : (long?)null,
                next_offset = uploading ? transferred : (long?)null,
                updated_at_utc = job.UpdatedAt,
                message = job.Message,
                download_ready = ready,
                retention_days = m_RetentionDays,
                expires_at_utc = job.State == OglJobState.Completed
                    ? (job.FinishedAt ?? job.CreatedAt).AddDays(m_RetentionDays)
                    : (DateTimeOffset?)null,
                file_size_bytes = ready ? new FileInfo(file).Length : (long?)null,
                download_url = ready ?
                    "/api/v1/iar/me/operations/" + job.JobId + "/file" : null,
                md5_url = ready ?
                    "/api/v1/iar/me/operations/" + job.JobId + "/md5" : null
            });
        }

        private void DownloadChunk(IOSHttpRequest request, IOSHttpResponse res,
            OglJobSnapshot job)
        {
            if (!TryReadyExport(job, out string file))
            { Error(res, HttpStatusCode.Conflict, "export_not_ready"); return; }
            long length = new FileInfo(file).Length;
            if (!long.TryParse(request.QueryString?["offset"], NumberStyles.None,
                    CultureInfo.InvariantCulture, out long offset) ||
                !int.TryParse(request.QueryString?["length"], NumberStyles.None,
                    CultureInfo.InvariantCulture, out int chunk) ||
                offset < 0 || offset >= length || chunk <= 0 ||
                chunk > OglIarTransferPolicy.MaximumChunkBytes)
            { Error(res, HttpStatusCode.BadRequest, "use_bounded_offset_and_length"); return; }
            int take = (int)Math.Min((long)chunk, length - offset);
            byte[] data = new byte[take];
            using (FileStream stream = File.OpenRead(file))
            {
                stream.Position = offset;
                stream.ReadExactly(data);
            }
            res.StatusCode = (int)HttpStatusCode.PartialContent;
            res.ContentType = "application/octet-stream";
            res.AddHeader("Content-Disposition", "attachment; filename=\"" + job.JobId + ".iar\"");
            res.AddHeader("Content-Range", "bytes " +
                offset.ToString(CultureInfo.InvariantCulture) + "-" +
                (offset + take - 1).ToString(CultureInfo.InvariantCulture) + "/" +
                length.ToString(CultureInfo.InvariantCulture));
            res.AddHeader("Accept-Ranges", "bytes");
            res.RawBuffer = data;
        }

        private void Md5Manifest(IOSHttpResponse res, OglJobSnapshot job)
        {
            if (!TryReadyExport(job, out string file))
            { Error(res, HttpStatusCode.Conflict, "export_not_ready"); return; }
            // MD5 for requested legacy tooling only; SHA-256 is the
            // security-oriented checksum also published in the manifest.
            string md5 = Digest(file, MD5.Create);
            string sha = Digest(file, SHA256.Create);
            res.StatusCode = (int)HttpStatusCode.OK;
            res.ContentType = "text/plain; charset=utf-8";
            res.AddHeader("Content-Disposition",
                "attachment; filename=\"" + job.JobId + ".iar.md5\"");
            res.RawBuffer = Encoding.UTF8.GetBytes(md5 + "  " +
                job.JobId + ".iar\nSHA256 " + sha + "\n");
        }

        private void DeleteArchive(IOSHttpResponse response, OglJobSnapshot job)
        {
            lock (m_Locks.GetOrAdd(job.JobId, _ => new object()))
            {
                // A running importer/exporter may still have the file open.
                // Only staged uploads and terminal jobs can be cleaned up.
                bool staged = job.Metadata["action"] == "import" &&
                    (job.State == OglJobState.Queued ||
                     (job.State == OglJobState.Running &&
                      job.ProgressPhase == "uploading"));
                bool terminal = job.State is OglJobState.Completed or
                    OglJobState.Failed or OglJobState.Cancelled;
                if (!staged && !terminal)
                {
                    Error(response, HttpStatusCode.Conflict,
                        "active_archive_cannot_be_deleted");
                    return;
                }
                if (staged) m_Jobs.Cancel(job.JobId, "Resident cancelled upload.");
                string path = FileFor(job);
                if (File.Exists(path)) File.Delete(path);
                DeleteMarker(path);
                Json(response, HttpStatusCode.OK,
                    new { operation_id = job.JobId, archive_deleted = true });
            }
        }

        private void ExpireStaleUploads(UserAccount user)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (OglJobSnapshot j in m_Jobs.List(1000))
            {
                if (j.Actor != Actor(user) ||
                    j.Metadata == null ||
                    !j.Metadata.TryGetValue("action", out string action) ||
                    action != "import" ||
                    !(j.State == OglJobState.Queued ||
                      (j.State == OglJobState.Running &&
                       j.ProgressPhase == "uploading")) ||
                    now - j.UpdatedAt <= TimeSpan.FromHours(24))
                    continue;
                lock (m_Locks.GetOrAdd(j.JobId, _ => new object()))
                {
                    OglJobSnapshot current = m_Jobs.Get(j.JobId);
                    if (current == null || !(current.State == OglJobState.Queued ||
                        (current.State == OglJobState.Running &&
                         current.ProgressPhase == "uploading")) ||
                        now - current.UpdatedAt <= TimeSpan.FromHours(24))
                        continue;
                    m_Jobs.Fail(j.JobId, "Expired incomplete IAR upload.");
                    string path = FileFor(j);
                    if (File.Exists(path)) File.Delete(path);
                    DeleteMarker(path);
                }
            }
        }

        private bool IsExpired(OglJobSnapshot job)
        {
            DateTimeOffset age = job.FinishedAt ?? job.CreatedAt;
            return OglIarNasStorage.IsExpired(age,
                DateTimeOffset.UtcNow, m_RetentionDays);
        }

        private static void DeleteMarker(string path)
        {
            string marker = path + ".ogl-retention";
            if (File.Exists(marker)) File.Delete(marker);
        }

        private void CleanupExpiredArchives()
        {
            if (!m_Enabled) return;
            try
            {
                OglIarNasStorage.RequireMounted(m_SharedRoot, m_NasHost);
                if (!Directory.Exists(m_SharedRoot)) return;
                DateTimeOffset now = DateTimeOffset.UtcNow;
                foreach (string regionDir in Directory.EnumerateDirectories(m_SharedRoot))
                {
                    if ((File.GetAttributes(regionDir) & FileAttributes.ReparsePoint) != 0)
                        continue;
                    if (!Guid.TryParseExact(Path.GetFileName(regionDir), "D",
                        out Guid region) || region == Guid.Empty)
                        continue;
                    foreach (string file in Directory.EnumerateFiles(regionDir, "*.iar",
                        SearchOption.TopDirectoryOnly))
                    {
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 ||
                            !OglIarNasStorage.IsManagedIar(m_SharedRoot, file,
                                out Guid id))
                            continue;

                        lock (m_Locks.GetOrAdd(id.ToString("D"), _ => new object()))
                        {
                            OglJobSnapshot job = m_Jobs.Get(id.ToString("D"));
                            if (job != null &&
                                job.State is OglJobState.Running or OglJobState.Queued)
                                continue;
                            if (job != null &&
                                (!job.Metadata.TryGetValue("self_service", out string owner) ||
                                 owner != "true"))
                                continue;
                            string marker = file + ".ogl-retention";
                            // Without a matching self-service job or retention
                            // marker we must not purge unknown operator files.
                            if (job == null && (!File.Exists(marker) ||
                                (File.GetAttributes(marker) & FileAttributes.ReparsePoint) != 0))
                                continue;
                            DateTimeOffset created = job?.FinishedAt ??
                                job?.CreatedAt ??
                                new DateTimeOffset(File.GetLastWriteTimeUtc(marker));
                            if (!OglIarNasStorage.IsExpired(created,
                                now, m_RetentionDays))
                                continue;
                            OglIarNasStorage.RequireMounted(m_SharedRoot, m_NasHost);
                            File.Delete(file);
                            DeleteMarker(file);
                        }
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException
                or ArgumentException)
            {
                Trace.TraceWarning("OGL-IAR NAS retention skipped: " + e.GetType().Name);
            }
        }

        private bool TryReadyExport(OglJobSnapshot job, out string path)
        {
            path = null;
            if (job.Metadata["action"] != "export" ||
                job.State != OglJobState.Completed || IsExpired(job))
                return false;
            path = FileFor(job);
            return File.Exists(path) && new FileInfo(path).Length > 0 &&
                new FileInfo(path).Length <= m_MaxBytes;
        }

        private static string Digest(string path, Func<HashAlgorithm> factory)
        {
            using HashAlgorithm digest = factory();
            using FileStream input = File.OpenRead(path);
            return Convert.ToHexString(digest.ComputeHash(input)).ToLowerInvariant();
        }

        private static string ReadString(JsonElement root, string key) =>
            root.TryGetProperty(key, out JsonElement entry) &&
            entry.ValueKind == JsonValueKind.String
                ? entry.GetString()?.Trim() ?? "" : "";

        private static long ReadInt64(JsonElement root, string key) =>
            root.TryGetProperty(key, out JsonElement entry) &&
            entry.ValueKind == JsonValueKind.Number &&
            entry.TryGetInt64(out long number) ? number : -1L;

        private static void Json(IOSHttpResponse r, HttpStatusCode status, object data)
        {
            r.StatusCode = (int)status;
            r.ContentType = "application/json; charset=utf-8";
            r.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(data);
        }

        private static void Error(IOSHttpResponse r, HttpStatusCode status,
            string code, object details = null) =>
            Json(r, status, new { error = code, details });
    }
}
