// SPDX-License-Identifier: MPL-2.0
// Local, read-only, per-day operator logs; no arbitrary paths or remote shell.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NexVerse.Core.Security;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class NexOperatorLogsApi
    {
        private const int MaxPage = 1000;
        private const int MaxOffset = 1000000;
        private static readonly Regex s_FileName = new Regex(
            @"^(Robust|OpenSim)(?:_([A-Za-z0-9][A-Za-z0-9._-]{0,63}))?_(\d{2})_(\d{2})_(\d{4})\.log$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex s_Secret = new Regex(
            @"(?i)(authorization\s*[:=]\s*bearer\s+|x-nexverse-api-key\s*[:=]\s*|password(?:hash|salt)?\s*[:=]\s*|token\s*[:=]\s*)([^\s,;]+)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly NexApiAuthenticator m_Auth;
        private readonly string m_LogDirectory;

        internal NexOperatorLogsApi(NexApiAuthenticator authenticator)
        {
            m_Auth = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            m_LogDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "logs"));
        }

        internal NexOperatorLogsApi(NexApiAuthenticator authenticator, string baseDirectory)
        {
            m_Auth = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            m_LogDirectory = Path.GetFullPath(Path.Combine(baseDirectory, "logs"));
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.KeepAlive = false;
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("Cache-Control", "private, no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (request == null || request.HttpMethod != "GET")
            {
                Write(response, HttpStatusCode.MethodNotAllowed, new {error = "method_not_allowed"});
                return;
            }
            if (!m_Auth.TryAuthenticate(request, NexScopes.AdminAll,
                    out NexPrincipal principal, out UserAccount account,
                    out int status, out string reason))
            {
                Write(response, (HttpStatusCode)status, new {error = "admin_auth_required"});
                return;
            }
            if (account == null || !account.LocalToGrid || account.UserLevel < 200 ||
                !principal.HasScope(NexScopes.AdminAll))
            {
                Write(response, HttpStatusCode.Forbidden, new {error = "admin_level_required"});
                return;
            }

            string path = (request.UriPath ?? "").TrimEnd('/');
            if (path == "/api/v1/admin/logs")
            {
                List(response);
                return;
            }

            string[] parts = path.Trim('/').Split('/');
            if (parts.Length != 7 || parts[0] != "api" || parts[1] != "v1" ||
                parts[2] != "admin" || parts[3] != "logs" ||
                (parts[4] != "Robust" && parts[4] != "OpenSim") ||
                !DateTime.TryParseExact(parts[5], "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) ||
                parts[6] != "entries" ||
                date.Date > DateTime.UtcNow.Date.AddDays(1))
            {
                Write(response, HttpStatusCode.NotFound, new {error = "log_not_found"});
                return;
            }
            int limit = 500, offset = 0;
            if (request.QueryString?["limit"] is string l &&
                (!int.TryParse(l, out limit) || limit < 1 || limit > MaxPage) ||
                request.QueryString?["offset"] is string o &&
                (!int.TryParse(o, out offset) || offset < 0 || offset > MaxOffset))
            {
                Write(response, HttpStatusCode.BadRequest, new {error = "invalid_pagination"});
                return;
            }
            string nodeId = request.QueryString?["node_id"] ?? string.Empty;
            if (nodeId.Length != 0 &&
                (parts[4] != "OpenSim" || !NexBusRemoteLogArchive.SafeNode.IsMatch(nodeId)))
            {
                Write(response, HttpStatusCode.BadRequest, new {error = "invalid_node_id"});
                return;
            }
            ReadDay(response, parts[4], date, limit, offset, nodeId);
        }

        private void List(IOSHttpResponse response)
        {
            try
            {
                var dates = new List<object>();
                if (Directory.Exists(m_LogDirectory))
                {
                    foreach (string file in Directory.EnumerateFiles(m_LogDirectory, "*.log"))
                    {
                        string basename = Path.GetFileName(file);
                        Match match = s_FileName.Match(basename);
                        if (!match.Success ||
                            !DateTime.TryParseExact(
                                match.Groups[3].Value+"_"+match.Groups[4].Value+"_"+match.Groups[5].Value,
                                "dd_MM_yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None,
                                out DateTime date)) continue;
                        FileInfo info = new FileInfo(file);
                        dates.Add(new
                        {
                            service = match.Groups[1].Value,
                            node_id = match.Groups[2].Success ? match.Groups[2].Value : null,
                            date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            name = basename,
                            bytes = info.Length,
                            last_modified_utc = info.LastWriteTimeUtc.ToString("O")
                        });
                    }
                }
                // Same fields as ReadDay, grouped in descending date order by the browser.
                dates = dates.OrderByDescending(x => JsonSerializer.Serialize(x)).ToList();
                Write(response, HttpStatusCode.OK, new {files = dates, directory = "logs", format = "dd_MM_yyyy"});
            }
            catch (IOException)
            {
                Write(response, HttpStatusCode.ServiceUnavailable, new {error = "logs_unavailable"});
            }
            catch (UnauthorizedAccessException)
            {
                Write(response, HttpStatusCode.ServiceUnavailable, new {error = "logs_unavailable"});
            }
        }

        private void ReadDay(IOSHttpResponse response, string service, DateTime date,
            int limit, int offset, string nodeId)
        {
            string filename = service+(string.IsNullOrEmpty(nodeId) ? "" : "_"+nodeId)+
                "_"+date.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture)+".log";
            string file = Path.Combine(m_LogDirectory, filename);
            if (!File.Exists(file))
            {
                Write(response, HttpStatusCode.NotFound, new {error = "log_not_found"});
                return;
            }
            try
            {
                List<string> lines = new List<string>(limit + 1);
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    for (int i = 0; i < offset && reader.ReadLine() != null; i++) { }
                    while (lines.Count <= limit)
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;
                        line = s_Secret.Replace(line, "$1[REDACTED]");
                        if (line.Length > 8192) line = line.Substring(0, 8192)+"…";
                        lines.Add(line);
                    }
                }
                bool hasMore = lines.Count > limit;
                if (hasMore) lines.RemoveAt(limit);
                Write(response, HttpStatusCode.OK, new
                {
                    service, node_id = string.IsNullOrEmpty(nodeId) ? null : nodeId,
                    date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    file = filename, offset, limit, has_more = hasMore, next_offset = offset + lines.Count,
                    lines
                });
            }
            catch (IOException)
            {
                Write(response, HttpStatusCode.ServiceUnavailable, new {error = "logs_unavailable"});
            }
            catch (UnauthorizedAccessException)
            {
                Write(response, HttpStatusCode.ServiceUnavailable, new {error = "logs_unavailable"});
            }
        }

        private static void Write(IOSHttpResponse response, HttpStatusCode status, object payload)
        {
            response.StatusCode = (int)status;
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(payload);
        }
    }
}
