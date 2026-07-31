using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Application.Services;

public class SessionService : ISessionService
{
    private readonly IAppDbContext _db;
    private readonly SessionOptions _options;

    public SessionService(IAppDbContext db, IOptions<SessionOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<Guid> OpenSessionAsync(SessionAccountType accountType, Guid accountId, string ipAddress, CancellationToken ct = default)
    {
        var session = new LoginSession
        {
            AccountType = accountType,
            AccountId = accountId,
            IpAddress = ipAddress,
            OnlineStatus = true,
            LastSeen = DateTimeOffset.UtcNow,
        };
        _db.Add(session);
        await _db.SaveChangesAsync(ct);
        return session.Id;
    }

    public async Task CloseSessionAsync(SessionAccountType accountType, Guid accountId, CancellationToken ct = default)
    {
        var openSessions = await _db.LoginSessions
            .Where(s => s.AccountType == accountType && s.AccountId == accountId && s.OnlineStatus)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var s in openSessions)
        {
            s.OnlineStatus = false;
            s.LogoutTime = now;
            s.LastSeen = now;
            _db.Update(s);
        }

        if (openSessions.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    public async Task TouchAsync(SessionAccountType accountType, Guid accountId, CancellationToken ct = default)
    {
        var session = await _db.LoginSessions
            .Where(s => s.AccountType == accountType && s.AccountId == accountId)
            .OrderByDescending(s => s.LastSeen)
            .FirstOrDefaultAsync(ct);

        if (session is null) return;

        session.LastSeen = DateTimeOffset.UtcNow;
        session.OnlineStatus = true;
        _db.Update(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> MarkStaleSessionsOfflineAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-_options.OfflineAfterMinutes);
        var stale = await _db.LoginSessions
            .Where(s => s.OnlineStatus && s.LastSeen < cutoff)
            .ToListAsync(ct);

        foreach (var s in stale)
        {
            s.OnlineStatus = false;
            s.LogoutTime ??= s.LastSeen;
            _db.Update(s);
        }

        if (stale.Count > 0)
            await _db.SaveChangesAsync(ct);

        return stale.Count;
    }

    public async Task<IReadOnlyList<SessionActivityDto>> GetSessionActivityAsync(CancellationToken ct = default)
    {
        // GroupBy-then-First-per-group doesn't reliably translate to SQL
        // across providers (Pomelo/MySQL included), so pull sessions and
        // reduce to "most recent per account" in memory. Session volume
        // per account is small, so this stays cheap at this app's scale.
        var allSessions = await _db.LoginSessions.ToListAsync(ct);

        var latestPerAccount = allSessions
            .GroupBy(s => (s.AccountType, s.AccountId))
            .Select(g => g.OrderByDescending(s => s.LastSeen).First())
            .ToList();

        var employeeNames = await _db.Employees.ToDictionaryAsync(e => e.Id, e => e.FullName, ct);
        var clientNames = await _db.Clients.ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return latestPerAccount
            .Select(s => new SessionActivityDto(
                s.AccountType,
                s.AccountId,
                s.AccountType == SessionAccountType.Employee
                    ? employeeNames.GetValueOrDefault(s.AccountId, "Unknown")
                    : clientNames.GetValueOrDefault(s.AccountId, "Unknown"),
                s.OnlineStatus,
                s.LastSeen,
                s.IpAddress
            ))
            .OrderByDescending(d => d.OnlineStatus)
            .ThenByDescending(d => d.LastSeen)
            .ToList();
    }

    public async Task<IReadOnlyList<LoginSessionDto>> GetHistoryForAccountAsync(SessionAccountType accountType, Guid accountId, CancellationToken ct = default) =>
        await _db.LoginSessions
            .Where(s => s.AccountType == accountType && s.AccountId == accountId)
            .OrderByDescending(s => s.LoginTime)
            .Select(s => new LoginSessionDto(s.Id, s.IpAddress, s.LoginTime, s.LogoutTime, s.OnlineStatus, s.LastSeen))
            .ToListAsync(ct);
}
