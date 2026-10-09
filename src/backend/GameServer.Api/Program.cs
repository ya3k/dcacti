using GameServer.Application;
using GameServer.Infrastructure;
using GameServer.Api.Authentication;
using GameServer.Api.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add application and infrastructure layer services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// The application session (API_CONTRACTS.md §2.3; ADR-015 D6): one JWT Bearer
// authentication scheme, plus the authorization pipeline that enforces it on
// every endpoint unless the endpoint opts out. The signing key is read from
// configuration here (D10), so a host that cannot validate sessions refuses to
// start rather than serving requests it cannot authenticate.
builder.Services.AddApplicationSessionAuthentication(builder.Configuration);

// Add ASP.NET Core services
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Health checks — core + infrastructure (Postgres/Redis, conditionally when connection strings present)
builder.Services.AddHealthChecks()
    .AddInfrastructureHealthChecks(builder.Configuration);

// Configure CORS for local development, Cloudflare tunnels, and Discord iframe hosting
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:5173", "https://localhost:5173", "http://127.0.0.1:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin)) return false;

            // Check explicitly configured origins
            if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;

            var uri = new Uri(origin);

            // Allow localhost on any port
            if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                return true;

            // Allow Cloudflare quick tunnels (*.trycloudflare.com)
            if (uri.Host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase))
                return true;

            // Allow Discord Activity origins (*.discordsays.com)
            if (uri.Host.EndsWith(".discordsays.com", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("FrontendPolicy");

// The authorization pipeline (ADR-015 D6). ASP.NET Core requires this exact
// order: routing, then authentication (which validates the presented session),
// then authorization (which enforces it). BattleHub needs no separate
// registration: the hub is an endpoint like any other, and it authenticates its
// connection with the same scheme through SignalR's standard access-token
// mechanism (API_CONTRACTS.md §2.3 "Transport", SIGNALR_PROTOCOL.md §1 item 3).
//
// Routing is requested explicitly because the negotiate exemption below inspects
// the selected endpoint, which only exists once routing has run.
app.UseRouting();

app.UseAuthentication();

// Publishes the validated session's player_id as the request context's
// GameServer.PlayerId (ADR-015 D3; API_CONTRACTS.md §2.3 "Identity"). It runs
// after authentication so it republishes an identity that was already validated
// and never invents one: a request with no valid session leaves the key unset.
app.UseMiddleware<AuthenticatedPlayerMiddleware>();

// SignalR's negotiate handshake is the one request on the hub transport that
// cannot carry the session, and it must be reachable for the documented
// access-token mechanism to work at all: the client learns its connection id
// from negotiate and attaches `Authorization` only to the requests that follow.
// Without this exemption the fallback session policy rejects the handshake and no
// client can reach the hub.
//
// The exemption is scoped to that single request. It marks the negotiate
// *endpoint* anonymous for authorization; it grants no identity, authentication
// still runs, and every request that carries the hub connection is authenticated
// and held to the session policy exactly as SIGNALR_PROTOCOL.md §1 items 3–5
// require.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method)
        && context.Request.Path.Equals(
            ApplicationSessionAuthentication.HubNegotiatePath,
            StringComparison.OrdinalIgnoreCase)
        && context.GetEndpoint() is { } negotiate)
    {
        // Keep the negotiate endpoint itself — its metadata is what routes the
        // request to the SignalR dispatcher — and add the one marker
        // authorization looks for.
        context.SetEndpoint(new Endpoint(
            negotiate.RequestDelegate,
            new EndpointMetadataCollection(
                negotiate.Metadata.Append(new AllowAnonymousAttribute())),
            negotiate.DisplayName));
    }

    await next();
});
app.UseAuthorization();

// Simple health endpoint — returns "Healthy" / "Degraded" / "Unhealthy"
//
// AllowAnonymous because these are transport-level liveness probes, not
// application resources: a probe that needed a session could not report on the
// service that issues sessions. They read no Player-scoped data and act for no
// Player, so they establish no identity and confer no authority
// (API_CONTRACTS.md §1 covers the application's REST surface, not probes).
app.MapHealthChecks("/health").AllowAnonymous();

// Detailed health endpoint — returns JSON with individual check results
app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration,
                tags = e.Value.Tags
            })
        }, new JsonSerializerOptions { WriteIndented = true });
        await context.Response.WriteAsync(result);
    }
}).AllowAnonymous();

app.MapControllers();

// BattleHub requires the same authenticated session as every REST endpoint
// (ADR-015 D6, SIGNALR_PROTOCOL.md §1 items 3–5), through the same one JWT
// Bearer scheme. It declares no authorization logic of its own and grows no
// second authentication mechanism (D6): the fallback session policy applied
// above is what enforces it, and the token arrives through SignalR's standard
// access-token mechanism. The hub is unusable without a valid session — a
// missing, invalid, tampered, expired, or identity-less token never yields a
// usable connection (§1 item 5) — and a Discord access token is not accepted as
// one (§1 item 4).
app.MapHub<BattleHub>("/hubs/battle");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }

