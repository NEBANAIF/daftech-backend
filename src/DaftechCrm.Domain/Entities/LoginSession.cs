using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

/// <summary>
/// SRS v2.0 §3.3 / §4.8: tracks a login session for any account type
/// (Employee or Client — Admin/IT Support/Technician are all Employee
/// rows distinguished by Roles). Distinct from LoginRecord: LoginRecord is
/// an immutable audit log of every attempt (including blocked ones);
/// LoginSession is the live/updatable presence record — one row per
/// active or most-recent session — that answers "is this person online
/// right now, and when did we last see them."
/// </summary>
public class LoginSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public SessionAccountType AccountType { get; set; }
    public Guid AccountId { get; set; }

    public string IpAddress { get; set; } = default!;
    public DateTimeOffset LoginTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LogoutTime { get; set; }

    public bool OnlineStatus { get; set; } = true;
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
}
