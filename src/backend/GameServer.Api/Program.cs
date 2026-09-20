using GameServer.Application;
using GameServer.Infrastructure;
using GameServer.Api.Hubs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add application and infrastructure layer services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

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

// Simple health endpoint — returns "Healthy" / "Degraded" / "Unhealthy"
app.MapHealthChecks("/health");

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
});

app.MapControllers();
app.MapHub<BattleHub>("/hubs/battle");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }

