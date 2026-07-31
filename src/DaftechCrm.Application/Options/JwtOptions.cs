namespace DaftechCrm.Application.Options;

/// <summary>
/// Signing/validation settings for the bearer tokens issued at login.
/// SigningKey must be a high-entropy secret (32+ bytes) supplied via
/// configuration/user-secrets/environment — never committed to source.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = default!;
    public string Issuer { get; set; } = "DaftechCrm";
    public string Audience { get; set; } = "DaftechCrmClients";

    /// <summary>Short-lived access token; the frontend refreshes on 401 (see auth.interceptor.ts).</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Longer-lived refresh token, tracked as its own LoginSession row so it can be revoked on logout.</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
