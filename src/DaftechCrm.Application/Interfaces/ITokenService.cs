using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.Interfaces;

public record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

public record IssuedTokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>
/// Issues and validates the JWT access token + opaque refresh token pair
/// used to authenticate every request after login (see auth.interceptor.ts
/// on the frontend, which already expects this pair — SessionService only
/// ever tracked presence, it never actually issued a credential).
/// </summary>
public interface ITokenService
{
    /// <summary>Mints a new access token (short-lived, carries identity + role claims) and a fresh persisted refresh token.</summary>
    Task<IssuedTokenPair> IssueTokenPairAsync(SessionAccountType accountType, Guid accountId, IReadOnlyList<string> roles, string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Validates a presented refresh token, revokes it, and issues a new pair
    /// (rotation). Returns null if the token is missing, expired, revoked, or
    /// already-rotated (reuse of a rotated token revokes the whole chain —
    /// treated as a signal of theft).
    /// </summary>
    Task<IssuedTokenPair?> RefreshAsync(string refreshToken, string ipAddress, CancellationToken ct = default);

    /// <summary>Revokes every active refresh token for an account (logout, password change, account disable).</summary>
    Task RevokeAllForAccountAsync(SessionAccountType accountType, Guid accountId, CancellationToken ct = default);
}
