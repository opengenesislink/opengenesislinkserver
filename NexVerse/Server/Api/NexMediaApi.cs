// SPDX-License-Identifier: MPL-2.0
// Bound, read-only web images. Never fetch an arbitrary asset UUID from a URL.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text.Json;
using log4net;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Groups;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    /// <summary>
    /// Universal, entity-bound media reader. Only the ID of a group/avatar is
    /// accepted: the authoritative group/profile store supplies the asset ID.
    /// There is intentionally no publicly addressable /media/assets/{uuid}.
    /// </summary>
    internal sealed class NexMediaApi
    {
        private const int MaxAssetBytes = 8 * 1024 * 1024;
        private const int MaxDimension = 2048;
        private static readonly ILog s_Log = LogManager.GetLogger(typeof(NexMediaApi));

        private readonly IUserAccountService m_Accounts;
        private readonly IProfilesData m_Profiles;
        private readonly GroupsService m_Groups;
        private readonly Func<IAssetService> m_ResolveAssets;
        private readonly NexApiAuthenticator m_Authenticator;

        internal NexMediaApi(IUserAccountService accounts, IProfilesData profiles,
            GroupsService groups, Func<IAssetService> resolveAssets,
            NexApiAuthenticator authenticator)
        {
            m_Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Profiles = profiles;
            m_Groups = groups;
            m_ResolveAssets = resolveAssets ?? throw new ArgumentNullException(nameof(resolveAssets));
            m_Authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.AddHeader("Cache-Control", "private, no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (request == null)
            {
                Error(response, HttpStatusCode.BadRequest, "invalid_request");
                return;
            }
            if (!string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                response.AddHeader("Allow", "GET");
                Error(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
                return;
            }

            // Exactly /api/v1/media/{groups|profiles}/{entity-uuid}/{image|first-life}
            string[] parts = (request.UriPath ?? "").Trim('/').Split('/');
            if (!TryParseMediaPath(parts, out bool group, out UUID entity,
                    out bool firstLife))
            {
                Error(response, HttpStatusCode.NotFound, "media_not_found");
                return;
            }
            if (!TryParseSize(request.QueryString?["size"], out int size))
            {
                Error(response, HttpStatusCode.BadRequest, "invalid_image_size");
                return;
            }

            if (group)
                GetGroupImage(entity, request, response, size);
            else
                GetProfileImage(entity, firstLife, request, response, size);
        }

        internal static bool TryParseMediaPath(string[] parts,
            out bool group, out UUID entity, out bool firstLife)
        {
            group = false;
            entity = UUID.Zero;
            firstLife = false;
            if (parts == null || parts.Length != 6 ||
                parts[0] != "api" || parts[1] != "v1" ||
                parts[2] != "media" ||
                !UUID.TryParse(parts[4], out entity) || entity.IsZero())
                return false;

            group = parts[3] == "groups";
            if (group)
                return parts[5] == "image";

            if (parts[3] != "profiles")
                return false;
            firstLife = parts[5] == "first-life";
            return firstLife || parts[5] == "image";
        }

        internal static bool TryParseSize(string raw, out int size)
        {
            size = 0;
            if (string.IsNullOrEmpty(raw))
                return true;
            if (!int.TryParse(raw, out size))
                return false;
            return size == 64 || size == 128 || size == 256 || size == 512;
        }

        private void GetGroupImage(UUID groupId, IOSHttpRequest request,
            IOSHttpResponse response, int size)
        {
            if (!m_Authenticator.TryAuthenticate(request, NexScopes.GroupsRead,
                    out NexPrincipal principal, out UserAccount _,
                    out int status, out string error))
            {
                Error(response, (HttpStatusCode)status, error);
                return;
            }
            if (m_Groups == null)
            {
                Error(response, HttpStatusCode.ServiceUnavailable, "groups_service_unavailable");
                return;
            }

            // The GroupsService is the source of truth. No caller-supplied
            // insignia/texture ID or spoofed requester_id is accepted.
            try
            {
                string requester = principal.HasScope(NexScopes.AdminAll)
                    ? UUID.Zero.ToString() : principal.Subject;
                ExtendedGroupRecord record = m_Groups.GetGroupRecord(requester, groupId);
                if (record == null || record.GroupPicture.IsZero())
                {
                    Error(response, HttpStatusCode.NotFound, "media_not_found");
                    return;
                }
                Serve(record.GroupPicture, response, size);
            }
            catch (Exception e)
            {
                s_Log.Warn("[OGL-MEDIA]: group image read failed (" + e.GetType().Name + ")");
                Error(response, HttpStatusCode.ServiceUnavailable, "media_service_unavailable");
            }
        }

        private void GetProfileImage(UUID avatarId, bool firstLife,
            IOSHttpRequest request, IOSHttpResponse response, int size)
        {
            if (m_Profiles == null)
            {
                Error(response, HttpStatusCode.ServiceUnavailable, "profiles_service_unavailable");
                return;
            }
            UserAccount account = m_Accounts.GetUserAccount(UUID.Zero, avatarId);
            if (account == null)
            {
                Error(response, HttpStatusCode.NotFound, "media_not_found");
                return;
            }

            UserProfileProperties profile = new UserProfileProperties { UserId = avatarId };
            string details = "";
            try
            {
                if (!m_Profiles.GetAvatarProperties(ref profile, ref details))
                {
                    Error(response, HttpStatusCode.ServiceUnavailable, "profiles_service_unavailable");
                    return;
                }
            }
            catch (Exception e)
            {
                s_Log.Warn("[OGL-MEDIA]: profile lookup failed (" + e.GetType().Name + ")");
                Error(response, HttpStatusCode.ServiceUnavailable, "profiles_service_unavailable");
                return;
            }

            // First-Life is potentially personal information: never publish it
            // anonymously, even if the regular avatar profile is public.
            if (firstLife || !profile.PublishProfile)
            {
                if (!m_Authenticator.TryAuthenticate(request, NexScopes.ProfileRead,
                        out NexPrincipal principal, out UserAccount _,
                        out int status, out string authError))
                {
                    Error(response, (HttpStatusCode)status, authError);
                    return;
                }
                if ((!UUID.TryParse(principal.Subject, out UUID subject) ||
                     subject != avatarId) && !principal.HasScope(NexScopes.AdminAll))
                {
                    Error(response, HttpStatusCode.NotFound, "media_not_found");
                    return;
                }
            }

            UUID assetId = firstLife ? profile.FirstLifeImageId : profile.ImageId;
            if (assetId.IsZero())
            {
                Error(response, HttpStatusCode.NotFound, "media_not_found");
                return;
            }
            Serve(assetId, response, size);
        }

        private void Serve(UUID assetId, IOSHttpResponse response, int size)
        {
            try
            {
                IAssetService assets = m_ResolveAssets();
                if (assets == null)
                {
                    Error(response, HttpStatusCode.ServiceUnavailable, "media_service_unavailable");
                    return;
                }
                AssetBase asset = assets.Get(assetId.ToString());
                if (asset?.Data == null || asset.Data.Length == 0 ||
                    asset.Data.Length > MaxAssetBytes ||
                    !IsImageType(asset.Type))
                {
                    Error(response, HttpStatusCode.NotFound, "media_not_found");
                    return;
                }

                byte[] bytes = asset.Data;
                if (IsPng(bytes) || IsJpeg(bytes))
                {
                    if (size == 0)
                    {
                        Binary(response, bytes, IsPng(bytes) ? "image/png" : "image/jpeg");
                        return;
                    }
                    using MemoryStream input = new MemoryStream(bytes, false);
                    using Image image = Image.FromStream(input, false, true);
                    WritePng(response, image, size);
                    return;
                }

                // OpenSim texture assets normally contain JPEG2000 data.
                if (!OpenJPEG.DecodeToImage(bytes, out ManagedImage _, out Image decoded) ||
                    decoded == null)
                {
                    Error(response, HttpStatusCode.UnsupportedMediaType, "media_decode_failed");
                    return;
                }
                using (decoded)
                    WritePng(response, decoded, size);
            }
            catch (OutOfMemoryException)
            {
                // System.Drawing may signal invalid image bytes via OOM.
                Error(response, HttpStatusCode.UnsupportedMediaType, "media_decode_failed");
            }
            catch (ArgumentException)
            {
                Error(response, HttpStatusCode.UnsupportedMediaType, "media_decode_failed");
            }
            catch (Exception e)
            {
                s_Log.Warn("[OGL-MEDIA]: image delivery failed (" + e.GetType().Name + ")");
                Error(response, HttpStatusCode.ServiceUnavailable, "media_service_unavailable");
            }
        }

        private static bool IsImageType(sbyte type) =>
            type == (sbyte)AssetType.Texture ||
            type == (sbyte)AssetType.ImageJPEG ||
            type == (sbyte)AssetType.TextureTGA ||
            type == (sbyte)AssetType.ImageTGA;

        private static bool IsPng(byte[] x) => x.Length >= 8 &&
            x[0] == 0x89 && x[1] == 0x50 && x[2] == 0x4e &&
            x[3] == 0x47 && x[4] == 0x0d && x[5] == 0x0a &&
            x[6] == 0x1a && x[7] == 0x0a;
        private static bool IsJpeg(byte[] x) => x.Length >= 3 &&
            x[0] == 0xff && x[1] == 0xd8 && x[2] == 0xff;

        private static void WritePng(IOSHttpResponse response, Image source, int size)
        {
            if (source.Width <= 0 || source.Height <= 0 ||
                source.Width > MaxDimension || source.Height > MaxDimension)
            {
                Error(response, HttpStatusCode.RequestEntityTooLarge, "media_dimensions_exceeded");
                return;
            }
            if (size == 0 || Math.Max(source.Width, source.Height) <= size)
            {
                using MemoryStream output = new MemoryStream();
                source.Save(output, ImageFormat.Png);
                if (output.Length > MaxAssetBytes)
                {
                    Error(response, HttpStatusCode.RequestEntityTooLarge, "media_output_exceeded");
                    return;
                }
                Binary(response, output.ToArray(), "image/png");
                return;
            }
            double factor = (double)size / Math.Max(source.Width, source.Height);
            int w = Math.Max(1, (int)Math.Round(source.Width * factor));
            int h = Math.Max(1, (int)Math.Round(source.Height * factor));
            using Bitmap thumbnail = new Bitmap(w, h);
            using (Graphics graphics = Graphics.FromImage(thumbnail))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, 0, 0, w, h);
            }
            using MemoryStream buffer = new MemoryStream();
            thumbnail.Save(buffer, ImageFormat.Png);
            if (buffer.Length > MaxAssetBytes)
            {
                Error(response, HttpStatusCode.RequestEntityTooLarge, "media_output_exceeded");
                return;
            }
            Binary(response, buffer.ToArray(), "image/png");
        }

        private static void Binary(IOSHttpResponse response, byte[] bytes, string mime)
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = mime;
            response.KeepAlive = false;
            response.RawBuffer = bytes;
        }

        private static void Error(IOSHttpResponse response, HttpStatusCode status, string code)
        {
            response.StatusCode = (int)status;
            response.ContentType = "application/json; charset=utf-8";
            response.KeepAlive = false;
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(new { error = code });
        }
    }
}
