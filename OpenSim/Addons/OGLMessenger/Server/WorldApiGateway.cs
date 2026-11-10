using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OpenSim.Addons.OGLMessenger.Server;

public sealed record OAuthTokens(string AccessToken, string? RefreshToken, long ExpiresIn);
public sealed record WorldApiResponse(int Status, string Body)
{
    public bool Success => Status is >= 200 and < 300;
}

public sealed class WorldApiGateway(IHttpClientFactory clients, MessengerOptions options)
{
    private HttpClient Create() => clients.CreateClient("world-api");

    public Uri AuthorizationUrl(string state, string challenge)
    {
        var values = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.CallbackUrl,
            ["scope"] = string.Join(" ", options.Scopes),
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state
        };
        string query = string.Join("&", values.Select(pair =>
            Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
        return new Uri(options.WorldBase, "oauth/authorize?" + query);
    }

    public async Task<OAuthTokens?> ExchangeAsync(
        string code, string verifier, CancellationToken cancellationToken)
    {
        return await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.CallbackUrl,
            ["code_verifier"] = verifier
        }, cancellationToken);
    }

    public async Task<OAuthTokens?> RefreshAsync(
        string refreshToken, CancellationToken cancellationToken)
    {
        return await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = options.ClientId
        }, cancellationToken);
    }

    private async Task<OAuthTokens?> RequestTokenAsync(
        Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(options.ClientSecret))
            data["client_secret"] = options.ClientSecret;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(options.WorldBase, "oauth/token"))
        {
            Content = new FormUrlEncodedContent(data)
        };
        using HttpResponseMessage response = await Create().SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument json = await JsonDocument.ParseAsync(stream,
            cancellationToken: cancellationToken);
        JsonElement root = json.RootElement;
        if (!root.TryGetProperty("access_token", out JsonElement access) ||
            access.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(access.GetString()))
            return null;

        string? refresh = root.TryGetProperty("refresh_token", out JsonElement r) &&
            r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        long expires = root.TryGetProperty("expires_in", out JsonElement seconds) &&
            seconds.TryGetInt64(out long value) ? value : 0;
        if (expires <= 0 || expires > 86400)
            return null;
        return new OAuthTokens(access.GetString()!, refresh, expires);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;
        var values = new Dictionary<string, string>
        {
            ["token"] = refreshToken,
            ["client_id"] = options.ClientId,
            ["token_type_hint"] = "refresh_token"
        };
        if (!string.IsNullOrEmpty(options.ClientSecret))
            values["client_secret"] = options.ClientSecret;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(options.WorldBase, "oauth/revoke"))
        {
            Content = new FormUrlEncodedContent(values)
        };
        using var response = await Create().SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        // Revocation is best effort if the backend is temporarily unavailable.
    }

    public async Task<WorldApiResponse> RequestAsync(
        HttpMethod method, string relativePath, string accessToken,
        object? payload, CancellationToken cancellationToken)
    {
        if (!relativePath.StartsWith("api/v1/", StringComparison.Ordinal) ||
            relativePath.Contains("://", StringComparison.Ordinal) ||
            relativePath.Contains("..", StringComparison.Ordinal) ||
            relativePath.Contains('#', StringComparison.Ordinal))
            throw new ArgumentException("Unexpected upstream path.", nameof(relativePath));

        using var request = new HttpRequestMessage(method,
            new Uri(options.WorldBase, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (payload is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(payload),
                Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await Create().SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.Content.Headers.ContentLength > 1024 * 1024)
            throw new InvalidOperationException("World API response too large.");
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (Encoding.UTF8.GetByteCount(body) > 1024 * 1024)
            throw new InvalidOperationException("World API response too large.");
        return new WorldApiResponse((int)response.StatusCode, body);
    }

    public async Task<(Guid Id, JsonElement Profile)?> GetLocalAvatarAsync(
        string accessToken, CancellationToken cancellationToken)
    {
        WorldApiResponse response = await RequestAsync(HttpMethod.Get,
            "api/v1/users/me", accessToken, null, cancellationToken);
        if (!response.Success)
            return null;
        using JsonDocument document = JsonDocument.Parse(response.Body);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("principal_id", out JsonElement principal) ||
            !Guid.TryParse(principal.GetString(), out Guid uuid) ||
            uuid == Guid.Empty ||
            !root.TryGetProperty("local_to_grid", out JsonElement local) ||
            local.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("active", out JsonElement active) ||
            active.ValueKind != JsonValueKind.True)
            return null;
        return (uuid, root.Clone());
    }
}
