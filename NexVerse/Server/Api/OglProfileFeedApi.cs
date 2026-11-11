// SPDX-License-Identifier: MPL-2.0
// Server-owned, opt-in profile activity feed. No avatar credentials from
// simulator circuits: interactive writes require browser OAuth PKCE S256.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;
using NexVerse.Core.Audit;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class OglProfileFeedApi
    {
        private readonly IUserAccountService m_Accounts;
        private readonly IProfilesData m_Profiles;
        private readonly NexApiAuthenticator m_Auth;
        private readonly OglProfileFeedStore m_Store;
        private readonly INexAuditSink m_Audit;
        private readonly string m_OAuthClientId;
        private readonly string m_OAuthRedirectUri;

        internal OglProfileFeedApi(IUserAccountService accounts, IProfilesData profiles,
            NexApiAuthenticator auth, OglProfileFeedStore store, INexAuditSink audit,
            string oauthClientId = "", string oauthRedirectUri = "")
        {
            m_Accounts=accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Profiles=profiles ?? throw new ArgumentNullException(nameof(profiles));
            m_Auth=auth ?? throw new ArgumentNullException(nameof(auth));
            m_Store=store ?? throw new ArgumentNullException(nameof(store));
            m_Audit=audit ?? NullNexAuditSink.Instance;
            m_OAuthClientId=oauthClientId?.Trim() ?? "";
            m_OAuthRedirectUri=oauthRedirectUri?.Trim() ?? "";
        }

        private static bool Is(IOSHttpRequest request, string method)
            => string.Equals(request.HttpMethod, method, StringComparison.OrdinalIgnoreCase);

        public void HandleApi(IOSHttpRequest request, IOSHttpResponse response)
        {
            string[] p=(request?.UriPath ?? "").Trim('/').Split('/');
            // Profile Feed auto-publication preferences are owned by the
            // authenticated local avatar, never supplied by a query parameter.
            if (p.Length == 5 && p[0] == "api" && p[1] == "v1" &&
                p[2] == "feed" && p[3] == "me" && p[4] == "activities")
            {
                bool read = Is(request,"GET");
                if (!read && !Is(request,"PUT"))
                {
                    Json(response,HttpStatusCode.MethodNotAllowed,
                        new {error="get_or_put_required"});
                    return;
                }
                if (!m_Auth.TryAuthenticate(request,
                    read ? NexScopes.ProfileRead : NexScopes.ProfileWrite,
                    out NexPrincipal activityPrincipal,out UserAccount account,
                    out int activityStatus,out string activityAuthError))
                {
                    Json(response,(HttpStatusCode)activityStatus,new {error=activityAuthError});
                    return;
                }
                if (account == null || !account.LocalToGrid ||
                    !UUID.TryParse(activityPrincipal.Subject,out UUID resident) ||
                    resident != account.PrincipalID)
                {
                    Json(response,HttpStatusCode.Forbidden,
                        new {error="local_resident_required"});
                    return;
                }
                if (!read)
                {
                    if (!TryActivitySettings(request,out bool autoEvents,
                        out bool autoClassifieds))
                    {
                        Json(response,HttpStatusCode.BadRequest,
                            new {error="invalid_auto_activity_settings"});
                        return;
                    }
                    if (!m_Store.SetActivitySettings(resident,autoEvents,autoClassifieds))
                    {
                        Json(response,HttpStatusCode.NotFound,
                            new {error="resident_not_found"});
                        return;
                    }
                }
                var settings=m_Store.GetActivitySettings(resident);
                Json(response,HttpStatusCode.OK,new {
                    auto_events=settings.Events,
                    auto_classifieds=settings.Classifieds
                });
                return;
            }

            // Session bootstrap and personalized following timeline do not
            // have a profile-owner UUID. Both require local OAuth residents.
            if(p.Length==4 && (p[3]=="me" || p[3]=="timeline"))
            {
                string required=p[3]=="me" ? NexScopes.ProfileRead
                    : NexScopes.RelationshipsRead;
                if(!Is(request,"GET"))
                {
                    Json(response,HttpStatusCode.MethodNotAllowed,new {error="get_required"});
                    return;
                }
                if(!m_Auth.TryAuthenticate(request,required,out NexPrincipal viewer,
                    out UserAccount viewerAccount,out int code,out string failure))
                {
                    Json(response,(HttpStatusCode)code,new {error=failure});
                    return;
                }
                if(viewerAccount==null || !viewerAccount.LocalToGrid ||
                   !UUID.TryParse(viewer.Subject,out UUID me) ||
                   viewerAccount.PrincipalID!=me)
                {
                    Json(response,HttpStatusCode.Forbidden,new {error="local_resident_required"});
                    return;
                }
                if(p[3]=="me")
                {
                    Json(response,HttpStatusCode.OK,
                        new {avatar_id=me.ToString(),name=viewerAccount.Name});
                    return;
                }
                m_Store.PruneExpired();
                // Defense in depth: a follow does not override the target's
                // PublishProfile setting. No private posts in this timeline.
                var items=m_Store.ListFollowingPosts(me,80);
                var visible=new List<object>();
                var visibleIds=new HashSet<string>(StringComparer.Ordinal);
                foreach(var item in items)
                {
                    UserAccount followed=m_Accounts.GetUserAccount(UUID.Zero,item.Owner);
                    if(followed==null || !followed.LocalToGrid)continue;
                    UserProfileProperties properties=new UserProfileProperties {
                        UserId=item.Owner };
                    string profileError="";
                    if(!m_Profiles.GetAvatarProperties(ref properties,ref profileError) ||
                       !properties.PublishProfile)continue;
                    visibleIds.Add(item.Entry.Id);
                    visible.Add(new {
                        owner_id=item.Owner.ToString(),owner_name=followed.Name,
                        id=item.Entry.Id,text=item.Entry.Text,
                        created_at=item.Entry.CreatedAt,likes=item.Entry.Likes,
                        comments=item.Entry.Comments
                    });
                    if(visible.Count>=30)break;
                }
                Json(response,HttpStatusCode.OK,new {
                    posts=visible,
                    liked_post_ids=m_Store.ListLikedFollowingPostIds(me)
                        .Where(visibleIds.Contains).ToArray()
                });
                return;
            }
            if(p.Length < 4 || p.Length > 7 ||
               !p[0].Equals("api",StringComparison.OrdinalIgnoreCase) ||
               !p[1].Equals("v1",StringComparison.OrdinalIgnoreCase) ||
               !p[2].Equals("feed",StringComparison.OrdinalIgnoreCase) ||
               !UUID.TryParse(p[3],out UUID ownerId))
            {
                Json(response,HttpStatusCode.NotFound,new {error="feed_not_found"});
                return;
            }
            UserAccount owner=m_Accounts.GetUserAccount(UUID.Zero,ownerId);
            if(owner==null || !owner.LocalToGrid)
            {
                Json(response,HttpStatusCode.NotFound,new {error="feed_not_found"});
                return;
            }
            // The existing viewer profile setting is the source of truth.
            UserProfileProperties profile=new UserProfileProperties { UserId=ownerId };
            string error="";
            if(!m_Profiles.GetAvatarProperties(ref profile,ref error))
            {
                Json(response,HttpStatusCode.ServiceUnavailable,new {error="profile_unavailable"});
                return;
            }

            m_Store.PruneExpired();

            if(p.Length==4 && Is(request,"GET"))
            {
                if(!AllowRead(request,response,ownerId,profile.PublishProfile))return;
                var posts=m_Store.ListPosts(ownerId);
                UUID viewerId=UUID.Zero;
                // Optional personalization: anonymous profiles remain public.
                // An invalid bearer never gains write/read permissions.
                if(!string.IsNullOrWhiteSpace(request?.Headers?["Authorization"]) &&
                   m_Auth.TryAuthenticate(request,NexScopes.RelationshipsRead,
                       out NexPrincipal viewer,out UserAccount viewerAccount,
                       out _,out _) &&
                   viewerAccount!=null && viewerAccount.LocalToGrid &&
                   UUID.TryParse(viewer.Subject,out UUID authenticatedViewer) &&
                   viewerAccount.PrincipalID==authenticatedViewer)
                    viewerId=authenticatedViewer;
                Json(response,HttpStatusCode.OK,new {
                    owner_id=ownerId.ToString(), name=owner.Name,
                    followers=m_Store.Followers(ownerId),
                    following=!viewerId.IsZero() && m_Store.IsFollowing(ownerId,viewerId),
                    liked_post_ids=viewerId.IsZero() ? Array.Empty<string>()
                        : m_Store.ListLikedPostIds(ownerId,viewerId).ToArray(),
                    posts=posts.Select(post=>new {
                        id=post.Id,author_id=post.AuthorId,text=post.Text,
                        created_at=post.CreatedAt,likes=post.Likes,comments=post.Comments
                    }).ToArray()
                });
                return;
            }

            if(p.Length==6 && UUID.TryParse(p[4],out UUID publicPost) &&
               p[5].Equals("comments",StringComparison.OrdinalIgnoreCase) &&
               Is(request,"GET"))
            {
                if(!AllowRead(request,response,ownerId,profile.PublishProfile))return;
                if(!m_Store.IsPostOwned(ownerId,publicPost))
                {
                    Json(response,HttpStatusCode.NotFound,new {error="post_not_found"});
                    return;
                }
                Json(response,HttpStatusCode.OK,new {
                    post_id=publicPost.ToString(),
                    comments=m_Store.ListComments(publicPost).Select(c=>new {
                        id=c.Id,author_id=c.AuthorId,text=c.Text,created_at=c.CreatedAt,
                        author_name=UUID.TryParse(c.AuthorId,out UUID cid)
                            ? m_Accounts.GetUserAccount(UUID.Zero,cid)?.Name ?? "Einwohner"
                            : "Einwohner"
                    }).ToArray()
                });
                return;
            }

            // Non-GET operations are never authorized through query parameters,
            // Viewer web session cookies or simulator machine keys.
            string permission=(p.Length==4 && Is(request,"POST")) ||
                (p.Length==5 && Is(request,"DELETE"))
                ? NexScopes.ProfileWrite
                : NexScopes.RelationshipsWrite;
            if(!m_Auth.TryAuthenticate(request,permission,out NexPrincipal principal,
                out UserAccount actorAccount,out int status,out string authError))
            {
                Json(response,(HttpStatusCode)status,new {error=authError});
                return;
            }
            if(!UUID.TryParse(principal.Subject,out UUID actorId) || actorAccount==null ||
               !actorAccount.LocalToGrid || actorAccount.PrincipalID!=actorId)
            {
                Json(response,HttpStatusCode.Forbidden,new {error="local_resident_required"});
                return;
            }

            bool isModerator = actorAccount.UserLevel >= 200 &&
                principal.HasScope(NexScopes.AdminAll);

            if(p.Length==4 && Is(request,"POST"))
            {
                if(actorId!=ownerId)
                {
                    Json(response,HttpStatusCode.Forbidden,new {error="feed_owner_required"});
                    return;
                }
                if(!TryText(request,"text",1500,out string text))
                {
                    Json(response,HttpStatusCode.BadRequest,new {error="invalid_post_text"});
                    return;
                }
                if(!m_Store.CreatePost(ownerId,text,out UUID createdPostId))
                {
                    Json(response,(HttpStatusCode)429,new {error="post_rate_limited"});
                    return;
                }
                Json(response,HttpStatusCode.Created,new {id=createdPostId.ToString()});
                return;
            }

            // No interaction with a private feed unless the authenticated
            // principal owns it. Anonymous viewers only see published profiles.
            if(!profile.PublishProfile && actorId!=ownerId && !isModerator)
            {
                Json(response,HttpStatusCode.NotFound,new {error="feed_not_found"});
                return;
            }
            if(p.Length==5 && p[4].Equals("following",StringComparison.OrdinalIgnoreCase))
            {
                if(actorId==ownerId)
                {
                    Json(response,HttpStatusCode.BadRequest,new {error="cannot_follow_self"});
                    return;
                }
                if(Is(request,"PUT") || Is(request,"DELETE"))
                {
                    if(!m_Store.Follow(ownerId,actorId,Is(request,"PUT")))
                    {
                        Json(response,HttpStatusCode.NotFound,new {error="resident_not_found"});
                        return;
                    }
                    Json(response,HttpStatusCode.OK,new {following=Is(request,"PUT")});
                    return;
                }
            }
            if(p.Length>=5 && UUID.TryParse(p[4],out UUID postId) &&
                m_Store.IsPostOwned(ownerId,postId))
            {
                if(p.Length==5 && Is(request,"DELETE"))
                {
                    if(ownerId!=actorId && !isModerator)
                    {
                        Json(response,HttpStatusCode.Forbidden,new {error="feed_owner_required"});
                        return;
                    }
                    bool removed=m_Store.DeletePost(ownerId,postId);
                    if(removed && isModerator && ownerId!=actorId)
                        AuditModeratorAction(actorId,ownerId,postId,"feed.post.moderate");
                    Json(response,removed ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                        new {deleted=removed});
                    return;
                }
                if(p.Length==6 && p[5].Equals("comments",StringComparison.OrdinalIgnoreCase))
                {
                    if(Is(request,"POST"))
                    {
                        if(!TryText(request,"text",800,out string text))
                        {
                            Json(response,HttpStatusCode.BadRequest,new {error="invalid_comment_text"});
                            return;
                        }
                        if(!m_Store.AddComment(postId,actorId,text,out UUID createdCommentId))
                        {
                            Json(response,(HttpStatusCode)429,new {error="comment_rate_limited"});
                            return;
                        }
                        Json(response,HttpStatusCode.Created,new {id=createdCommentId.ToString()});
                        return;
                    }
                }
                if(p.Length==7 && p[5].Equals("comments",StringComparison.OrdinalIgnoreCase) &&
                   UUID.TryParse(p[6],out UUID commentId) && Is(request,"DELETE"))
                {
                    bool removed=m_Store.DeleteComment(ownerId,postId,commentId,actorId,isModerator);
                    if(removed && isModerator && ownerId!=actorId)
                        AuditModeratorAction(actorId,ownerId,postId,"feed.comment.moderate");
                    Json(response,removed ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                        new {deleted=removed});
                    return;
                }
                if(p.Length==6 && p[5].Equals("likes",StringComparison.OrdinalIgnoreCase) &&
                   (Is(request,"PUT") || Is(request,"DELETE")))
                {
                    if(!m_Store.Like(postId,actorId,Is(request,"PUT")))
                    {
                        Json(response,HttpStatusCode.NotFound,new {error="resident_not_found"});
                        return;
                    }
                    Json(response,HttpStatusCode.OK,new {liked=Is(request,"PUT")});
                    return;
                }
            }
            Json(response,HttpStatusCode.NotFound,new {error="feed_operation_not_found"});
        }

        public void HandleWeb(IOSHttpRequest request, IOSHttpResponse response)
        {
            if(request==null || !Is(request,"GET"))
            {
                Json(response,HttpStatusCode.MethodNotAllowed,new {error="get_required"});
                return;
            }
            string nonce=Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            // OAuth uses one exact registered redirect URI: /feed, never
            // unregistered per-avatar query strings. This page processes the
            // one-time code only in the browser and immediately erases its URL.
            if(request.QueryString?["code"]!=null ||
               request.QueryString?["error"]!=null)
            {
                WriteFeedHtml(response,
                    OglProfileFeedWebApp.CallbackPage(nonce,m_OAuthClientId,
                        m_OAuthRedirectUri),nonce);
                return;
            }
            string name=(request.QueryString?["name"] ?? "").Trim();
            // Firestorm expands [AGENT_NAME] to First.Last or First.Resident.
            // Do not infer identity or permission from the URL alone.
            name=name.Replace(' ','.'); // convert whitespace-name alias to dotted form
            string[] names=name.Split('.',StringSplitOptions.RemoveEmptyEntries);
            if(name.Length>128 || names.Length!=2 || names.Any(x=>x.Length>64))
            {
                WebMessage(response,HttpStatusCode.BadRequest,"Ungültiger Avatarname.");
                return;
            }
            UserAccount account=m_Accounts.GetUserAccount(UUID.Zero,names[0],names[1]);
            if(account==null || !account.LocalToGrid)
            {
                WebMessage(response,HttpStatusCode.NotFound,"Avatar nicht gefunden.");
                return;
            }
            var profile=new UserProfileProperties { UserId=account.PrincipalID };
            string error="";
            if(!m_Profiles.GetAvatarProperties(ref profile,ref error))
            {
                WebMessage(response,HttpStatusCode.ServiceUnavailable,"Profil derzeit nicht verfügbar.");
                return;
            }
            if(!profile.PublishProfile && string.IsNullOrWhiteSpace(m_OAuthClientId))
            {
                WebMessage(response,HttpStatusCode.NotFound,"Dieser Feed ist privat.");
                return;
            }
            m_Store.PruneExpired();
            var html=new StringBuilder();
            html.Append("<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
                .Append("<meta name=\"theme-color\" content=\"#070e1e\">")
                .Append("<title>Stadt NexVerse · Profilfeed</title><style>")
                .Append(OglProfileFeedWebApp.Styles())
                .Append("</style></head><body><div class=\"ogl-shell\">");
            html.Append("<div class=\"ogl-brandbar\" aria-label=\"Stadt NexVerse\">")
                .Append("<span class=\"ogl-emblem\" aria-hidden=\"true\">NV</span>")
                .Append("<div><div class=\"ogl-brandname\">Stadt NexVerse</div>")
                .Append("<span class=\"ogl-brandtag\">Dein virtuelles Zuhause</span></div>")
                .Append("<span class=\"ogl-spacer\"></span>")
                .Append("<span class=\"ogl-network-chip\">NexVerse Community</span></div>");
            html.Append("<header class=\"ogl-hero\"><div class=\"ogl-cover\" aria-hidden=\"true\"></div>")
                .Append("<div class=\"ogl-profile\"><span class=\"ogl-portrait\" aria-hidden=\"true\">")
                .Append(WebUtility.HtmlEncode((names[0].Substring(0,1) +
                    names[1].Substring(0,1)).ToUpperInvariant()))
                .Append("</span><div class=\"ogl-profile-copy\">")
                .Append("<span class=\"ogl-kicker\">Einwohnerprofil</span><h1>")
                .Append(WebUtility.HtmlEncode(account.Name))
                .Append("</h1><div class=\"ogl-subtitle\">Momente, Geschichten und Neuigkeiten aus Stadt NexVerse</div>")
                .Append("</div></div></header>");
            html.Append(OglProfileFeedWebApp.Controls())
                .Append("<main id=\"ogl-feed-posts\" aria-label=\"Feed-Beitraege\">");
            var posts=profile.PublishProfile ? m_Store.ListPosts(account.PrincipalID,20)
                : new List<OglProfileFeedStore.Post>();
            if(posts.Count==0)html.Append("<p class=\"sub\">Hier wurden noch keine Beiträge veröffentlicht.</p>");
            foreach(var post in posts)
            {
                html.Append("<article class=\"post\"><div class=\"ogl-post-header\">")
                    .Append("<span class=\"ogl-post-avatar\" aria-hidden=\"true\">")
                    .Append(WebUtility.HtmlEncode((names[0].Substring(0,1) +
                        names[1].Substring(0,1)).ToUpperInvariant()))
                    .Append("</span><div class=\"ogl-post-who\"><div class=\"ogl-post-name\">")
                    .Append(WebUtility.HtmlEncode(account.Name))
                    .Append("</div><div class=\"meta\">")
                    .Append(DateTimeOffset.FromUnixTimeSeconds(post.CreatedAt).UtcDateTime.ToString("dd.MM.yyyy HH:mm"))
                    .Append(" UTC</div></div></div><div class=\"content\">")
                    .Append(WebUtility.HtmlEncode(post.Text))
                    .Append("</div><div class=\"ogl-reactions\">")
                    .Append(post.Likes).Append(" Gefaellt mir · ").Append(post.Comments)
                    .Append(" Kommentare</div>");
                foreach(var comment in m_Store.ListComments(new UUID(post.Id),10))
                {
                    UserAccount author=UUID.TryParse(comment.AuthorId,out UUID cid)
                        ? m_Accounts.GetUserAccount(UUID.Zero,cid) : null;
                    html.Append("<div class=\"comment\"><strong>")
                        .Append(WebUtility.HtmlEncode(author?.Name ?? "Einwohner"))
                        .Append("</strong>: ")
                        .Append(WebUtility.HtmlEncode(comment.Text)).Append("</div>");
                }
                html.Append("</article>");
            }
            html.Append("</main><footer class=\"ogl-footer\">")
                .Append("<span>© Stadt NexVerse · Deine Welt. Deine Community.</span>")
                .Append("<span class=\"ogl-powered\">Powered by <strong>OpenGenesisLINK</strong></span>")
                .Append("</footer></div>");
            html.Append(OglProfileFeedWebApp.Script(nonce,
                account.PrincipalID.ToString(),account.Name,profile.PublishProfile,
                m_OAuthClientId,m_OAuthRedirectUri));
            html.Append("</body></html>");
            WriteFeedHtml(response,html.ToString(),nonce);
        }

        private static void WriteFeedHtml(IOSHttpResponse response,string html,string nonce)
        {
            response.StatusCode=(int)HttpStatusCode.OK;
            response.ContentType="text/html; charset=utf-8";
            response.AddHeader("Cache-Control","no-store");
            response.AddHeader("X-Content-Type-Options","nosniff");
            response.AddHeader("Referrer-Policy","no-referrer");
            response.AddHeader("X-Frame-Options","DENY");
            response.AddHeader("Content-Security-Policy",
                "default-src 'none'; style-src 'unsafe-inline'; " +
                "script-src 'nonce-" + nonce + "'; connect-src 'self'; " +
                "base-uri 'none'; form-action 'none'; frame-ancestors 'none'");
            response.RawBuffer=Encoding.UTF8.GetBytes(html);
        }

        private void AuditModeratorAction(UUID actor, UUID owner, UUID post, string action)
        {
            try
            {
                m_Audit.Record(new NexAuditEvent(actor.ToString(), action,
                    "profile-feed:" + owner + "/" + post,
                    Guid.NewGuid().ToString("N"),
                    new Dictionary<string, string> { ["mode"] = "admin_moderation" }));
            }
            catch (Exception auditError)
            {
                // Do not disguise a committed moderation delete as a failed
                // operation; operators can reconcile from the structured log.
                Trace.TraceWarning("[OGL-FEED]: Moderation audit dispatch failed: " +
                    auditError.GetType().Name);
            }
        }

        private bool AllowRead(IOSHttpRequest request,IOSHttpResponse response,UUID owner,bool published)
        {
            if(published)return true;
            if(!m_Auth.TryAuthenticate(request,NexScopes.ProfileRead,
                out NexPrincipal principal,out UserAccount _,out int status,out string authError))
            {
                Json(response,HttpStatusCode.NotFound,new {error="feed_not_found"});
                return false;
            }
            if(!UUID.TryParse(principal.Subject,out UUID actor) ||
               (actor!=owner && !principal.HasScope(NexScopes.AdminAll)))
            {
                Json(response,HttpStatusCode.NotFound,new {error="feed_not_found"});
                return false;
            }
            return true;
        }

        private static bool TryActivitySettings(IOSHttpRequest request,
            out bool events,out bool classifieds)
        {
            events=false;
            classifieds=false;
            try
            {
                using var reader=new StreamReader(request.InputStream,Encoding.UTF8,
                    true,1024,leaveOpen:true);
                char[] buffer=new char[2049];
                int length=reader.ReadBlock(buffer,0,buffer.Length);
                if(length<=0 || length>=buffer.Length)return false;
                using JsonDocument json=JsonDocument.Parse(new string(buffer,0,length));
                JsonElement root=json.RootElement;
                if(root.ValueKind!=JsonValueKind.Object ||
                   !root.TryGetProperty("auto_events",out JsonElement first) ||
                   !root.TryGetProperty("auto_classifieds",out JsonElement second))
                    return false;
                if((first.ValueKind!=JsonValueKind.True &&
                    first.ValueKind!=JsonValueKind.False) ||
                   (second.ValueKind!=JsonValueKind.True &&
                    second.ValueKind!=JsonValueKind.False))
                    return false;
                events=first.GetBoolean();
                classifieds=second.GetBoolean();
                return true;
            }
            catch{return false;}
        }

        private static bool TryText(IOSHttpRequest request,string field,int max,out string value)
        {
            value="";
            try
            {
                using var reader=new StreamReader(request.InputStream,Encoding.UTF8,true,1024,leaveOpen:true);
                char[] buffer=new char[4097];
                int n=reader.ReadBlock(buffer,0,buffer.Length);
                if(n<=0 || n>=buffer.Length)return false;
                using JsonDocument json=JsonDocument.Parse(new string(buffer,0,n));
                if(json.RootElement.ValueKind!=JsonValueKind.Object ||
                   !json.RootElement.TryGetProperty(field,out JsonElement text) ||
                   text.ValueKind!=JsonValueKind.String)return false;
                value=(text.GetString() ?? "").Trim();
                return value.Length>0 && value.Length<=max;
            }
            catch{return false;}
        }

        private static void WebMessage(IOSHttpResponse r,HttpStatusCode status,string message)
        {
            r.StatusCode=(int)status;r.ContentType="text/html; charset=utf-8";
            r.AddHeader("Cache-Control","no-store");
            r.AddHeader("X-Content-Type-Options","nosniff");
            r.AddHeader("Content-Security-Policy","default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'");
            r.RawBuffer=Encoding.UTF8.GetBytes("<!doctype html><html lang=\"de\"><meta charset=\"utf-8\"><body style=\"background:#0b1420;color:#d8e9f8;font:14px system-ui;padding:20px\">"
                +WebUtility.HtmlEncode(message)+"</body></html>");
        }

        private static void Json(IOSHttpResponse r,HttpStatusCode status,object payload)
        {
            r.StatusCode=(int)status;r.ContentType="application/json; charset=utf-8";
            r.AddHeader("Cache-Control","no-store");
            r.AddHeader("X-Content-Type-Options","nosniff");
            r.RawBuffer=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        }
    }
}
