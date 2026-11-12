// SPDX-License-Identifier: MPL-2.0
// HGSP 2.0 cryptographic building block. Does not change legacy Hypergrid
// wire formats or grant authority to self-asserted principals.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenSim.Framework
{
    /// <summary>
    /// ES256/P-256 signed, audience-bound grants and per-operation proofs.
    /// A home-grid signed grant is necessary but NOT sufficient: the visiting
    /// grid must prove possession of its pinned private key on each request.
    /// The resource handler must independently verify the active travel
    /// session, inventory owner and authorization scope before dispatch.
    /// </summary>
    public static class HGSP2Authorization
    {
        public const string GrantHeader = "X-OGL-HGSP2-Grant";
        public const string ProofHeader = "X-OGL-HGSP2-Proof";
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions { PropertyNameCaseInsensitive = false };
        private static readonly byte[] JwtHeader =
            Encoding.UTF8.GetBytes("{\"typ\":\"HGSP2\",\"alg\":\"ES256\"}");

        public sealed class Grant
        {
            public string Version { get; set; } = "2.0";
            public string Issuer { get; set; }
            public string Audience { get; set; }
            public string Avatar { get; set; }
            public string Session { get; set; }
            public string Trip { get; set; }
            public string Service { get; set; } = "xinventory";
            public string Scopes { get; set; }
            public string PeerKeyId { get; set; }
            public string TokenId { get; set; }
            public long NotBefore { get; set; }
            public long Expires { get; set; }
        }

        public sealed class Proof
        {
            public string Version { get; set; } = "2.0";
            public string Issuer { get; set; }
            public string Audience { get; set; }
            public string Avatar { get; set; }
            public string Session { get; set; }
            public string Trip { get; set; }
            public string Service { get; set; } = "xinventory";
            public string Scope { get; set; }
            public string BodySHA256 { get; set; }
            public string GrantSHA256 { get; set; }
            public string Nonce { get; set; }
            public long Issued { get; set; }
            public long Expires { get; set; }
        }

        public sealed class ReplayWindow
        {
            private readonly ConcurrentDictionary<string, long> used =
                new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
            private long checks;
            public int Count => used.Count;
            public bool TryReserve(string key, long expiry, long now)
            {
                if (String.IsNullOrEmpty(key) || key.Length > 512 || expiry <= now)
                    return false;
                // Finite in-memory bound; return deny rather than disabling
                // replay protection when the node runs out of capacity.
                if (used.Count >= 50000)
                {
                    foreach (KeyValuePair<string, long> pair in used)
                        if (pair.Value < now)
                            used.TryRemove(pair.Key, out _);
                    if (used.Count >= 50000) return false;
                }
                if (System.Threading.Interlocked.Increment(ref checks) % 1000 == 0)
                {
                    foreach (KeyValuePair<string, long> pair in used)
                        if (pair.Value < now)
                            used.TryRemove(pair.Key, out _);
                }
                return used.TryAdd(key, expiry);
            }
        }

        public static string KeyId(string publicSpkiBase64)
        {
            byte[] publicKey = Convert.FromBase64String(publicSpkiBase64);
            return Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
        }

        public static string PublicKey(ECDsa key)
            => Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

        public static string Digest(byte[] bytes)
            => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        public static string Sign<T>(T payload, ECDsa privateKey)
        {
            if (payload == null || privateKey == null)
                throw new ArgumentNullException(payload == null ? nameof(payload) : nameof(privateKey));
            string input = B64(JwtHeader) + "." + B64(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
            byte[] signature = privateKey.SignData(Encoding.ASCII.GetBytes(input),
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (signature.Length != 64)
                throw new CryptographicException("HGSP2 ES256 P1363 signature must contain 64 bytes.");
            return input + "." + B64(signature);
        }

        public static bool TryReadSigned<T>(string compact, ECDsa trustedSigner, out T claims)
            where T : class
        {
            claims = null;
            if (String.IsNullOrWhiteSpace(compact) || compact.Length > 8192 ||
                trustedSigner == null) return false;
            string[] parts = compact.Split('.');
            if (parts.Length != 3) return false;
            try
            {
                byte[] header = UnB64(parts[0]);
                if (!header.AsSpan().SequenceEqual(JwtHeader)) return false;
                byte[] signature = UnB64(parts[2]);
                if (signature.Length != 64) return false;
                if (!trustedSigner.VerifyData(
                    Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), signature,
                    HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                    return false;
                byte[] data = UnB64(parts[1]);
                if (data.Length > 4096) return false;
                claims = JsonSerializer.Deserialize<T>(data, JsonOptions);
                return claims != null;
            }
            catch (Exception error) when (error is FormatException ||
                error is JsonException || error is CryptographicException ||
                error is ArgumentException)
            {
                return false;
            }
        }

        private static string B64(byte[] bytes) => Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] UnB64(string text)
        {
            if (text == null || text.Length > 12000) throw new FormatException();
            foreach (char c in text)
                if (!((c >= 'A' && c <= 'Z') ||
                      (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') || c == '-' || c == '_'))
                    throw new FormatException();
            string padded = text.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
        }

        public static string RequiredScope(string method)
        {
            switch (method)
            {
                case "GETINVENTORYSKELETON":
                case "GETROOTFOLDER":
                case "GETFOLDERFORTYPE":
                case "GETFOLDERCONTENT":
                case "GETMULTIPLEFOLDERSCONTENT":
                case "GETFOLDERITEMS":
                case "GETITEM":
                case "GETMULTIPLEITEMS":
                case "GETFOLDER":
                case "GETACTIVEGESTURES":
                case "GETASSETPERMISSIONS":
                    return "inventory.read";
                case "ADDFOLDER":
                case "UPDATEFOLDER":
                case "MOVEFOLDER":
                case "DELETEFOLDERS":
                case "PURGEFOLDER":
                case "ADDITEM":
                case "UPDATEITEM":
                case "MOVEITEMS":
                case "DELETEITEMS":
                    return "inventory.write";
                default:
                    return null;
            }
        }

        private static bool ScopeAllowed(string granted, string required)
        {
            if (String.IsNullOrWhiteSpace(granted) || String.IsNullOrWhiteSpace(required))
                return false;
            foreach (string item in granted.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (String.Equals(item, required, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>
        /// Validates a home-signed travel grant and destination-signed request
        /// proof. No trust-on-first-use or HTTP-reported identity is accepted.
        /// The sessionExists callback MUST read authoritative, revocable travel
        /// state; its arguments are avatar, session, trip, destination grid.
        /// </summary>
        public static bool AuthorizeInventory(
            string grantText, string proofText, byte[] formBody, string method,
            string ownerId, string homeGrid, string destinationGrid,
            ECDsa trustedHomeKey, ECDsa pinnedDestinationKey,
            ReplayWindow replay, Func<string, string, string, string, bool> sessionExists,
            DateTimeOffset now)
        {
            if (formBody == null || formBody.Length > 2 * 1024 * 1024 ||
                replay == null || sessionExists == null ||
                String.IsNullOrEmpty(ownerId) || String.IsNullOrEmpty(homeGrid) ||
                String.IsNullOrEmpty(destinationGrid))
                return false;
            string requiredScope = RequiredScope(method);
            if (requiredScope == null ||
                !TryReadSigned(grantText, trustedHomeKey, out Grant grant) ||
                !TryReadSigned(proofText, pinnedDestinationKey, out Proof proof))
                return false;

            long stamp = now.ToUnixTimeSeconds();
            // Enforce 2-minute proof TTL with 30-second clock skew and a
            // maximum grant TTL of 8 hours; requests are never accepted just
            // because a token has a self-declared expiry in the far future.
            if (grant.Version != "2.0" || proof.Version != "2.0" ||
                grant.Service != "xinventory" || proof.Service != "xinventory" ||
                grant.Issuer != homeGrid || grant.Audience != destinationGrid ||
                proof.Issuer != destinationGrid || proof.Audience != homeGrid ||
                !Guid.TryParse(grant.Avatar, out Guid owner) || owner == Guid.Empty ||
                !String.Equals(owner.ToString("D"), ownerId, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(proof.Avatar, grant.Avatar, StringComparison.OrdinalIgnoreCase) ||
                grant.Session != proof.Session || grant.Trip != proof.Trip ||
                !Guid.TryParse(grant.Session, out Guid sid) || sid == Guid.Empty ||
                !Guid.TryParse(grant.Trip, out Guid trip) || trip == Guid.Empty ||
                !Guid.TryParse(grant.TokenId, out Guid tokenId) || tokenId == Guid.Empty ||
                !Guid.TryParse(proof.Nonce, out Guid nonce) || nonce == Guid.Empty ||
                grant.PeerKeyId != KeyId(PublicKey(pinnedDestinationKey)) ||
                !ScopeAllowed(grant.Scopes, requiredScope) ||
                proof.Scope != requiredScope ||
                grant.NotBefore > stamp + 30 || grant.Expires <= stamp ||
                grant.Expires - grant.NotBefore > 8 * 3600 ||
                proof.Issued > stamp + 30 || proof.Issued < stamp - 120 ||
                proof.Expires <= stamp || proof.Expires > proof.Issued + 120 ||
                !String.Equals(proof.BodySHA256, Digest(formBody), StringComparison.Ordinal) ||
                !String.Equals(proof.GrantSHA256, Digest(Encoding.UTF8.GetBytes(grantText)), StringComparison.Ordinal))
                return false;
            if (!sessionExists(grant.Avatar, grant.Session, grant.Trip, grant.Audience))
                return false;
            return replay.TryReserve(grant.PeerKeyId + ":" + grant.Session + ":" + proof.Nonce,
                proof.Expires, stamp);
        }
    }
}
