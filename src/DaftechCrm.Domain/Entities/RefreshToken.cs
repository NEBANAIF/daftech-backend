using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

/// <summary>
/// A single refresh token issued at login. Only the SHA-256 hash is stored
/// (never the raw token) so a database read alone can't be used to forge a
/// session. Revoked explicitly on logout/password-change, or implicitly by
/// being replaced when used (rotation) — ReplacedByTokenHash links the chain
/// so reuse of an already-rotated token can be detected and the whole chain
/// revoked (token-theft protection).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public SessionAccountType AccountType { get; set; }
    public Guid AccountId { get; set; }

    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByIp { get; set; } = default!;

    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
