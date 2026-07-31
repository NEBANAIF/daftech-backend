using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DaftechCrm.Infrastructure.Auth;

/// <summary>
/// Claim name carrying the account type (Employee/Client) — read back by
/// authorization policies and controllers via ClaimTypes/CrmClaimTypes.
/// </summary>
public static class CrmClaimTypes
{
    public const string AccountType = "acct_type";
}

public class JwtTokenService : ITokenService
{
    private readonly IAppDbContext _db;
    private readonly JwtOptions _options;

    public JwtTokenService(IAppDbContext db, IOptions<JwtOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<IssuedTokenPair> IssueTokenPairAsync(
        SessionAccountType accountType, Guid accountId, IReadOnlyList<string> roles, string ipAddress, CancellationToken ct = default)
    {
        var access = CreateAccessToken(accountType, accountId, roles);
        var refresh = await CreateAndPersistRefreshTokenAsync(accountType, accountId, ipAddress, ct);
        return new IssuedTokenPair(access.Token, access.ExpiresAt, refresh.rawToken, refresh.expiresAt);
    }

    public async Task<IssuedTokenPair?> RefreshAsync(string refreshToken, string ipAddress, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null) return null;

        if (!existing.IsActive)
        {
            // Presented an expired-but-not-yet-rotated token, or one already
            // used once before (ReplacedByTokenHash set): reuse of a rotated
            // token means the token was likely stolen — revoke the whole
            // chain for this account so both the attacker and the legitimate
            // holder are forced to log in again.
            if (existing.RevokedAt is not null && existing.ReplacedByTokenHash is not null)
                await RevokeAllForAccountAsync(existing.AccountType, existing.AccountId, ct);
            return null;
        }

        var (rawNewToken, newTokenRow) = CreateRefreshTokenEntity(existing.AccountType, existing.AccountId, ipAddress);
        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.ReplacedByTokenHash = newTokenRow.TokenHash;
        _db.Update(existing);
        _db.Add(newTokenRow);

        var employee = existing.AccountType == SessionAccountType.Employee
            ? await _db.Employees.FirstOrDefaultAsync(e => e.Id == existing.AccountId, ct)
            : null;
        var client = existing.AccountType == SessionAccountType.Client
            ? await _db.Clients.FirstOrDefaultAsync(c => c.Id == existing.AccountId, ct)
            : null;

        // Account may have been disabled/rejected since the refresh token was issued — re-check on every refresh.
        if (employee is not null && employee.AccountStatus == EmployeeAccountStatus.Disabled) return null;
        if (client is not null && client.AccountStatus != ClientAccountStatus.Approved) return null;
        if (employee is null && client is null) return null;

        var roles = employee?.Roles.Select(r => r.ToString()).ToList() ?? new List<string>();

        await _db.SaveChangesAsync(ct);

        var access = CreateAccessToken(existing.AccountType, existing.AccountId, roles);
        return new IssuedTokenPair(access.Token, access.ExpiresAt, rawNewToken, newTokenRow.ExpiresAt);
    }

    public async Task RevokeAllForAccountAsync(SessionAccountType accountType, Guid accountId, CancellationToken ct = default)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.AccountType == accountType && t.AccountId == accountId && t.RevokedAt == null)
            .ToListAsync(ct);

        if (active.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var t in active)
        {
            t.RevokedAt = now;
            _db.Update(t);
        }
        await _db.SaveChangesAsync(ct);
    }

    private AccessTokenResult CreateAccessToken(SessionAccountType accountType, Guid accountId, IReadOnlyList<string> roles)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing or too short (need 32+ bytes). Set it via user-secrets/environment — never commit it.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, accountId.ToString()),
            new(CrmClaimTypes.AccountType, accountType.ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private async Task<(string rawToken, DateTimeOffset expiresAt)> CreateAndPersistRefreshTokenAsync(
        SessionAccountType accountType, Guid accountId, string ipAddress, CancellationToken ct)
    {
        var (raw, entity) = CreateRefreshTokenEntity(accountType, accountId, ipAddress);
        _db.Add(entity);
        await _db.SaveChangesAsync(ct);
        return (raw, entity.ExpiresAt);
    }

    private (string rawToken, RefreshToken entity) CreateRefreshTokenEntity(SessionAccountType accountType, Guid accountId, string ipAddress)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            AccountType = accountType,
            AccountId = accountId,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays),
            CreatedByIp = ipAddress,
        };
        return (rawToken, entity);
    }

    private static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
