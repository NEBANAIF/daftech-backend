using DaftechCrm.Application.Interfaces;

namespace DaftechCrm.Api.Services;

/// <summary>
/// Resolves the caller's IP address for the current HTTP request — this is
/// the real implementation behind the frontend's "capture employee IP on
/// login" requirement. Prefers X-Forwarded-For (set by a reverse proxy /
/// load balancer in front of the API) and falls back to the raw socket
/// address for direct connections.
/// </summary>
public class HttpCurrentRequestContext : ICurrentRequestContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentRequestContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string ResolveClientIpAddress()
    {
        var context = _accessor.HttpContext;
        if (context is null) return "unknown";

        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // X-Forwarded-For can be a comma-separated chain of proxies; the
            // first entry is the original client.
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
