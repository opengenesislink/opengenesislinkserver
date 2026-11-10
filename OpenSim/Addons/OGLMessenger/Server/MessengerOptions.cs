using System.Net;

namespace OpenSim.Addons.OGLMessenger.Server;

public sealed class MessengerOptions
{
    public string WorldApiBaseUrl { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string DatabasePath { get; set; } = "";
    public string DataProtectionKeysPath { get; set; } = "";
    public string TrustedProxyIp { get; set; } = "";
    public int SessionDays { get; set; } = 30;
    public string[] Scopes { get; set; } =
    [
        "openid", "offline_access", "users:read", "relationships:read",
        "relationships:write", "groups:read"
    ];

    public bool IsReady =>
        Uri.TryCreate(WorldApiBaseUrl, UriKind.Absolute, out Uri? world) &&
        world.Scheme == Uri.UriSchemeHttps &&
        Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out Uri? site) &&
        site.Scheme == Uri.UriSchemeHttps &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(DatabasePath) &&
        !string.IsNullOrWhiteSpace(DataProtectionKeysPath) &&
        SessionDays is >= 1 and <= 30;

    public Uri WorldBase => new(WorldApiBaseUrl.TrimEnd('/') + "/");
    public Uri PublicBase => new(PublicBaseUrl.TrimEnd('/') + "/");
    public string CallbackUrl => new Uri(PublicBase, "auth/callback").AbsoluteUri;

    public static MessengerOptions Read(IConfiguration configuration)
    {
        var o = configuration.GetSection("Messenger").Get<MessengerOptions>() ?? new();
        // Do not use localhost/insecure HTTP exceptions in production:
        // the service handles real avatar sessions and passwords at the
        // upstream OAuth consent page.
        return o;
    }
}
