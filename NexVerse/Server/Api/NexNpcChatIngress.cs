// SPDX-License-Identifier: MPL-2.0
// R619: signed owner-to-NPC direct IM ingestion. Never uses public bearer tokens.
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenSim.Framework.Servers.HttpServer;

namespace NexVerse.Server.Api
{
    internal sealed class NexNpcChatIngress
    {
        private readonly byte[] m_Secret;
        private readonly NexNpcSnapshotIndex m_Index;
        private readonly NexNpcChatStore m_Store;
        internal NexNpcChatIngress(string secret, NexNpcSnapshotIndex index, NexNpcChatStore store)
        {
            if (Encoding.UTF8.GetByteCount(secret ?? "") < 32)
                throw new ArgumentException("NPC chat HMAC secret requires at least 32 bytes.");
            m_Secret = Encoding.UTF8.GetBytes(secret);
            m_Index = index ?? throw new ArgumentNullException(nameof(index));
            m_Store = store ?? throw new ArgumentNullException(nameof(store));
        }
        internal void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (request.HttpMethod != "POST" ||
                (request.UriPath ?? "").TrimEnd('/') != "/internal/nexnpc/chat/v1")
            {
                Reply(response,HttpStatusCode.NotFound,"not_found"); return;
            }
            byte[] body;
            try
            {
                using var stream = new MemoryStream();
                byte[] buffer = new byte[2048];
                int n;
                while ((n=request.InputStream.Read(buffer,0,buffer.Length))>0)
                {
                    if (stream.Length+n>8192)
                    { Reply(response,HttpStatusCode.RequestEntityTooLarge,"payload_too_large");return; }
                    stream.Write(buffer,0,n);
                }
                body=stream.ToArray();
            }
            catch { Reply(response,HttpStatusCode.BadRequest,"invalid_body");return; }
            string timestamp=request.Headers?["X-NexNPC-Chat-Time"]??"";
            string signature=request.Headers?["X-NexNPC-Chat-Signature"]??"";
            long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!long.TryParse(timestamp,out long stamp) ||
                stamp < now-120 || stamp > now+120 || !Verify(timestamp,body,signature))
            { Reply(response,HttpStatusCode.Unauthorized,"signature_invalid");return; }
            try
            {
                using JsonDocument doc=JsonDocument.Parse(body);
                JsonElement obj=doc.RootElement;
                if (obj.ValueKind!=JsonValueKind.Object ||
                    !Uuid(obj,"event_id",out Guid eventID) ||
                    !Uuid(obj,"npc_id",out Guid npcID) ||
                    !Uuid(obj,"owner_principal_id",out Guid owner) ||
                    !Str(obj,"sender",8,out string sender) ||
                    (sender!="npc" && sender!="owner") ||
                    !Str(obj,"text",4096,out string message) ||
                    Encoding.UTF8.GetByteCount(message)>4096)
                { Reply(response,HttpStatusCode.BadRequest,"invalid_npc_chat_event");return; }

                // Source authorization: only a currently verified *owner's*
                // NPC is allowed. A valid HMAC alone never grants ownership.
                if (!m_Index.TryOwned(owner,out var owned,out _) ||
                    !owned.Any(x=>x.npc_id==npcID))
                { Reply(response,HttpStatusCode.ServiceUnavailable,"npc_registry_unavailable");return; }

                // Explicit resident opt-in is mandatory. No history is kept
                // until owner enables it from a personally authenticated API.
                if (!m_Store.IsEnabled(owner))
                { Reply(response,HttpStatusCode.Forbidden,"npc_chat_opt_in_required");return; }

                if (!m_Store.Append(eventID,owner,npcID,sender,message))
                { Reply(response,HttpStatusCode.Conflict,"npc_chat_not_recorded");return; }
                Reply(response,HttpStatusCode.Accepted,"accepted");
            }
            catch(JsonException)
            { Reply(response,HttpStatusCode.BadRequest,"invalid_json"); }
            catch(Exception)
            { Reply(response,HttpStatusCode.ServiceUnavailable,"npc_chat_archive_unavailable"); }
        }
        private bool Verify(string stamp,byte[] body,string signature)
        {
            try
            {
                byte[] received=Convert.FromBase64String(signature);
                byte[] head=Encoding.ASCII.GetBytes(stamp+"\n");
                byte[] input=new byte[head.Length+body.Length];
                Buffer.BlockCopy(head,0,input,0,head.Length);
                Buffer.BlockCopy(body,0,input,head.Length,body.Length);
                using var hmac=new HMACSHA256(m_Secret);
                byte[] digest=hmac.ComputeHash(input);
                return received.Length==digest.Length &&
                    CryptographicOperations.FixedTimeEquals(received,digest);
            }
            catch(FormatException){return false;}
        }
        private static bool Uuid(JsonElement obj,string property,out Guid value)
        {
            value=Guid.Empty;
            return obj.TryGetProperty(property,out JsonElement v) &&
                v.ValueKind==JsonValueKind.String &&
                Guid.TryParse(v.GetString(),out value) && value!=Guid.Empty;
        }
        private static bool Str(JsonElement obj,string property,int max,out string value)
        {
            value="";
            if (!obj.TryGetProperty(property,out JsonElement v) ||
                v.ValueKind!=JsonValueKind.String)return false;
            value=(v.GetString()??"").Trim();
            return value.Length>0 && value.Length<=max;
        }
        private static void Reply(IOSHttpResponse response,HttpStatusCode code,string status)
        {
            response.KeepAlive=false;
            response.StatusCode=(int)code;
            response.ContentType="application/json; charset=utf-8";
            response.RawBuffer=JsonSerializer.SerializeToUtf8Bytes(new{error=status});
        }
    }
}
