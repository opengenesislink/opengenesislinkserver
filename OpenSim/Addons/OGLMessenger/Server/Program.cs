using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// The API must be deployed behind an HTTPS reverse proxy. No avatar password
// is ever processed until the trusted local-account adapter is implemented.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/healthz");
app.MapGet("/api/v1/capabilities", () => Results.Ok(new
{
    service = "NexVerse Messenger",
    version = "g1-development",
    authenticationReady = false,
    messagingReady = false,
    groupsReady = false,
    pushReady = false
}));

// Fail closed. Never issue sessions based solely on a submitted avatar name.
app.MapPost("/api/v1/auth/login", () =>
    Results.Problem(
        title: "Authentication adapter unavailable",
        detail: "Local avatar authentication has not been connected.",
        statusCode: StatusCodes.Status503ServiceUnavailable));

app.MapFallback("/api/v1/{**path}", () =>
    Results.Problem(
        title: "Endpoint not implemented",
        statusCode: StatusCodes.Status501NotImplemented));

app.Run();

public partial class Program;
