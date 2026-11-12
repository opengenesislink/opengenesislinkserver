// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Server.Base;

namespace OpenSim.Server.Handlers.Inventory
{
    /// <summary>
    /// Public Hypergrid inventory authorization boundary. This is not applied
    /// to the separate private/internal XInventory connector.
    /// Peer public keys are pinned by the operator; no unsigned HTTP version
    /// assertion can confer trust or cause automatic legacy downgrade.
    /// </summary>
    public sealed class HGSP2InventoryGuard : IDisposable
    {
        private readonly ECDsa localPublic;
        private readonly Dictionary<string, ECDsa> pinnedPeers = new(StringComparer.Ordinal);
        private readonly IHGTravelingData traveling;
        private readonly HGSP2Authorization.ReplayWindow replay = new();
        private readonly string home;
        public bool Enabled { get; }
        public string Status => Enabled ? "secure-only; no implicit legacy downgrade" : "disabled";

        public HGSP2InventoryGuard(IConfigSource config)
        {
            IConfig section = config.Configs["OGLHypergridSecurity"];
            Enabled = section != null && section.GetBoolean("Enabled", false);
            if (!Enabled)
                return;

            home = section.GetString("LocalGridURI", "").TrimEnd('/');
            if (!Uri.TryCreate(home, UriKind.Absolute, out Uri homeUri) ||
                homeUri.Scheme != Uri.UriSchemeHttps && homeUri.Scheme != Uri.UriSchemeHttp ||
                String.IsNullOrWhiteSpace(home))
                throw new InvalidOperationException("[HGSP2] LocalGridURI must be a valid configured grid origin.");

            // The local home-grid signing key is loaded ONLY from private process
            // environment; never from a public endpoint or caller request.
            string privatePem = Environment.GetEnvironmentVariable("OGL_HGSP2_PRIVATE_KEY_PEM");
            if (String.IsNullOrWhiteSpace(privatePem))
                throw new InvalidOperationException("[HGSP2] Signing key is missing; public HG inventory cannot start insecurely.");
            using ECDsa privateKey = ECDsa.Create();
            privateKey.ImportFromPem(privatePem.Replace("\\n", "\n"));
            localPublic = ECDsa.Create();
            localPublic.ImportSubjectPublicKeyInfo(privateKey.ExportSubjectPublicKeyInfo(), out _);

            // PinnedPeers: https://partnerA.example:8002|BASE64-SPKI,https://partnerB...|BASE64-SPKI.
            // A peer not explicitly pinned is never recognized as an HGSP2 peer.
            string peers = section.GetString("PinnedPeers", "");
            foreach (string peer in peers.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = peer.Trim().Split('|');
                if (pair.Length != 2)
                    throw new InvalidOperationException("[HGSP2] Malformed pinned peer entry.");
                string uri = pair[0].TrimEnd('/');
                if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri peerUri) ||
                    (peerUri.Scheme != "https" && peerUri.Scheme != "http") ||
                    uri == home || pinnedPeers.Count >= 64 || pinnedPeers.ContainsKey(uri))
                    throw new InvalidOperationException("[HGSP2] Invalid or duplicated pinned peer identity.");
                var key = ECDsa.Create();
                try { key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(pair[1]), out _); }
                catch { key.Dispose(); throw new InvalidOperationException("[HGSP2] Invalid pinned peer public key."); }
                pinnedPeers.Add(uri, key);
            }

            IConfig db = config.Configs["DatabaseService"];
            IConfig ua = config.Configs["UserAgentService"];
            string provider = ua?.GetString("StorageProvider",
                db?.GetString("StorageProvider", "")) ?? "";
            string connection = ua?.GetString("ConnectionString",
                db?.GetString("ConnectionString", "")) ?? "";
            string realm = ua?.GetString("Realm", "hg_traveling_data") ?? "hg_traveling_data";
            if (String.IsNullOrWhiteSpace(provider) || String.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("[HGSP2] Authoritative travel database configuration is missing.");
            traveling = ServerUtils.LoadPlugin<IHGTravelingData>(provider, new object[] { connection, realm });
            if (traveling == null)
                throw new InvalidOperationException("[HGSP2] Travel database unavailable; public inventory denied.");
        }

        public bool Authorize(string grantToken, string proofToken, byte[] body,
            string method, Dictionary<string, object> form)
        {
            if (!Enabled || form == null || HGSP2Authorization.RequiredScope(method) == null)
                return false;
            // Explicit owner and principal consistency. Item/folder ownership
            // and suitcase scope are also validated by HGSuitcaseInventoryService.
            string owner = null;
            foreach (string name in new[] { "PRINCIPAL", "Owner", "OWNER" })
            {
                if (!form.TryGetValue(name, out object value)) continue;
                if (!Guid.TryParse(value?.ToString(), out Guid candidate) || candidate == Guid.Empty)
                    return false;
                string id = candidate.ToString("D");
                if (owner != null && !String.Equals(owner, id, StringComparison.OrdinalIgnoreCase))
                    return false;
                owner = id;
            }
            // Methods that specify no owner (e.g. PURGEFOLDER) cannot be
            // associated with an authenticated principal; fail closed.
            if (owner == null || String.IsNullOrWhiteSpace(grantToken) ||
                String.IsNullOrWhiteSpace(proofToken)) return false;
            foreach (KeyValuePair<string, ECDsa> peer in pinnedPeers)
            {
                if (HGSP2Authorization.AuthorizeInventory(
                    grantToken, proofToken, body, method, owner, home, peer.Key,
                    localPublic, peer.Value, replay, VerifyTravel,
                    DateTimeOffset.UtcNow))
                    return true;
            }
            return false;
        }

        private bool VerifyTravel(string avatar, string session, string trip, string destination)
        {
            if (!UUID.TryParse(avatar, out UUID agent) || agent == UUID.Zero ||
                !UUID.TryParse(session, out UUID sessionId) || sessionId == UUID.Zero)
                return false;
            HGTravelingData record = traveling.Get(sessionId);
            if (record == null || record.UserID != agent || record.Data == null)
                return false;
            if (!record.Data.TryGetValue("HGSP2Trip", out string boundTrip) ||
                !String.Equals(boundTrip, trip, StringComparison.OrdinalIgnoreCase) ||
                !record.Data.TryGetValue("GridExternalName", out string boundGrid) ||
                !String.Equals(boundGrid.TrimEnd('/'), destination, StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        public void Dispose()
        {
            localPublic?.Dispose();
            foreach (ECDsa peer in pinnedPeers.Values) peer.Dispose();
        }
    }
}
