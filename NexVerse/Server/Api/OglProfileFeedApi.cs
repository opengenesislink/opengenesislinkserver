// SPDX-License-Identifier: MPL-2.0
// Server-owned, opt-in profile activity feed. Public viewer pages are read-only:
// browser profile tabs do not receive a resident OAuth token by default.
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Linq;
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

        internal OglProfileFeedApi(IUserAccountService accounts, IProfilesData profiles,
            NexApiAuthenticator auth, OglProfileFeedStore store)
        {
            m_Accounts=accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Profiles=profiles ?? throw new ArgumentNullException(nameof(profiles));
            m_Auth=auth ?? throw new ArgumentNullException(nameof(auth));
            m_Store=store ?? throw new ArgumentNullException(nameof(store));
        }

        private static bool Is(IOSHttpRequest request, string method)
            => string.Equals(request.HttpMethod, method, StringComparison.OrdinalIgnoreCase);

        public void HandleApi(IOSHttpRequest request, IOSHttpResponse response)
        {
            string[] p=(request?.UriPath ?? "").Trim('/').Split('/');
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

            if(p.Length==4 && Is(request,"GET"))
            {
                if(!AllowRead(request,response,ownerId,profile.PublishProfile))return;
                var posts=m_Store.ListPosts(ownerId);
                Json(response,HttpStatusCode.OK,new {
                    owner_id=ownerId.ToString(), name=owner.Name,
                    followers=m_Store.Followers(ownerId),
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
                        id=c.Id,author_id=c.AuthorId,text=c.Text,created_at=c.CreatedAt
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
            if(!profile.PublishProfile && actorId!=ownerId)
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
                    m_Store.Follow(ownerId,actorId,Is(request,"PUT"));
                    Json(response,HttpStatusCode.OK,new {following=Is(request,"PUT")});
                    return;
                }
            }
            if(p.Length>=5 && UUID.TryParse(p[4],out UUID postId) &&
                m_Store.IsPostOwned(ownerId,postId))
            {
                if(p.Length==5 && Is(request,"DELETE"))
                {
                    if(ownerId!=actorId)
                    {
                        Json(response,HttpStatusCode.Forbidden,new {error="feed_owner_required"});
                        return;
                    }
                    m_Store.DeletePost(ownerId,postId);
                    Json(response,HttpStatusCode.OK,new {deleted=true});
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
                    bool removed=m_Store.DeleteComment(ownerId,postId,commentId,actorId);
                    Json(response,removed ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                        new {deleted=removed});
                    return;
                }
                if(p.Length==6 && p[5].Equals("likes",StringComparison.OrdinalIgnoreCase) &&
                   (Is(request,"PUT") || Is(request,"DELETE")))
                {
                    m_Store.Like(postId,actorId,Is(request,"PUT"));
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
            if(!profile.PublishProfile)
            {
                WebMessage(response,HttpStatusCode.NotFound,"Dieser Feed ist privat.");
                return;
            }
            var html=new StringBuilder();
            html.Append("<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
                .Append("<title>Stadt NexVerse – Profilfeed</title>")
                .Append("<style>:root{color-scheme:dark}*{box-sizing:border-box}body{margin:0;padding:17px;background:#0b1420;color:#e4effa;font:14px/1.55 system-ui,sans-serif}")
                .Append("header{border-bottom:1px solid #304961;padding-bottom:16px;margin-bottom:15px}h1{font-size:20px;margin:0;color:#f0faff}")
                .Append(".sub{color:#93b5ce;font-size:12px}.post{background:#13273b;border:1px solid #304c64;border-radius:11px;margin:12px 0;padding:16px}")
                .Append(".content{white-space:pre-wrap;overflow-wrap:anywhere;margin:10px 0}.meta{font-size:11px;color:#90abc1}")
                .Append(".comment{margin:7px 0 0 15px;padding:8px 11px;border-left:2px solid #417e9e;color:#c5daec;white-space:pre-wrap;overflow-wrap:anywhere}")
                .Append("a{color:#7acfee}footer{font-size:12px;color:#8aa5b9;margin-top:19px}</style></head><body>");
            html.Append("<header><div class=\"sub\">STADT NEXVERSE · OPEN GENESIS LINK</div><h1>")
                .Append(WebUtility.HtmlEncode(account.Name)).Append("</h1><div class=\"sub\">Profilfeed</div></header>");
            var posts=m_Store.ListPosts(account.PrincipalID,20);
            if(posts.Count==0)html.Append("<p class=\"sub\">Hier wurden noch keine Beiträge veröffentlicht.</p>");
            foreach(var post in posts)
            {
                html.Append("<article class=\"post\"><strong>")
                    .Append(WebUtility.HtmlEncode(account.Name))
                    .Append("</strong><div class=\"meta\">")
                    .Append(DateTimeOffset.FromUnixTimeSeconds(post.CreatedAt).UtcDateTime.ToString("dd.MM.yyyy HH:mm"))
                    .Append(" UTC</div><div class=\"content\">")
                    .Append(WebUtility.HtmlEncode(post.Text))
                    .Append("</div><div class=\"meta\">Gefällt mir: ")
                    .Append(post.Likes).Append(" · Kommentare: ").Append(post.Comments).Append("</div>");
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
            html.Append("<footer>Beiträge, Kommentare und Reaktionen werden über das authentifizierte Stadtportal verwaltet.")
                .Append(" <a href=\"https://stadt-nexverse.de/\" target=\"_blank\" rel=\"noopener noreferrer\">Stadt NexVerse</a></footer></body></html>");
            response.StatusCode=(int)HttpStatusCode.OK;
            response.ContentType="text/html; charset=utf-8";
            response.AddHeader("Cache-Control","no-store");
            response.AddHeader("X-Content-Type-Options","nosniff");
            response.AddHeader("Referrer-Policy","no-referrer");
            response.AddHeader("Content-Security-Policy","default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'");
            response.RawBuffer=Encoding.UTF8.GetBytes(html.ToString());
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
