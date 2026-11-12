// SPDX-License-Identifier: MPL-2.0
using System;
using System.Net;
using Nini.Config;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Explicit, two-switch S3 configuration. Reading an absent or disabled
    /// section does not access secrets, construct an SDK client or contact S3.
    /// This does not change the legacy FSAssets StorageProvider setting.
    /// </summary>
    public sealed class OGLS3StorageOptions
    {
        public bool Enabled { get; private set; }
        public string Endpoint { get; private set; }
        public string Bucket { get; private set; }
        public string Region { get; private set; }
        public string AccessKey { get; private set; }
        public string SecretKey { get; private set; }
        public bool ForcePathStyle { get; private set; }
        public int ConnectionTimeoutSeconds { get; private set; }
        public int RequestTimeoutSeconds { get; private set; }
        public int MaxRetryAttempts { get; private set; }
        public bool HealthCheckEnabled { get; private set; }

        public static OGLS3StorageOptions Read(IConfigSource config)
        {
            IConfig section = config?.Configs["OGLS3Storage"];
            if (section == null || !section.GetBoolean("Enabled", false))
                return new OGLS3StorageOptions { Enabled = false };

            var options = new OGLS3StorageOptions
            {
                Enabled = true,
                Endpoint = section.GetString("Endpoint", "http://127.0.0.1:9000").Trim(),
                Bucket = section.GetString("Bucket", "ogl-assets").Trim(),
                Region = section.GetString("Region", "us-east-1").Trim(),
                ForcePathStyle = section.GetBoolean("ForcePathStyle", true),
                ConnectionTimeoutSeconds = section.GetInt("ConnectionTimeoutSeconds", 10),
                RequestTimeoutSeconds = section.GetInt("RequestTimeoutSeconds", 60),
                MaxRetryAttempts = section.GetInt("MaxRetryAttempts", 3),
                HealthCheckEnabled = section.GetBoolean("HealthCheckEnabled", false)
            };

            // Prefer the existing environment-based secret pattern. Never
            // include these fields in exceptions, diagnostics or ToString().
            options.AccessKey = Environment.GetEnvironmentVariable("OGL_S3_ACCESS_KEY");
            if (String.IsNullOrWhiteSpace(options.AccessKey))
                options.AccessKey = section.GetString("AccessKey", "");
            options.SecretKey = Environment.GetEnvironmentVariable("OGL_S3_SECRET_KEY");
            if (String.IsNullOrWhiteSpace(options.SecretKey))
                options.SecretKey = section.GetString("SecretKey", "");

            if (String.IsNullOrWhiteSpace(options.AccessKey) ||
                String.IsNullOrWhiteSpace(options.SecretKey))
                throw new InvalidOperationException("OGL S3 requires credentials (prefer OGL_S3_ACCESS_KEY and OGL_S3_SECRET_KEY).");

            if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps) ||
                !String.IsNullOrEmpty(endpoint.UserInfo) ||
                !String.IsNullOrEmpty(endpoint.Query) ||
                !String.IsNullOrEmpty(endpoint.Fragment) ||
                endpoint.AbsolutePath != "/")
                throw new InvalidOperationException("OGL S3 Endpoint must be an HTTP(S) origin without embedded credentials, query or path.");

            bool ssl = section.GetBoolean("UseSSL", false);
            if (ssl != (endpoint.Scheme == Uri.UriSchemeHttps))
                throw new InvalidOperationException("OGL S3 UseSSL must agree with the configured Endpoint scheme.");

            // Cleartext is supported only over local/private networks (e.g.
            // WireGuard). Public S3 endpoints must use TLS with normal validation.
            if (!ssl && !IsPrivateEndpoint(endpoint.Host))
                throw new InvalidOperationException("Plain HTTP S3 requires a loopback or private IP Endpoint; use HTTPS otherwise.");

            if (String.IsNullOrWhiteSpace(options.Bucket) ||
                options.Bucket.Length > 63 ||
                options.Bucket.IndexOfAny(new[] { '/', '\\', ' ', ':', '@' }) >= 0 ||
                String.IsNullOrWhiteSpace(options.Region) ||
                options.ConnectionTimeoutSeconds < 1 || options.ConnectionTimeoutSeconds > 120 ||
                options.RequestTimeoutSeconds < 1 || options.RequestTimeoutSeconds > 600 ||
                options.MaxRetryAttempts < 1 || options.MaxRetryAttempts > 6)
                throw new InvalidOperationException("OGL S3 Bucket, Region or timeout/retry settings are invalid.");

            return options;
        }

        private static bool IsPrivateEndpoint(string host)
        {
            if (String.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
                return true;
            if (!IPAddress.TryParse(host, out IPAddress address))
                return false;
            if (IPAddress.IsLoopback(address))
                return true;
            byte[] bytes = address.GetAddressBytes();
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return bytes[0] == 10 ||
                    (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                    (bytes[0] == 192 && bytes[1] == 168);
            return bytes.Length == 16 && (bytes[0] & 0xFE) == 0xFC; // IPv6 ULA
        }
    }
}
