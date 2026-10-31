// SPDX-License-Identifier: MPL-2.0
// R619: only private owner<->NPC IM events, never local/public chat.
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.World.NPC
{
    internal sealed class NexNpcChatPublisher : IDisposable
    {
        private sealed class Entry
        {
            internal Guid EventId;
            internal UUID Npc;
            internal UUID Owner;
            internal string Sender;
            internal string Text;
        }
        private readonly Uri m_Endpoint;
        private readonly byte[] m_Key;
        private readonly HttpClient m_Http;
        private readonly ConcurrentQueue<Entry> m_Queue = new();
        private int m_Queued;
        private int m_Running;
        private volatile bool m_Disposed;
        internal NexNpcChatPublisher(Uri endpoint,string secret)
        {
            if (endpoint == null || endpoint.Scheme != Uri.UriSchemeHttps ||
                endpoint.AbsolutePath != "/internal/nexnpc/chat/v1" ||
                !string.IsNullOrEmpty(endpoint.Query))
                throw new ArgumentException("NPC IM ingress must be private HTTPS.");
            if (Encoding.UTF8.GetByteCount(secret??"") < 32)
                throw new ArgumentException("NPC IM signing key must be 32+ bytes.");
            m_Endpoint=endpoint;
            m_Key=Encoding.UTF8.GetBytes(secret);
            m_Http=new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect=false,
                UseProxy=false
            }) { Timeout=TimeSpan.FromSeconds(8) };
        }
        // Best-effort forward to the consent-controlled World archive; never
        // persist an IM on the simulator before World has verified opt-in.
        internal void Send(UUID npc,UUID owner,string sender,string text)
        {
            if(m_Disposed || npc.IsZero() || owner.IsZero() ||
               (sender!="npc" && sender!="owner") ||
               string.IsNullOrWhiteSpace(text) ||
               Encoding.UTF8.GetByteCount(text)>4096)
                return;
            if(Interlocked.Increment(ref m_Queued)>256)
            { Interlocked.Decrement(ref m_Queued);return; }
            m_Queue.Enqueue(new Entry
            {
                EventId=Guid.NewGuid(),Npc=npc,Owner=owner,Sender=sender,Text=text
            });
            Schedule();
        }
        private void Schedule()
        {
            if(Interlocked.CompareExchange(ref m_Running,1,0)==0)
                _=Task.Run(DrainAsync);
        }
        private async Task DrainAsync()
        {
            try
            {
                while(!m_Disposed && m_Queue.TryDequeue(out Entry entry))
                {
                    Interlocked.Decrement(ref m_Queued);
                    try { await SendOneAsync(entry).ConfigureAwait(false); }
                    catch(Exception) {} // No text/keys in logs; IM was not archived.
                }
            }
            finally
            {
                Interlocked.Exchange(ref m_Running,0);
                if(!m_Disposed && !m_Queue.IsEmpty)Schedule();
            }
        }
        private async Task SendOneAsync(Entry entry)
        {
            byte[] body=JsonSerializer.SerializeToUtf8Bytes(new
            {
                event_id=entry.EventId.ToString("D"),
                npc_id=entry.Npc.ToString(),
                owner_principal_id=entry.Owner.ToString(),
                sender=entry.Sender,
                text=entry.Text
            });
            // Retry a temporary simulator/ROBUST race or network outage
            // without ever storing private content on disk.
            for(int attempt=0;attempt<3 && !m_Disposed;attempt++)
            {
                string now=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                byte[] prefix=Encoding.ASCII.GetBytes(now+"\n");
                byte[] input=new byte[prefix.Length+body.Length];
                Buffer.BlockCopy(prefix,0,input,0,prefix.Length);
                Buffer.BlockCopy(body,0,input,prefix.Length,body.Length);
                using var hmac=new HMACSHA256(m_Key);
                string signature=Convert.ToBase64String(hmac.ComputeHash(input));
                try
                {
                    using var request=new HttpRequestMessage(HttpMethod.Post,m_Endpoint);
                    request.Content=new ByteArrayContent(body);
                    request.Content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    request.Headers.TryAddWithoutValidation("X-NexNPC-Chat-Time",now);
                    request.Headers.TryAddWithoutValidation("X-NexNPC-Chat-Signature",signature);
                    using HttpResponseMessage response=await m_Http.SendAsync(request).ConfigureAwait(false);
                    if(response.IsSuccessStatusCode || (int)response.StatusCode<500)
                        return; // 403 opt-in missing, 401 invalid secret: do not retry.
                }
                catch(HttpRequestException){}
                catch(TaskCanceledException){}
                if(attempt<2)await Task.Delay(TimeSpan.FromSeconds(attempt+1)).ConfigureAwait(false);
            }
        }
        public void Dispose()
        {
            m_Disposed=true;
            while(m_Queue.TryDequeue(out _)){}
            // The in-flight callback is allowed to finish; process exit
            // releases the bounded HttpClient resources.
        }
    }
}
