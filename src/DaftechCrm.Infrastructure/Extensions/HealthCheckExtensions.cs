using System.Text.Json;
using DaftechCrm.Infrastructure.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DaftechCrm.Infrastructure.Extensions;

/// <summary>Registers the CRM health checks and maps /health, /health/ready and /health/live.</summary>
public static class HealthCheckExtensions
{
    public const string ReadyTag = "ready";
    public const string LiveTag = "live";

    public static IServiceCollection AddCrmHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag])
            .AddCheck<StorageHealthCheck>("storage", tags: [ReadyTag])
            .AddCheck<EmailHealthCheck>("email", tags: [ReadyTag])
            .AddCheck<ApiHealthCheck>("api", tags: [LiveTag]);

        return services;
    }

    public static IEndpointRouteBuilder MapCrmHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        var options = new HealthCheckOptions { ResponseWriter = WriteResponseAsync };

        endpoints.MapHealthChecks("/health", options).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteResponseAsync,
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LiveTag),
            ResponseWriter = WriteResponseAsync,
        }).AllowAnonymous();

        return endpoints;
    }

    /// <summary>Emits a detailed JSON payload instead of the default plain-text status.</summary>
    private static async Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            timestampUtc = DateTimeOffset.UtcNow,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds,
                error = entry.Value.Exception?.Message,
                data = entry.Value.Data,
            }),
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }
}
