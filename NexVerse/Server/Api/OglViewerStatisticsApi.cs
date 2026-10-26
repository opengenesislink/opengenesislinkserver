// SPDX-License-Identifier: MPL-2.0
// Recent successful viewer login counts. The viewer channel/version are
// self-reported; no IPs, principal IDs or device identifiers are stored here.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using NexVerse.Core.Security;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class OglViewerStatisticsApi
    {
        private readonly NexApiAuthenticator m_Auth;
        public OglViewerStatisticsApi(NexApiAuthenticator auth) =>
            m_Auth = auth ?? throw new ArgumentNullException(nameof(auth));

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.KeepAlive = false;
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("Cache-Control", "private, no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (request.HttpMethod != "GET")
            {
                Send(response, 405, new { error = "method_not_allowed" });
                return;
            }
            if (!m_Auth.TryAuthenticate(request, NexScopes.AdminAll,
                    out NexPrincipal principal, out UserAccount admin,
                    out int status, out string error))
            {
                Send(response, status, new { error = "admin_auth_required" });
                return;
            }
            if (admin == null || !admin.LocalToGrid || admin.UserLevel < 200 ||
                !principal.HasScope(NexScopes.AdminAll))
            {
                Send(response, 403, new { error = "admin_level_required" });
                return;
            }
            int days = 7;
            string daysRaw = request.QueryString?["days"];
            if (daysRaw != null && (!int.TryParse(daysRaw, out days) || days < 1 || days > 30))
            {
                Send(response, 400, new { error = "days_out_of_range" });
                return;
            }
            var counts = new Dictionary<(string Viewer, string Version), int>();
            int scanned = 0, total = 0;
            string folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "logs"));
            try
            {
                for (int ago = 0; ago < days; ++ago)
                {
                    string day = DateTime.Today.AddDays(-ago).ToString("dd_MM_yyyy",
                        CultureInfo.InvariantCulture);
                    string file = Path.Combine(folder, "Robust_" + day + ".log");
                    if (!File.Exists(file)) continue;
                    ++scanned;
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var lines = new StreamReader(stream);
                    int inspected = 0;
                    while (!lines.EndOfStream && inspected++ < 250000)
                    {
                        string line = lines.ReadLine();
                        int start = line?.IndexOf("[OGL-VIEWER-STATS] ", StringComparison.Ordinal) ?? -1;
                        if (start < 0) continue;
                        string raw = line.Substring(start + "[OGL-VIEWER-STATS] ".Length);
                        try
                        {
                            using JsonDocument doc = JsonDocument.Parse(raw);
                            string viewer = doc.RootElement.GetProperty("viewer").GetString() ?? "Unbekannt";
                            string version = doc.RootElement.GetProperty("version").GetString() ?? "Unbekannt";
                            if (viewer.Length > 96 || version.Length > 96) continue;
                            var key = (viewer, version);
                            counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                            ++total;
                        }
                        catch (JsonException) { }
                        catch (KeyNotFoundException) { }
                        catch (InvalidOperationException) { }
                    }
                }
                Send(response, 200, new {
                    period_days = days,
                    metric = "successful_logins",
                    source = "viewer_self_reported_channel_and_version",
                    files_scanned = scanned, total,
                    viewers = counts.OrderByDescending(x => x.Value)
                        .Select(x => new { viewer = x.Key.Viewer,
                            version = x.Key.Version, logins = x.Value }).ToArray(),
                    note = "Die Werte zählen erfolgreiche Anmeldungen, nicht gleichzeitig aktive Viewer."
                });
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Send(response, 503, new { error = "viewer_statistics_unavailable" });
            }
        }
        private static void Send(IOSHttpResponse response, int code, object data)
        {
            response.StatusCode = code;
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(data);
        }
    }
}
