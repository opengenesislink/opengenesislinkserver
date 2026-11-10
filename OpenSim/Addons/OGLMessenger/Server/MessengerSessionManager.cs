using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace OpenSim.Addons.OGLMessenger.Server;

public sealed record OAuthFlow(string State, string Verifier, long CreatedAt);

public sealed class MessengerSessionManager(
    MessengerOptions options,
    MessengerSessionStore store,
    WorldApiGateway gateway,
    IDataProtectionProvider provider)
{
    public const string SessionCookie = "__Host-nv-messenger";
    public const string FlowCookie = "__Host-nv-messenger-oauth";

    private readonly IDataProtector _flowProtector =
        provider.CreateProtector("OpenGenesisLINK.OGLMessenger.OAuthState.v1");

    // A single-node SQLite session store: serialize refresh-token rotation
    // so concurrent PWA polling requests cannot burn the same refresh token.
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public Uri StartLogin(HttpContext ctx)
    {
        string state = MessengerSessionStore.RandomSecret();
        string verifier = MessengerSessionStore.RandomSecret();
        string challenge = Convert.ToBase64String(
            SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var flow = new OAuthFlow(state, verifier,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        ctx.Response.Cookies.Append(FlowCookie,
            _flowProtector.Protect(JsonSerializer.Serialize(flow)),
            Cookie(TimeSpan.FromMinutes(10)));
        return gateway.AuthorizationUrl(state, challenge);
    }

    public OAuthFlow? ConsumeFlow(HttpContext ctx, string state)
    {
        string? protectedFlow = ctx.Request.Cookies[FlowCookie];
        ctx.Response.Cookies.Delete(FlowCookie, new CookieOptions
        {
            Path = "/", HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax
        });
        if (string.IsNullOrWhiteSpace(protectedFlow) ||
            protectedFlow.Length > 4096 || string.IsNullOrEmpty(state))
            return null;
        try
        {
            OAuthFlow? flow = JsonSerializer.Deserialize<OAuthFlow>(
                _flowProtector.Unprotect(protectedFlow));
            if (flow == null ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() - flow.CreatedAt > 600 ||
                flow.CreatedAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(flow.State), Encoding.UTF8.GetBytes(state)))
                return null;
            return flow;
        }
        catch (CryptographicException) { return null; }
        catch (JsonException) { return null; }
        catch (FormatException) { return null; }
    }

    public void Login(HttpContext ctx, Guid avatarId, OAuthTokens tokens)
    {
        string? old = ctx.Request.Cookies[SessionCookie];
        store.Revoke(old);
        string cookie = store.Create(avatarId, tokens, options.SessionDays);
        ctx.Response.Cookies.Append(SessionCookie, cookie,
            Cookie(TimeSpan.FromDays(options.SessionDays)));
    }

    public void Logout(HttpContext ctx)
    {
        store.Revoke(ctx.Request.Cookies[SessionCookie]);
        ctx.Response.Cookies.Delete(SessionCookie,
            new CookieOptions { Path = "/", Secure = true, HttpOnly = true });
    }

    public async Task<MessengerSession?> ResolveAsync(
        HttpContext ctx, CancellationToken cancellationToken)
    {
        string? token = ctx.Request.Cookies[SessionCookie];
        if (string.IsNullOrEmpty(token))
            return null;
        MessengerSession? current = store.Find(token);
        if (current == null)
            return null;

        if (current.AccessExpiresAt >
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60)
            return current;

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            current = store.Find(token);
            if (current == null)
                return null;
            if (current.AccessExpiresAt >
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60)
                return current;

            OAuthTokens? refreshed =
                await gateway.RefreshAsync(current.RefreshToken, cancellationToken);
            if (refreshed == null || string.IsNullOrWhiteSpace(refreshed.RefreshToken))
            {
                store.Revoke(token);
                return null;
            }

            store.UpdateTokens(token, refreshed);
            return store.Find(token);
        }
        finally { _refreshGate.Release(); }
    }

    public static bool CheckCsrf(HttpContext ctx, MessengerSession current)
    {
        string? header = ctx.Request.Headers["X-NexVerse-CSRF"];
        if (string.IsNullOrEmpty(header) ||
            header.Length != current.CsrfToken.Length ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(header), Encoding.ASCII.GetBytes(current.CsrfToken)))
            return false;

        // Reject cross-site Origin even if a browser happens to supply a
        // header. Host is validated by ASP.NET AllowedHosts and proxy config.
        if (ctx.Request.Headers.TryGetValue("Origin", out var origin) &&
            Uri.TryCreate(origin.ToString(), UriKind.Absolute, out Uri? uri))
            return uri.Scheme == ctx.Request.Scheme &&
                   uri.Authority.Equals(ctx.Request.Host.Value,
                       StringComparison.OrdinalIgnoreCase);
        return !ctx.Request.Headers.ContainsKey("Origin");
    }

    private static CookieOptions Cookie(TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = lifetime,
        IsEssential = true
    };
}
