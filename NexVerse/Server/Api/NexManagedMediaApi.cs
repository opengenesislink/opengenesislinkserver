// SPDX-License-Identifier: MPL-2.0
// Media uploads, publication and owner-bound inventory images.
using System;
using System.IO;
using System.Net;
using System.Drawing;
using OpenMetaverse.Imaging;
using System.Text.Json;
using log4net;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Groups;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class NexManagedMediaApi
    {
        private static readonly ILog s_Log = LogManager.GetLogger(typeof(NexManagedMediaApi));
        private const int MaxUploadBytes = 4 * 1024 * 1024;
        private const int MaxJsonBytes = 6 * 1024 * 1024;
        private readonly NexApiAuthenticator m_Auth;
        private readonly IUserAccountService m_Accounts;
        private readonly IInventoryService m_Inventory;
        private readonly IProfilesData m_Profiles;
        private readonly GroupsService m_Groups;
        private readonly Func<IAssetService> m_Assets;
        private readonly NexMediaCatalogStore m_Catalog;
        private readonly Action<UUID, IOSHttpResponse, int> m_Render;

        internal NexManagedMediaApi(NexApiAuthenticator auth,
            IUserAccountService accounts, IInventoryService inventory,
            IProfilesData profiles, GroupsService groups,
            Func<IAssetService> assets, NexMediaCatalogStore catalog,
            Action<UUID, IOSHttpResponse, int> render)
        {
            m_Auth = auth ?? throw new ArgumentNullException(nameof(auth));
            m_Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Inventory = inventory;
            m_Profiles = profiles;
            m_Groups = groups;
            m_Assets = assets ?? throw new ArgumentNullException(nameof(assets));
            m_Catalog = catalog;
            m_Render = render ?? throw new ArgumentNullException(nameof(render));
        }

        internal static bool CanHandle(string path, string method) =>
            path.StartsWith("/api/v1/media/uploads", StringComparison.Ordinal) ||
            path.StartsWith("/api/v1/media/assets/", StringComparison.Ordinal) ||
            path.StartsWith("/api/v1/media/marketplace/", StringComparison.Ordinal) ||
            path.StartsWith("/api/v1/media/inventory/", StringComparison.Ordinal) ||
            ((method == "PUT") &&
             (path.StartsWith("/api/v1/media/groups/", StringComparison.Ordinal) ||
              path.StartsWith("/api/v1/media/profiles/", StringComparison.Ordinal)));

        internal void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.AddHeader("Cache-Control", "private, no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            string path = (request.UriPath ?? "").TrimEnd('/');
            string method = request.HttpMethod ?? "";
            string[] parts = path.Trim('/').Split('/');
            try
            {
                if (path == "/api/v1/media/uploads" && method == "POST")
                {
                    Upload(request, response);
                    return;
                }

                // Strict segment matching; never expose an arbitrary legacy asset.
                if (parts.Length == 5 &&
                    parts[0] == "api" && parts[1] == "v1" && parts[2] == "media" &&
                    UUID.TryParse(parts[4], out UUID id) && !id.IsZero())
                {
                    if (parts[3] == "assets")
                    {
                        if (method == "GET") ReadAsset(request, response, id);
                        else if (method == "PATCH") ChangeVisibility(request, response, id);
                        else Error(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
                        return;
                    }
                    if (parts[3] == "inventory" && method == "GET")
                    {
                        ReadInventory(request, response, id);
                        return;
                    }
                }
                if (parts.Length == 6 && parts[0] == "api" &&
                    parts[1] == "v1" && parts[2] == "media" &&
                    UUID.TryParse(parts[4], out UUID entity) && !entity.IsZero() &&
                    parts[5] == "image")
                {
                    if (parts[3] == "marketplace")
                    {
                        if (method == "GET") ReadListing(request, response, entity);
                        else if (method == "PUT") PublishListing(request, response, entity);
                        else if (method == "DELETE") UnpublishListing(request, response, entity);
                        else Error(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
                        return;
                    }
                    if (method == "PUT" && parts[3] == "groups")
                    {
                        UpdateGroupImage(request, response, entity);
                        return;
                    }
                    if (method == "PUT" && parts[3] == "profiles")
                    {
                        UpdateProfileImage(request, response, entity, false);
                        return;
                    }
                }
                if (parts.Length == 6 && method == "PUT" &&
                    parts[0] == "api" && parts[1] == "v1" && parts[2] == "media" &&
                    parts[3] == "profiles" && parts[5] == "first-life" &&
                    UUID.TryParse(parts[4], out UUID avatar) && !avatar.IsZero())
                {
                    UpdateProfileImage(request, response, avatar, true);
                    return;
                }
                Error(response, HttpStatusCode.NotFound, "media_not_found");
            }
            catch (JsonException) { Error(response, HttpStatusCode.BadRequest, "invalid_json"); }
            catch (FormatException) { Error(response, HttpStatusCode.BadRequest, "invalid_media_format"); }
            catch (MySql.Data.MySqlClient.MySqlException e)
            {
                s_Log.Error("[OGL-MEDIA]: MariaDB publication failure", e);
                Error(response, HttpStatusCode.ServiceUnavailable, "media_catalog_unavailable");
            }
            catch (Exception e)
            {
                s_Log.Error("[OGL-MEDIA]: managed media request failed", e);
                Error(response, HttpStatusCode.ServiceUnavailable, "media_service_unavailable");
            }
        }

        private bool Ready(IOSHttpResponse response)
        {
            if (m_Catalog != null) return true;
            Error(response, HttpStatusCode.ServiceUnavailable, "media_catalog_unavailable");
            return false;
        }

        private bool Authorize(IOSHttpRequest request, IOSHttpResponse response,
            string scope, out NexPrincipal principal, out UserAccount account)
        {
            if (m_Auth.TryAuthenticate(request, scope, out principal, out account,
                out int status, out string code)) return true;
            Error(response, (HttpStatusCode)status, code);
            return false;
        }

        private bool Machine(IOSHttpRequest request, IOSHttpResponse response)
        {
            if (!Authorize(request, response, NexScopes.MediaPublish,
                out NexPrincipal principal, out _)) return false;
            if (!principal.Subject.StartsWith("api-key:", StringComparison.Ordinal))
            {
                Error(response, HttpStatusCode.Forbidden, "machine_key_required");
                return false;
            }
            return true;
        }

        private static JsonElement Body(IOSHttpRequest request)
        {
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[16384];
            int count;
            while ((count = request.InputStream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + count > MaxJsonBytes)
                    throw new FormatException("Oversized media JSON body");
                buffer.Write(chunk, 0, count);
            }
            buffer.Position = 0;
            using JsonDocument json = JsonDocument.Parse(buffer);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException("Object expected");
            return json.RootElement.Clone();
        }

        private static bool TryUuid(JsonElement obj, string name, out UUID value)
        {
            value = UUID.Zero;
            return obj.TryGetProperty(name, out JsonElement part) &&
                   part.ValueKind == JsonValueKind.String &&
                   UUID.TryParse(part.GetString(), out value) && !value.IsZero();
        }

        private static bool TryImageBytes(string mime, byte[] data)
        {
            if (data.Length < 32 || data.Length > MaxUploadBytes)
                return false;
            if (mime == "image/png")
            {
                if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4e ||
                    data[3] != 0x47 || data[12] != (byte)'I' ||
                    data[13] != (byte)'H' || data[14] != (byte)'D' || data[15] != (byte)'R')
                    return false;
                int w = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
                int h = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
                return w > 0 && w <= 2048 && h > 0 && h <= 2048;
            }
            if (mime != "image/jpeg" || data[0] != 0xff || data[1] != 0xd8)
                return false;
            // Scan JPEG SOF for bounded dimensions. Do not trust MIME alone.
            int p = 2;
            while (p + 9 < data.Length)
            {
                if (data[p++] != 0xff) return false;
                while (p < data.Length && data[p] == 0xff) ++p;
                if (p >= data.Length) return false;
                byte marker = data[p++];
                if (marker == 0xda || marker == 0xd9) return false;
                if (p + 1 >= data.Length) return false;
                int length = (data[p] << 8) | data[p + 1];
                if (length < 2 || p + length > data.Length) return false;
                if ((marker >= 0xc0 && marker <= 0xcf) &&
                    marker != 0xc4 && marker != 0xc8 && marker != 0xcc)
                {
                    if (length < 7) return false;
                    int h = (data[p + 3] << 8) | data[p + 4];
                    int w = (data[p + 5] << 8) | data[p + 6];
                    return w > 0 && w <= 2048 && h > 0 && h <= 2048;
                }
                p += length;
            }
            return false;
        }

        private void Upload(IOSHttpRequest request, IOSHttpResponse response)
        {
            if (!Ready(response)) return;
            // Residents upload for themselves; dedicated publisher machine
            // keys may upload for a local account explicitly named by the portal.
            bool resident = m_Auth.TryAuthenticate(request, NexScopes.ProfileWrite,
                out NexPrincipal actor, out UserAccount user, out _, out _) &&
                user != null && user.LocalToGrid;
            if (!resident && !Machine(request, response)) return;

            JsonElement body = Body(request);
            UUID owner = resident ? user.PrincipalID : UUID.Zero;
            if (resident)
            {
                if (body.TryGetProperty("owner_id", out JsonElement raw) &&
                    (!UUID.TryParse(raw.GetString(), out UUID supplied) || supplied != owner))
                {
                    Error(response, HttpStatusCode.Forbidden, "owner_mismatch");
                    return;
                }
            }
            else if (!TryUuid(body, "owner_id", out owner) ||
                     m_Accounts.GetUserAccount(UUID.Zero, owner) is not UserAccount existing ||
                     !existing.LocalToGrid)
            {
                Error(response, HttpStatusCode.BadRequest, "invalid_owner");
                return;
            }
            if (!body.TryGetProperty("content_type", out JsonElement contentType) ||
                contentType.ValueKind != JsonValueKind.String ||
                !body.TryGetProperty("data_base64", out JsonElement encoded) ||
                encoded.ValueKind != JsonValueKind.String)
            {
                Error(response, HttpStatusCode.BadRequest, "missing_image_data");
                return;
            }
            string mime = contentType.GetString();
            byte[] data;
            try { data = Convert.FromBase64String(encoded.GetString()); }
            catch (FormatException) { Error(response, HttpStatusCode.BadRequest, "invalid_base64"); return; }
            if (!TryImageBytes(mime, data))
            {
                Error(response, HttpStatusCode.UnsupportedMediaType, "invalid_image");
                return;
            }

            IAssetService assets = m_Assets();
            if (assets == null)
            {
                Error(response, HttpStatusCode.ServiceUnavailable, "media_asset_service_unavailable");
                return;
            }
            // Avatar/group/Viewer textures MUST be JPEG2000, not PNG/JPEG
            // bytes mislabeled as OpenSim AssetType.Texture.
            byte[] texture;
            try
            {
                using MemoryStream source = new MemoryStream(data, false);
                using Image original = Image.FromStream(source, false, true);
                if (original.Width <= 0 || original.Height <= 0 ||
                    original.Width > 2048 || original.Height > 2048)
                {
                    Error(response, HttpStatusCode.RequestEntityTooLarge, "media_dimensions_exceeded");
                    return;
                }
                using Bitmap bitmap = new Bitmap(original);
                texture = OpenJPEG.EncodeFromImage(bitmap, false);
                if (texture == null || texture.Length < 8 || texture.Length > MaxUploadBytes)
                {
                    Error(response, HttpStatusCode.UnsupportedMediaType, "media_transcode_failed");
                    return;
                }
            }
            catch (Exception e) when (e is ArgumentException ||
                                       e is PlatformNotSupportedException ||
                                       e is TypeInitializationException ||
                                       e is DllNotFoundException)
            {
                // Never store corrupt non-JPEG2000 bytes for the Viewer.
                s_Log.Warn("[OGL-MEDIA]: PNG/JPEG to JPEG2000 encoder unavailable or invalid (" +
                           e.GetType().Name + ")");
                Error(response, HttpStatusCode.ServiceUnavailable, "media_encoder_unavailable");
                return;
            }
            UUID id = UUID.Random();
            AssetBase asset = new AssetBase(id, "OpenGenesisLINK Media",
                (sbyte)AssetType.Texture, owner.ToString()) { Data = texture };
            string storedId = assets.Store(asset);
            if (!UUID.TryParse(storedId, out UUID committed) || committed.IsZero())
            {
                Error(response, HttpStatusCode.ServiceUnavailable, "media_asset_store_failed");
                return;
            }
            // On transient SQL errors an unreferenced immutable asset remains
            // in AssetService; never expose it via a free UUID route.
            m_Catalog.RecordUpload(committed, owner, mime);
            Reply(response, HttpStatusCode.Created, new
            {
                asset_id = committed.ToString(),
                owner_id = owner.ToString(),
                content_type = mime,
                image_url = "/api/v1/media/assets/" + committed,
                visibility = "private"
            });
        }

        private bool OwnerOrPublisher(IOSHttpRequest request, IOSHttpResponse response,
            NexMediaCatalogStore.MediaRecord media, out bool trustedPublisher)
        {
            trustedPublisher = false;
            if (m_Auth.TryAuthenticate(request, NexScopes.ProfileWrite,
                out NexPrincipal _, out UserAccount account, out _, out _) &&
                account != null && account.PrincipalID == media.Owner)
                return true;
            if (m_Auth.TryAuthenticate(request, NexScopes.MediaPublish,
                out NexPrincipal publisher, out _, out _, out _) &&
                publisher.Subject.StartsWith("api-key:", StringComparison.Ordinal))
            {
                trustedPublisher = true;
                return true;
            }
            Error(response, HttpStatusCode.Forbidden, "media_owner_required");
            return false;
        }

        private void ReadAsset(IOSHttpRequest request, IOSHttpResponse response, UUID assetId)
        {
            if (!Ready(response)) return;
            NexMediaCatalogStore.MediaRecord? item = m_Catalog.FindUpload(assetId);
            if (!item.HasValue)
            {
                Error(response, HttpStatusCode.NotFound, "media_not_found");
                return;
            }
            if (!item.Value.Public)
            {
                if (!Authorize(request, response, NexScopes.ProfileRead,
                    out NexPrincipal actor, out UserAccount account)) return;
                if ((account == null || account.PrincipalID != item.Value.Owner) &&
                    !actor.HasScope(NexScopes.AdminAll))
                {
                    Error(response, HttpStatusCode.NotFound, "media_not_found");
                    return;
                }
            }
            Render(request, response, assetId);
        }

        private void ReadInventory(IOSHttpRequest request, IOSHttpResponse response, UUID itemId)
        {
            if (!Authorize(request, response, NexScopes.InventoryRead,
                out _, out UserAccount user)) return;
            if (user == null || !user.LocalToGrid || m_Inventory == null)
            {
                Error(response, HttpStatusCode.NotFound, "media_not_found");
                return;
            }
            InventoryItemBase item = m_Inventory.GetItem(user.PrincipalID, itemId);
            if (item == null || item.Owner != user.PrincipalID ||
                (item.AssetType != (int)AssetType.Texture &&
                 item.AssetType != (int)AssetType.ImageJPEG &&
                 item.AssetType != (int)AssetType.TextureTGA &&
                 item.AssetType != (int)AssetType.ImageTGA))
            {
                Error(response, HttpStatusCode.NotFound, "media_not_found");
                return;
            }
            Render(request, response, item.AssetID);
        }

        private void ChangeVisibility(IOSHttpRequest request, IOSHttpResponse response, UUID assetId)
        {
            if (!Ready(response)) return;
            NexMediaCatalogStore.MediaRecord? item = m_Catalog.FindUpload(assetId);
            if (!item.HasValue) { Error(response, HttpStatusCode.NotFound, "media_not_found"); return; }
            if (!OwnerOrPublisher(request, response, item.Value, out _)) return;
            JsonElement body = Body(request);
            if (!body.TryGetProperty("public", out JsonElement mode) ||
                (mode.ValueKind != JsonValueKind.True && mode.ValueKind != JsonValueKind.False))
            {
                Error(response, HttpStatusCode.BadRequest, "invalid_visibility");
                return;
            }
            m_Catalog.ChangeVisibility(assetId, item.Value.Owner, mode.GetBoolean());
            Reply(response, HttpStatusCode.OK, new { asset_id = assetId.ToString(),
                visibility = mode.GetBoolean() ? "public" : "private" });
        }

        private void ReadListing(IOSHttpRequest request, IOSHttpResponse response, UUID listing)
        {
            if (!Ready(response)) return;
            UUID? asset = m_Catalog.FindListingImage(listing);
            if (!asset.HasValue) { Error(response, HttpStatusCode.NotFound, "media_not_found"); return; }
            Render(request, response, asset.Value);
        }

        private void PublishListing(IOSHttpRequest request, IOSHttpResponse response, UUID listing)
        {
            if (!Ready(response) || !Machine(request, response)) return;
            JsonElement body = Body(request);
            if (!TryUuid(body, "asset_id", out UUID asset) ||
                !TryUuid(body, "owner_id", out UUID owner))
            {
                Error(response, HttpStatusCode.BadRequest, "invalid_listing_binding");
                return;
            }
            if (!m_Catalog.PublishListing(listing, asset, owner))
            {
                Error(response, HttpStatusCode.NotFound, "media_owner_asset_mismatch");
                return;
            }
            Reply(response, HttpStatusCode.OK, new {
                listing_id = listing.ToString(), asset_id = asset.ToString(),
                image_url = "/api/v1/media/marketplace/" + listing + "/image"
            });
        }

        private void UnpublishListing(IOSHttpRequest request, IOSHttpResponse response, UUID listing)
        {
            if (!Ready(response) || !Machine(request, response)) return;
            if (!m_Catalog.UnpublishListing(listing))
            {
                Error(response, HttpStatusCode.NotFound, "media_not_found");
                return;
            }
            Reply(response, HttpStatusCode.OK, new { unpublished = true });
        }

        private void UpdateProfileImage(IOSHttpRequest request, IOSHttpResponse response,
            UUID avatar, bool firstLife)
        {
            if (!Ready(response)) return;
            if (!Authorize(request, response, NexScopes.ProfileWrite, out _, out UserAccount user))
                return;
            if (user == null || user.PrincipalID != avatar || m_Profiles == null)
            {
                Error(response, HttpStatusCode.Forbidden, "profile_owner_required");
                return;
            }
            JsonElement body = Body(request);
            if (!TryUuid(body, "asset_id", out UUID assetId) ||
                m_Catalog.FindUpload(assetId) is not NexMediaCatalogStore.MediaRecord upload ||
                upload.Owner != avatar)
            {
                Error(response, HttpStatusCode.NotFound, "media_owner_asset_mismatch");
                return;
            }
            UserProfileProperties profile = new UserProfileProperties { UserId = avatar };
            string error = "";
            if (!m_Profiles.GetAvatarProperties(ref profile, ref error))
            {
                Error(response, HttpStatusCode.ServiceUnavailable, "profiles_service_unavailable");
                return;
            }
            if (firstLife) profile.FirstLifeImageId = assetId;
            else profile.ImageId = assetId;
            if (!m_Profiles.UpdateAvatarProperties(ref profile, ref error))
            {
                Error(response, HttpStatusCode.ServiceUnavailable, "profile_image_update_failed");
                return;
            }
            Reply(response, HttpStatusCode.OK, new { asset_id = assetId.ToString(),
                image_url = "/api/v1/media/profiles/" + avatar +
                    (firstLife ? "/first-life" : "/image") });
        }

        private void UpdateGroupImage(IOSHttpRequest request, IOSHttpResponse response, UUID groupId)
        {
            if (!Ready(response)) return;
            if (!Authorize(request, response, NexScopes.GroupsManage,
                out _, out UserAccount user)) return;
            if (m_Groups == null || user == null || !user.LocalToGrid)
            {
                Error(response, HttpStatusCode.Forbidden, "group_resident_required");
                return;
            }
            JsonElement body = Body(request);
            if (!TryUuid(body, "asset_id", out UUID assetId) ||
                m_Catalog.FindUpload(assetId) is not NexMediaCatalogStore.MediaRecord upload ||
                upload.Owner != user.PrincipalID)
            {
                Error(response, HttpStatusCode.NotFound, "media_owner_asset_mismatch");
                return;
            }
            ExtendedGroupRecord record = m_Groups.GetGroupRecord(user.PrincipalID.ToString(), groupId);
            ExtendedGroupMembershipData membership = m_Groups.GetAgentGroupMembership(
                user.PrincipalID.ToString(), user.PrincipalID.ToString(), groupId);
            if (record == null || membership == null ||
                (((GroupPowers)membership.GroupPowers & GroupPowers.ChangeActions) == 0 &&
                 record.FounderID != user.PrincipalID))
            {
                Error(response, HttpStatusCode.Forbidden, "group_change_identity_required");
                return;
            }
            m_Groups.UpdateGroup(user.PrincipalID.ToString(), groupId, record.Charter,
                record.ShowInList, assetId, record.MembershipFee, record.OpenEnrollment,
                record.AllowPublish, record.MaturePublish);
            ExtendedGroupRecord changed = m_Groups.GetGroupRecord(user.PrincipalID.ToString(), groupId);
            if (changed == null || changed.GroupPicture != assetId)
            {
                Error(response, HttpStatusCode.ServiceUnavailable, "group_image_update_failed");
                return;
            }
            Reply(response, HttpStatusCode.OK, new { asset_id = assetId.ToString(),
                image_url = "/api/v1/media/groups/" + groupId + "/image" });
        }

        private void Render(IOSHttpRequest request, IOSHttpResponse response, UUID asset)
        {
            if (!NexMediaApi.TryParseSize(request.QueryString?["size"], out int size))
            {
                Error(response, HttpStatusCode.BadRequest, "invalid_image_size");
                return;
            }
            m_Render(asset, response, size);
        }

        private static void Error(IOSHttpResponse response, HttpStatusCode status, string code) =>
            Reply(response, status, new { error = code });

        private static void Reply(IOSHttpResponse response, HttpStatusCode status, object body)
        {
            response.StatusCode = (int)status;
            response.ContentType = "application/json; charset=utf-8";
            response.KeepAlive = false;
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(body);
        }
    }
}
