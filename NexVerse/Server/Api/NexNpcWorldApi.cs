// SPDX-License-Identifier: MPL-2.0
// R617: Source-verified, owner-scoped live NPC directory. The trusted simulator
// must publish complete signed region snapshots; no NPCs are inferred from IMs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class NexNpcWorldApi
    {
        private readonly NexApiAuthenticator m_Authentication;
        private readonly NexNpcSnapshotIndex m_Index;
        private readonly NexNpcChatStore m_Chat;

        internal NexNpcWorldApi(NexApiAuthenticator authentication,
            NexNpcSnapshotIndex index, NexNpcChatStore chat = null)
        {
            m_Authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            m_Index = index; // null means no verified simulator feed configured
            m_Chat = chat;
        }

        internal void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.AddHeader("Cache-Control", "private, no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            string path = (request.UriPath ?? "").TrimEnd('/');
            bool mine = path == "/api/v1/npcs/mine";
            bool chatSettings = path == "/api/v1/npcs/chat/settings";
            bool chatDelete = path == "/api/v1/npcs/chat/history";
            string prefix = "/api/v1/npcs/";
            bool history = path.StartsWith(prefix, StringComparison.Ordinal) &&
                path.EndsWith("/messages", StringComparison.Ordinal);
            if (!mine && !history && !chatSettings && !chatDelete)
            {
                Reply(response,HttpStatusCode.NotFound,new { error="not_found" });
                return;
            }
            string method = request.HttpMethod?.ToUpperInvariant() ?? "";
            bool validMethod = (method=="GET" && (mine || history || chatSettings)) ||
                (method=="PUT" && chatSettings) ||
                (method=="DELETE" && chatDelete);
            if (!validMethod)
            {
                response.AddHeader("Allow", chatSettings ? "GET, PUT" : chatDelete ? "DELETE" : "GET");
                Reply(response,HttpStatusCode.MethodNotAllowed,new { error="method_not_allowed" });
                return;
            }
            if (!m_Authentication.TryAuthenticate(request,
                method=="GET" ? NexScopes.RelationshipsRead : NexScopes.RelationshipsWrite,
                out NexPrincipal principal,
                out UserAccount account, out int status, out string reason))
            {
                response.AddHeader("WWW-Authenticate","Bearer");
                Reply(response,(HttpStatusCode)status,new { error=reason });
                return;
            }
            if (account == null || !account.Active || !account.LocalToGrid ||
                !Guid.TryParse(principal.Subject,out Guid owner) ||
                owner == Guid.Empty || owner != account.PrincipalID.Guid)
            {
                Reply(response,HttpStatusCode.Forbidden,new { error="resident_required" });
                return;
            }
            // Consent and deletion are owner-scoped operations; they must
            // remain available even if simulator snapshots are offline.
            if (chatSettings || chatDelete)
            {
                HandleChatSettings(request,response,owner,method,chatDelete);
                return;
            }
            if (m_Index == null || !m_Index.TryOwned(owner,
                out List<NexNpcOwnerEntry> owned, out long revision))
            {
                Reply(response,HttpStatusCode.ServiceUnavailable,
                    new { error="npc_registry_unavailable",
                          message="No complete fresh verified simulator snapshot is available." });
                return;
            }
            if (mine)
            {
                // An opaque generation-bound cursor avoids silently skipped
                // NPCs if a region changes while the owner pages through it.
                string cursor = request.QueryString?["cursor"] ?? "";
                int offset = 0;
                if (cursor.Length > 0)
                {
                    string[] pieces = cursor.Split('-');
                    if (pieces.Length != 2 ||
                        !long.TryParse(pieces[0],NumberStyles.None,CultureInfo.InvariantCulture,out long at) ||
                        !int.TryParse(pieces[1],NumberStyles.None,CultureInfo.InvariantCulture,out offset) ||
                        offset < 0 || offset > 10000)
                    {
                        Reply(response,HttpStatusCode.BadRequest,new { error="invalid_cursor" });
                        return;
                    }
                    if (at != revision)
                    {
                        Reply(response,HttpStatusCode.Conflict,new { error="npc_snapshot_changed" });
                        return;
                    }
                }
                var items = owned.Skip(offset).Take(500).Select(n => new
                {
                    npc_id=n.npc_id, owner_principal_id=n.owner_principal_id,
                    name=n.name, region_name=n.region_name
                }).ToArray();
                int next = offset + items.Length;
                Reply(response,HttpStatusCode.OK,new
                {
                    npcs=items, next_cursor=next < owned.Count
                        ? revision.ToString(CultureInfo.InvariantCulture)+"-"+next.ToString(CultureInfo.InvariantCulture)
                        : ""
                });
                return;
            }
            string uuid = path.Substring(prefix.Length,path.Length - prefix.Length - "/messages".Length);
            if (!Guid.TryParse(uuid,out Guid npcID) || npcID == Guid.Empty)
            {
                Reply(response,HttpStatusCode.BadRequest,new { error="invalid_npc_id" });
                return;
            }
            if (!owned.Any(n => n.npc_id == npcID))
            {
                Reply(response,HttpStatusCode.NotFound,new { error="npc_not_found" });
                return;
            }
            if (m_Chat == null)
            {
                Reply(response,HttpStatusCode.ServiceUnavailable,new { error="npc_chat_archive_unavailable" });
                return;
            }
            try
            {
                if (!m_Chat.IsEnabled(owner))
                {
                    Reply(response,HttpStatusCode.Forbidden,new { error="npc_chat_opt_in_required" });
                    return;
                }
                string rawCursor = request.QueryString?["cursor"] ?? "";
                if (rawCursor.Length>0 && (!long.TryParse(rawCursor,NumberStyles.None,
                    CultureInfo.InvariantCulture,out _) || rawCursor.Length>20))
                {
                    Reply(response,HttpStatusCode.BadRequest,new { error="invalid_cursor" });
                    return;
                }
                long cursor=rawCursor.Length==0 ? 0 : long.Parse(rawCursor,CultureInfo.InvariantCulture);
                var page=m_Chat.Read(owner,npcID,cursor,200);
                Reply(response,HttpStatusCode.OK,new
                {
                    npc_id=npcID,
                    owner_principal_id=owner,
                    messages=page.Messages,
                    next_cursor=page.NextCursor,
                    // The simulator -> World archive is best-effort; no fake
                    // assertion of a guaranteed lossless historical record.
                    capture_mode="best_effort_owner_npc_direct_im",
                    complete=false,
                    retention_days=m_Chat.RetentionDays
                });
            }
            catch(Exception)
            { Reply(response,HttpStatusCode.ServiceUnavailable,new { error="npc_chat_archive_unavailable" }); }
        }

        private void HandleChatSettings(IOSHttpRequest request,IOSHttpResponse response,
            Guid owner,string method,bool deleting)
        {
            if(m_Chat==null)
            {
                if(method=="GET")
                    Reply(response,HttpStatusCode.OK,new { enabled=false,available=false,retention_days=(int?)null });
                else
                    Reply(response,HttpStatusCode.ServiceUnavailable,new { error="npc_chat_archive_unavailable" });
                return;
            }
            try
            {
                if(deleting)
                {
                    m_Chat.DeleteHistory(owner);
                    Reply(response,HttpStatusCode.OK,new { deleted=true });
                    return;
                }
                if(method=="GET")
                {
                    Reply(response,HttpStatusCode.OK,new
                    {
                        enabled=m_Chat.IsEnabled(owner), available=true,
                        retention_days=m_Chat.RetentionDays,
                        capture_mode="best_effort_owner_npc_direct_im",
                        complete=false
                    });
                    return;
                }
                // Explicit owner permission: a mere true without a consent
                // acknowledgement is never accepted. No third-party chat.
                using var buffer=new MemoryStream();
                byte[] bytes=new byte[256];
                int read;
                while((read=request.InputStream.Read(bytes,0,bytes.Length))>0)
                {
                    if(buffer.Length+read>1024)
                    { Reply(response,HttpStatusCode.RequestEntityTooLarge,new { error="body_too_large" });return; }
                    buffer.Write(bytes,0,read);
                }
                using var doc=JsonDocument.Parse(buffer.ToArray());
                var body=doc.RootElement;
                if(body.ValueKind!=JsonValueKind.Object ||
                    !body.TryGetProperty("enabled",out JsonElement enabled) ||
                    (enabled.ValueKind!=JsonValueKind.True && enabled.ValueKind!=JsonValueKind.False))
                {
                    Reply(response,HttpStatusCode.BadRequest,new { error="invalid_consent" });
                    return;
                }
                bool grant=enabled.GetBoolean();
                if(grant && (!body.TryGetProperty("acknowledge_private_owner_npc_im",out JsonElement ack) ||
                    ack.ValueKind!=JsonValueKind.True))
                {
                    Reply(response,HttpStatusCode.BadRequest,new { error="explicit_npc_chat_consent_required" });
                    return;
                }
                m_Chat.SetEnabled(owner,grant);
                Reply(response,HttpStatusCode.OK,new
                {
                    enabled=grant,history_deleted=!grant,
                    retention_days=m_Chat.RetentionDays
                });
            }
            catch(JsonException)
            { Reply(response,HttpStatusCode.BadRequest,new { error="invalid_json" }); }
            catch(Exception)
            { Reply(response,HttpStatusCode.ServiceUnavailable,new { error="npc_chat_archive_unavailable" }); }
        }

        private static void Reply(IOSHttpResponse response,
            HttpStatusCode code, object payload)
        {
            response.KeepAlive = false;
            response.StatusCode = (int)code;
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("Cache-Control","private, no-store");
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(payload);
        }
    }
}
