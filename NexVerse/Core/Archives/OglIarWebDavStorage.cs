// SPDX-License-Identifier: MPL-2.0
// UGREEN NAS IAR transport: WebDAV over authenticated HTTPS, no kernel mount.
// Persistent archive bytes live on the NAS; native OpenSim archiver uses bounded
// local working files that must be removed after import/export completes.
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace NexVerse.Core.Archives
{
    public sealed class OglIarWebDavStorage : IDisposable
    {
        private readonly HttpClient m_Http;
        private readonly Uri m_Root;
        private readonly string m_NasHost;
        private readonly string m_Auth;
        public const string BackendName = "WebDAV";

        // WebDAV URL must refer to a dedicated existing folder (not the NAS
        // service root). TLS hostname verification remains enabled. The socket
        // is pinned to 10.78.0.4 so DNS cannot move backups off the VPN.
        public OglIarWebDavStorage(string baseUrl, string nasHost, string user,
            string passwordFile)
        {
            if (nasHost != "10.78.0.4" ||
                !Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.Port <= 0 ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath == "/" ||
                uri.AbsolutePath.Contains("%", StringComparison.Ordinal) ||
                uri.AbsolutePath.Contains("..", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(user) || user.Contains(':') ||
                string.IsNullOrWhiteSpace(passwordFile) ||
                !Path.IsPathFullyQualified(passwordFile))
                throw new ArgumentException("IAR WebDAV requires a pinned NAS, HTTPS folder URL and secret file.");
            if (OperatingSystem.IsLinux())
            {
                UnixFileMode permissions = File.GetUnixFileMode(passwordFile);
                if ((permissions & (UnixFileMode.GroupRead | UnixFileMode.OtherRead |
                    UnixFileMode.GroupWrite | UnixFileMode.OtherWrite |
                    UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0)
                    throw new UnauthorizedAccessException("IAR WebDAV password file must be owner-only (0600).");
            }
            string password = File.ReadAllText(passwordFile).TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(password) || password.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("Invalid IAR WebDAV password file.");
            m_Root = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
            m_NasHost = nasHost;
            m_Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password));
            SocketsHttpHandler handler = new()
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = TimeSpan.FromSeconds(8),
                ConnectCallback = async (context, cancellation) =>
                {
                    Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(IPAddress.Parse(m_NasHost),
                            context.DnsEndPoint.Port, cancellation).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch { socket.Dispose(); throw; }
                }
            };
            m_Http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(15) };
        }

        public void Dispose() => m_Http.Dispose();

        public static string ArchiveKey(Guid region, Guid id)
        {
            if (region == Guid.Empty || id == Guid.Empty)
                throw new ArgumentException("Invalid managed IAR identifiers.");
            return region.ToString("D") + "/" + id.ToString("D") + ".iar";
        }

        public static string MarkerKey(Guid region, Guid id) =>
            ArchiveKey(region, id) + ".ogl-retention";

        public void EnsureRegion(Guid region)
        {
            if (region == Guid.Empty) throw new ArgumentException("Invalid region.");
            using HttpRequestMessage req = Request("MKCOL", region.ToString("D") + "/");
            using HttpResponseMessage reply = Send(req);
            if (reply.StatusCode != HttpStatusCode.Created &&
                reply.StatusCode != HttpStatusCode.MethodNotAllowed &&
                reply.StatusCode != HttpStatusCode.Conflict)
                Check(reply);
            // A preexisting directory is acceptable; PUT/GET will validate access.
        }

        public void CreateMarker(Guid region, Guid id)
        {
            using HttpRequestMessage req = Request("PUT", MarkerKey(region, id));
            req.Headers.TryAddWithoutValidation("If-None-Match", "*");
            req.Content = new ByteArrayContent(Array.Empty<byte>());
            using HttpResponseMessage reply = Send(req);
            Check(reply);
        }

        public void Upload(Guid region, Guid id, string localPath, long maxBytes)
        {
            string key = ArchiveKey(region, id);
            using FileStream file = File.OpenRead(localPath);
            if (file.Length < 1 || file.Length > maxBytes)
                throw new IOException("IAR WebDAV upload exceeds configured quota.");
            using HttpRequestMessage req = Request("PUT", key);
            req.Headers.TryAddWithoutValidation("If-None-Match", "*");
            req.Content = new StreamContent(file);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            using HttpResponseMessage response = Send(req);
            Check(response);
        }

        public long Size(Guid region, Guid id)
        {
            using HttpRequestMessage req = Request("HEAD", ArchiveKey(region, id));
            using HttpResponseMessage response = Send(req);
            if (response.StatusCode == HttpStatusCode.NotFound) return -1;
            Check(response);
            return response.Content.Headers.ContentLength ?? -1;
        }

        public void Download(Guid region, Guid id, string localPath, long maxBytes)
        {
            using HttpRequestMessage req = Request("GET", ArchiveKey(region, id));
            using HttpResponseMessage response = Send(req);
            Check(response);
            if (response.Content.Headers.ContentLength is long len && len > maxBytes)
                throw new IOException("Remote IAR exceeds size limit.");
            using Stream input = response.Content.ReadAsStream();
            using FileStream output = new(localPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[65536];
            long total = 0;
            int count;
            try
            {
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += count;
                    if (total > maxBytes) throw new IOException("Remote IAR quota exceeded.");
                    output.Write(buffer, 0, count);
                }
                output.Flush(true);
            }
            catch
            {
                output.Dispose();
                File.Delete(localPath);
                throw;
            }
        }

        // WebDAV is not required to support Range. A 200 response is read as
        // a forward-only stream and skipped in bounded memory, never buffered.
        public byte[] DownloadChunk(Guid region, Guid id, long offset,
            int length, long knownSize)
        {
            if (length <= 0 || length > OglIarTransferPolicy.MaximumChunkBytes ||
                offset < 0 || offset >= knownSize || knownSize <= 0)
                throw new ArgumentException("Invalid IAR read range.");
            int take = (int)Math.Min(length, knownSize - offset);
            using HttpRequestMessage req = Request("GET", ArchiveKey(region, id));
            req.Headers.Range = new RangeHeaderValue(offset, offset + take - 1);
            using HttpResponseMessage response = Send(req);
            Check(response);
            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.PartialContent)
                throw new IOException("Unexpected IAR WebDAV range status.");
            long skip = offset;
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                ContentRangeHeaderValue range = response.Content.Headers.ContentRange;
                if (range?.From != offset || range?.To != offset + take - 1 ||
                    (range.HasLength && range.Length != knownSize))
                    throw new IOException("Invalid IAR WebDAV Content-Range.");
                skip = 0;
            }
            using Stream stream = response.Content.ReadAsStream();
            byte[] scratch = new byte[65536];
            while (skip > 0)
            {
                int n = stream.Read(scratch, 0, (int)Math.Min(skip, scratch.Length));
                if (n <= 0) throw new IOException("Unexpected end of IAR WebDAV stream.");
                skip -= n;
            }
            byte[] output = new byte[take];
            stream.ReadExactly(output);
            return output;
        }

        public string Digest(Guid region, Guid id, HashAlgorithm algorithm,
            long maxBytes)
        {
            using HttpRequestMessage req = Request("GET", ArchiveKey(region, id));
            using HttpResponseMessage response = Send(req);
            Check(response);
            if (response.Content.Headers.ContentLength is long len && len > maxBytes)
                throw new IOException("IAR WebDAV digest quota exceeded.");
            using Stream input = response.Content.ReadAsStream();
            byte[] buffer = new byte[65536];
            long total = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > maxBytes) throw new IOException("IAR WebDAV digest quota exceeded.");
                algorithm.TransformBlock(buffer, 0, read, buffer, 0);
            }
            algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(algorithm.Hash).ToLowerInvariant();
        }

        public void Delete(Guid region, Guid id)
        {
            DeleteKey(ArchiveKey(region, id));
            DeleteKey(MarkerKey(region, id));
        }

        public void DeleteKey(string key)
        {
            using HttpRequestMessage req = Request("DELETE", key);
            using HttpResponseMessage reply = Send(req);
            if (reply.StatusCode != HttpStatusCode.NotFound) Check(reply);
        }

        // Retention never enumerates arbitrary shares or directories. It only
        // inspects one UUID region folder beneath the pinned WebDAV root.
        public System.Collections.Generic.IEnumerable<Guid> ListArchiveIds(Guid region)
        {
            using HttpRequestMessage req = Request("PROPFIND", region.ToString("D") + "/");
            req.Headers.TryAddWithoutValidation("Depth", "1");
            req.Content = new StringContent(
                "<?xml version=\"1.0\"?><d:propfind xmlns:d=\"DAV:\"><d:prop><d:resourcetype/></d:prop></d:propfind>",
                Encoding.UTF8, "application/xml");
            using HttpResponseMessage response = Send(req);
            if (response.StatusCode == HttpStatusCode.NotFound) yield break;
            if (response.StatusCode != (HttpStatusCode)207) Check(response);
            using Stream stream = response.Content.ReadAsStream();
            using MemoryStream output = new();
            byte[] buf = new byte[8192];
            int n;
            while ((n = stream.Read(buf, 0, buf.Length)) != 0)
            {
                if (output.Length + n > 2 * 1024 * 1024)
                    throw new IOException("IAR WebDAV directory listing too large.");
                output.Write(buf, 0, n);
            }
            output.Position = 0;
            XDocument xml = XDocument.Load(output,
                LoadOptions.None);
            XNamespace ns = "DAV:";
            string prefix = new Uri(m_Root, region.ToString("D") + "/").AbsolutePath;
            foreach (XElement entry in xml.Descendants(ns + "response"))
            {
                string href = (string)entry.Element(ns + "href");
                if (string.IsNullOrWhiteSpace(href)) continue;
                string path = Uri.UnescapeDataString(
                    Uri.TryCreate(href, UriKind.Absolute, out Uri absolute)
                        ? absolute.AbsolutePath : href.Split('?')[0]);
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string name = path.Substring(prefix.Length);
                if (name.Contains('/') || !name.EndsWith(".iar", StringComparison.Ordinal)) continue;
                if (Guid.TryParseExact(name.Substring(0, name.Length - 4),
                    "D", out Guid id) && id != Guid.Empty)
                    yield return id;
            }
        }

        public bool MarkerExists(Guid region, Guid id)
        {
            using HttpRequestMessage req = Request("HEAD", MarkerKey(region, id));
            using HttpResponseMessage response = Send(req);
            if (response.StatusCode == HttpStatusCode.NotFound) return false;
            Check(response);
            return true;
        }

        private HttpRequestMessage Request(string method, string path)
        {
            // Key components are generated from GUIDs only.
            if (string.IsNullOrWhiteSpace(path) ||
                path.Contains("..", StringComparison.Ordinal) ||
                path.IndexOfAny(new[] { '?', '#', '%', '\\' }) >= 0)
                throw new ArgumentException("Invalid WebDAV resource path.");
            HttpRequestMessage request = new(new HttpMethod(method), new Uri(m_Root, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", m_Auth);
            return request;
        }

        private HttpResponseMessage Send(HttpRequestMessage request)
        {
            try { return m_Http.Send(request, HttpCompletionOption.ResponseHeadersRead); }
            catch (Exception e) when (e is HttpRequestException or
                TaskCanceledException or InvalidOperationException)
            { throw new IOException("IAR WebDAV transport unavailable.", e); }
        }

        private static void Check(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new IOException("IAR WebDAV HTTP " + (int)response.StatusCode);
        }
    }
}
