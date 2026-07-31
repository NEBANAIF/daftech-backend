namespace DaftechCrm.Application.Interfaces;

/// <summary>
/// Resolves the caller's IP address for the current request. Implemented in
/// the Api layer by reading HttpContext.Connection.RemoteIpAddress (and
/// X-Forwarded-For when behind a reverse proxy), so Application-layer
/// services never touch ASP.NET Core types directly.
/// </summary>
public interface ICurrentRequestContext
{
    string ResolveClientIpAddress();
}
