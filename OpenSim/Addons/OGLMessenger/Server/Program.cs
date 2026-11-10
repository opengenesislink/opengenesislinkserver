using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using OpenSim.Addons.OGLMessenger.Server;

var builder = WebApplication.CreateBuilder(args);
MessengerOptions options = MessengerOptions.Read(builder.Configuration);
builder.Services.AddSingleton(options);
builder.Services.AddHealthChecks();

if (options.IsReady)
{
    string keys = Path.GetFullPath(options.DataProtectionKeysPath);
    Directory.CreateDirectory(keys);
    if (OperatingSystem.IsLinux())
        File.SetUnixFileMode(keys, UnixFileMode.UserRead |
            UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    builder.Services.AddDataProtection()
        .SetApplicationName("OpenGenesisLINK.OGLMessenger")
        .PersistKeysToFileSystem(new DirectoryInfo(keys));
}
else
{
    // This server still boots for diagnostics, but neither OAuth nor user
    // endpoints become available without secure HTTPS configuration.
    builder.Services.AddDataProtection();
}

builder.Services.AddSingleton<MessengerSessionStore>();
builder.Services.AddSingleton<MessengerSessionManager>();
builder.Services.AddHttpClient("world-api", client =>
    client.Timeout = TimeSpan.FromSeconds(12))
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<WorldApiGateway>();
builder.Services.AddRateLimiter(settings =>
{
    settings.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    settings.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 12, Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0, AutoReplenishment = true
                }));
    settings.AddPolicy("send", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30, Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0, AutoReplenishment = true
                }));
});

builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.Limits.MaxRequestBodySize = 16 * 1024);

var app = builder.Build();
var proxyOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto |
                       ForwardedHeaders.XForwardedFor,
    ForwardLimit = 1
};
if (!string.IsNullOrWhiteSpace(options.TrustedProxyIp))
{
    if (!IPAddress.TryParse(options.TrustedProxyIp, out IPAddress? trusted))
        throw new InvalidOperationException("Invalid trusted reverse proxy IP.");
    proxyOptions.KnownProxies.Add(trusted);
}
app.UseForwardedHeaders(proxyOptions);

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; style-src 'self'; " +
        "script-src 'self'; connect-src 'self'; manifest-src 'self'; " +
        "object-src 'none'; frame-ancestors 'none'; base-uri 'none'";

    if (context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Path.StartsWithSegments("/auth"))
        context.Response.Headers["Cache-Control"] = "no-store";

    if (context.Request.Path == "/healthz")
    {
        await next();
        return;
    }

    if (!options.IsReady &&
        (context.Request.Path.StartsWithSegments("/api") ||
         context.Request.Path.StartsWithSegments("/auth")))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "messenger_not_configured" });
        return;
    }

    if (options.IsReady && !context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "https_required" });
        return;
    }
    await next();
});

app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/healthz");
app.MapMessengerEndpoints();
app.Run();

public partial class Program;
