using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace DaftechCrm.Api.Middleware;

/// <summary>
/// Rate limiting built on <c>System.Threading.RateLimiting</c> (.NET 8 built-in).
/// Two policies: a global 100 requests/minute per caller, and a stricter
/// 10 attempts/minute for authentication endpoints to blunt brute-force attacks.
/// Rejections return 429 with a <c>Retry-After</c> header.
/// </summary>
public static class RateLimitingMiddleware
{
    /// <summary>Policy name applied to auth endpoints via <c>[EnableRateLimiting]</c> or route metadata.</summary>
    public const string AuthPolicy = "auth";

    private const int GlobalPermitsPerMinute = 100;
    private const int AuthPermitsPerMinute = 10;

    public static IServiceCollection AddCrmRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(ResolvePartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = GlobalPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                }));

            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter($"auth:{ResolvePartitionKey(context)}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AuthPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
                    ? value
                    : TimeSpan.FromMinutes(1);

                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                context.HttpContext.Response.ContentType = "application/problem+json";

                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("DaftechCrm.RateLimiting");
                logger.LogWarning("Rate limit hit by {Caller} for {Path}",
                    ResolvePartitionKey(context.HttpContext), context.HttpContext.Request.Path);

                await context.HttpContext.Response.WriteAsync(
                    $"{{\"status\":429,\"title\":\"Too many requests\",\"detail\":\"Rate limit exceeded. Retry after {(int)retryAfter.TotalSeconds} seconds.\"}}",
                    cancellationToken);
            };
        });

        return services;
    }

    /// <summary>Partition by authenticated user when available, otherwise by remote IP.</summary>
    private static string ResolvePartitionKey(HttpContext context) =>
        context.User?.Identity?.IsAuthenticated == true
            ? $"user:{context.User.Identity!.Name}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}
