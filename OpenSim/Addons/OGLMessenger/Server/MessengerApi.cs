using System.Net;
using System.Text;
using System.Text.Json;
using OpenSim.Addons.OGLMessenger.Contracts;

namespace OpenSim.Addons.OGLMessenger.Server;

public static class MessengerApi
{
    public static void MapMessengerEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/capabilities", () => Results.Ok(new
        {
            service = "NexVerse Messenger",
            version = "g1-world-api-mvp",
            authentication = "world-api-oauth2-pkce",
            localAvatarLogin = true,
            persistentSession = true,
            localFriendIm = true,
            offlineHistory = "world-api-opt-in-archive",
            groupMemberships = true,
            groupNoticesRead = true,
            hgOutboundIm = false,
            groupLiveChat = false,
            webPush = false,
            deliveryReceipts = false
        }));

        app.MapGet("/auth/start",
            (HttpContext ctx, MessengerSessionManager sessions) =>
                Results.Redirect(sessions.StartLogin(ctx).AbsoluteUri))
            .RequireRateLimiting("login");

        app.MapGet("/auth/callback", async (
            HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world, MessengerOptions options) =>
        {
            string code = ctx.Request.Query["code"].ToString();
            string state = ctx.Request.Query["state"].ToString();
            if (code.Length is < 10 or > 4096 || state.Length is < 10 or > 256)
                return Results.Redirect("/?auth=failed");

            OAuthFlow? flow = sessions.ConsumeFlow(ctx, state);
            if (flow == null)
                return Results.Redirect("/?auth=failed");

            OAuthTokens? tokens = await world.ExchangeAsync(
                code, flow.Verifier, ctx.RequestAborted);
            if (tokens == null || string.IsNullOrWhiteSpace(tokens.RefreshToken))
                return Results.Redirect("/?auth=failed");

            var identity = await world.GetLocalAvatarAsync(
                tokens.AccessToken, ctx.RequestAborted);
            if (identity == null)
            {
                await world.RevokeAsync(tokens.RefreshToken, ctx.RequestAborted);
                return Results.Redirect("/?auth=failed");
            }

            sessions.Login(ctx, identity.Value.Id, tokens);
            return Results.Redirect("/");
        }).RequireRateLimiting("login");

        app.MapGet("/api/v1/auth/me", async (
            HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world) =>
        {
            MessengerSession? session = await sessions.ResolveAsync(
                ctx, ctx.RequestAborted);
            if (session == null)
                return Results.Unauthorized();
            var identity = await world.GetLocalAvatarAsync(
                session.AccessToken, ctx.RequestAborted);
            if (identity == null || identity.Value.Id != session.AvatarId)
                return Results.Unauthorized();
            JsonElement profile = identity.Value.Profile;
            return Results.Json(new
            {
                principalId = identity.Value.Id,
                displayName = GetProperty(profile, "display_name"),
                username = GetProperty(profile, "username"),
                csrfToken = session.CsrfToken
            });
        });

        app.MapPost("/api/v1/auth/logout", async (
            HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world) =>
        {
            MessengerSession? session = await sessions.ResolveAsync(
                ctx, ctx.RequestAborted);
            if (session == null)
                return Results.Unauthorized();
            if (!MessengerSessionManager.CheckCsrf(ctx, session))
                return Results.StatusCode(403);
            sessions.Logout(ctx);
            try
            {
                await world.RevokeAsync(session.RefreshToken, ctx.RequestAborted);
            }
            catch (HttpRequestException) { /* local session already revoked */ }
            catch (TaskCanceledException) { /* local session already revoked */ }
            return Results.Ok(new { signedOut = true });
        });

        app.MapGet("/api/v1/friends", async (
            HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world) => await ProxyGet(ctx, sessions, world,
                session => "api/v1/relationships/" + session.AvatarId.ToString("D")));

        app.MapGet("/api/v1/groups", async (
            HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world) => await ProxyGet(ctx, sessions, world,
                _ => "api/v1/groups/mine?limit=100"));

        app.MapGet("/api/v1/groups/{groupId:guid}/notices", async (
            Guid groupId, HttpContext ctx,
            MessengerSessionManager sessions, WorldApiGateway world) =>
            await ProxyGet(ctx, sessions, world, _ =>
                "api/v1/groups/" + groupId.ToString("D") + "/notices"));

        app.MapGet("/api/v1/conversations", async (
            HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world) => await ProxyGet(ctx, sessions, world, _ =>
                "api/v1/messages?limit=100"));

        app.MapGet("/api/v1/messages", async (
            HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world) =>
        {
            if (!long.TryParse(ctx.Request.Query["after"].FirstOrDefault() ?? "0",
                out long after) || after < 0)
                return Results.BadRequest(new { error = "invalid_cursor" });
            return await ProxyGet(ctx, sessions, world, _ =>
                "api/v1/messages/events?after=" + after + "&limit=100");
        });

        app.MapGet("/api/v1/messages/with/{peer:guid}", async (
            Guid peer, HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world) =>
        {
            if (peer == Guid.Empty)
                return Results.BadRequest(new { error = "invalid_peer" });
            return await ProxyGet(ctx, sessions, world, _ =>
                "api/v1/messages/with/" + peer.ToString("D") + "?limit=100");
        });

        app.MapGet("/api/v1/messages/settings", async (
            HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world) => await ProxyGet(ctx, sessions, world, _ =>
                "api/v1/messages/settings"));

        app.MapPatch("/api/v1/messages/settings", async (
            HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world, ArchiveSetting input) =>
        {
            MessengerSession? session = await sessions.ResolveAsync(
                ctx, ctx.RequestAborted);
            if (session == null) return Results.Unauthorized();
            if (!MessengerSessionManager.CheckCsrf(ctx, session))
                return Results.StatusCode(403);
            WorldApiResponse response = await world.RequestAsync(
                HttpMethod.Patch, "api/v1/messages/settings",
                session.AccessToken, new { enabled = input.Enabled }, ctx.RequestAborted);
            return Relay(response);
        });

        app.MapPost("/api/v1/im", async (
            HttpContext ctx, MessengerSessionManager sessions,
            WorldApiGateway world, MessengerSessionStore store,
            SendMessageRequest request) =>
        {
            MessengerSession? session = await sessions.ResolveAsync(
                ctx, ctx.RequestAborted);
            if (session == null) return Results.Unauthorized();
            if (!MessengerSessionManager.CheckCsrf(ctx, session))
                return Results.StatusCode(403);

            if (request == null || request.ClientMessageId == Guid.Empty ||
                request.Recipient == null || request.Recipient.AgentId == Guid.Empty ||
                request.Recipient.AgentId == session.AvatarId ||
                string.IsNullOrWhiteSpace(request.Body) ||
                Encoding.UTF8.GetByteCount(request.Body) > 1024)
                return Results.BadRequest(new { error = "invalid_message" });

            // World API currently sends direct IM to local confirmed friends.
            // Do not report cross-grid delivery until the native HG adapter
            // can prove its authorization and delivery semantics.
            if (!request.Recipient.IsLocal)
                return Results.Json(new { error = "hg_route_unavailable" },
                    statusCode: StatusCodes.Status501NotImplemented);

            SendReservation reservation = store.ReserveSend(
                session.AvatarId, request.ClientMessageId,
                request.Recipient.AgentId, request.Body);
            if (reservation.Status == SendReservationStatus.Conflict)
                return Results.Conflict(new { error = "idempotency_key_reused" });
            if (reservation.Status == SendReservationStatus.Pending)
                return Results.Conflict(new { error = "delivery_unconfirmed_retry_not_safe" });
            if (reservation.Status == SendReservationStatus.Completed)
                return Results.Content(reservation.Response ?? "{}",
                    "application/json", statusCode: reservation.HttpStatus);

            // The World API owns actual grid/OfflineIM routing. The local ledger
            // prevents duplicate submits, even if this HTTP request times out
            // after the authoritative gateway has accepted a message.
            WorldApiResponse response = await world.RequestAsync(
                HttpMethod.Post, "api/v1/messages", session.AccessToken,
                new
                {
                    to_agent_id = request.Recipient.AgentId.ToString("D"),
                    message = request.Body
                }, ctx.RequestAborted);

            if (response.Status is >= 200 and < 500)
                store.CompleteSend(session.AvatarId,
                    request.ClientMessageId, response.Status, response.Body);

            return Relay(response);
        }).RequireRateLimiting("send");

        app.MapFallback("/api/v1/{**path}", () =>
            Results.Json(new { error = "endpoint_not_implemented" },
                statusCode: 501));
    }

    private sealed record ArchiveSetting(bool Enabled);

    private static string GetProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static async Task<IResult> ProxyGet(
        HttpContext ctx, MessengerSessionManager sessions,
        WorldApiGateway world, Func<MessengerSession, string> path)
    {
        MessengerSession? session = await sessions.ResolveAsync(
            ctx, ctx.RequestAborted);
        if (session == null) return Results.Unauthorized();
        string resolvedPath = path(session);
        WorldApiResponse response = await world.RequestAsync(
            HttpMethod.Get, resolvedPath, session.AccessToken, null,
            ctx.RequestAborted);
        return Relay(response);
    }

    private static IResult Relay(WorldApiResponse response)
    {
        return Results.Content(response.Body, "application/json",
            statusCode: response.Status);
    }
}
