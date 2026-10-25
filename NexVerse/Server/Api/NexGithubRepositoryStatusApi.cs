// SPDX-License-Identifier: MPL-2.0
// Source-of-truth GitHub release/commit/CI metadata, with bounded cache.
// No credentials, shell execution, or operator-defined remote URLs.
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenSim.Framework.Servers.HttpServer;

namespace NexVerse.Server.Api
{
    internal sealed class NexGithubRepositoryStatusApi
    {
        private const string Base = "https://api.github.com/repos/opengenesislink/opengenesislinkserver";
        private static readonly HttpClient s_Client = CreateClient();
        private static readonly SemaphoreSlim s_Refresh = new SemaphoreSlim(1, 1);
        private static byte[] s_Cached;
        private static DateTimeOffset s_LastFetch = DateTimeOffset.MinValue;

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenGenesisLINK-WorldAPI/1.0");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.KeepAlive = false;
            response.AddHeader("Cache-Control", "private, no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            response.ContentType = "application/json; charset=utf-8";

            if (request == null || request.HttpMethod != "GET" ||
                (request.UriPath ?? "").TrimEnd('/') != "/api/v1/repository/status")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(
                    new { error = "method_not_allowed" });
                return;
            }

            // The short, process-local cache prevents 60/h anonymous GitHub API
            // rate-limit exhaustion and avoids any database writes.
            try
            {
                if (s_Cached != null &&
                    DateTimeOffset.UtcNow - s_LastFetch < TimeSpan.FromMinutes(5))
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.RawBuffer = s_Cached;
                    return;
                }

                if (!s_Refresh.Wait(TimeSpan.FromSeconds(8)))
                {
                    WriteFallback(response);
                    return;
                }
                try
                {
                    if (s_Cached == null ||
                        DateTimeOffset.UtcNow - s_LastFetch >= TimeSpan.FromMinutes(5))
                    {
                        var payload = FetchStatus().GetAwaiter().GetResult();
                        if (payload != null)
                        {
                            s_Cached = payload;
                            s_LastFetch = DateTimeOffset.UtcNow;
                        }
                    }
                    WriteFallback(response);
                }
                finally { s_Refresh.Release(); }
            }
            catch (Exception)
            {
                // GitHub is an external dependency. Never expose credentials,
                // stack traces, HTML proxy errors or fabricated release tags.
                WriteFallback(response);
            }
        }

        private static void WriteFallback(IOSHttpResponse response)
        {
            if (s_Cached != null)
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.RawBuffer = s_Cached;
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(
                    new { error = "github_unavailable",
                        source = "https://github.com/opengenesislink/opengenesislinkserver" });
            }
        }

        private static async Task<JsonElement?> Read(string url)
        {
            // A fixed URL set only. No request parameters or redirect to
            // an attacker-supplied domain. GitHub metadata is public.
            try
            {
                using HttpResponseMessage response = await s_Client.GetAsync(url,
                    HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;
                byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (bytes.Length > 512 * 1024) return null;
                using JsonDocument document = JsonDocument.Parse(bytes);
                return document.RootElement.Clone();
            }
            catch (Exception) { return null; }
        }

        private static string String(JsonElement? root, params string[] path)
        {
            if (!root.HasValue) return null;
            JsonElement value = root.Value;
            foreach (string name in path)
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty(name, out value)) return null;
            }
            return value.ValueKind == JsonValueKind.String ? value.GetString() :
                value.ValueKind == JsonValueKind.Number ? value.ToString() : null;
        }

        private static async Task<byte[]> FetchStatus()
        {
            var result = await Task.WhenAll(
                Read(Base + "/releases/latest"),
                Read(Base + "/commits/main"),
                Read(Base + "/actions/workflows/nexjast-ci.yml/runs?branch=main&per_page=1")
            ).ConfigureAwait(false);

            JsonElement? release = result[0], commit = result[1], runs = result[2];
            JsonElement? lastRun = null;
            if (runs.HasValue &&
                runs.Value.TryGetProperty("workflow_runs", out JsonElement list) &&
                list.ValueKind == JsonValueKind.Array && list.GetArrayLength() != 0)
                lastRun = list[0];

            if (!release.HasValue && !commit.HasValue && !runs.HasValue)
                return null;

            object stable = release.HasValue ? new
            {
                tag_name = String(release, "tag_name"),
                name = String(release, "name"),
                published_at = String(release, "published_at"),
                body = String(release, "body")
            } : null;
            object development = commit.HasValue ? new
            {
                sha = String(commit, "sha"),
                commit = new {
                    message = String(commit, "commit", "message"),
                    author = new { date = String(commit, "commit", "author", "date") }
                }
            } : null;
            object ci = runs.HasValue ? new
            {
                workflow_runs = lastRun.HasValue ? new object[] {new
                {
                    run_number = String(lastRun, "run_number"),
                    name = String(lastRun, "name"),
                    head_sha = String(lastRun, "head_sha"),
                    status = String(lastRun, "status"),
                    conclusion = String(lastRun, "conclusion")
                }} : Array.Empty<object>()
            } : null;

            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                source = "github",
                release = stable,
                commit = development,
                ci,
                fail = new[] { !release.HasValue ? 0 : -1,
                               !commit.HasValue ? 1 : -1,
                               !runs.HasValue ? 2 : -1 }
                              .Where(x => x >= 0).ToArray(),
                at = DateTimeOffset.UtcNow.ToString("O")
            });
        }
    }
}
