using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

public class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = default!;

    /// <summary>Used for login-credential delivery (SRS v2.0 §4.3.1) and notifications.</summary>
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;

    /// <summary>Technical specialization: Front-end, Back-end, or Database (SRS v2.0 §4.4.1) — extendable free text, not a closed enum, per "extendable list" wording.</summary>
    public string Specialization { get; set; } = default!;

    /// <summary>Stored as a comma-separated list of EmployeeRole values (see EF config).</summary>
    public List<EmployeeRole> Roles { get; set; } = new();

    public EmployeeAccountStatus AccountStatus { get; set; } = EmployeeAccountStatus.Active;

    /// <summary>
    /// System-generated login username (initials + random digits, e.g.
    /// "mf4821") — set once at registration by AccountCredentialService,
    /// never chosen by the employee.
    /// </summary>
    public string Username { get; set; } = default!;

    /// <summary>PBKDF2 hash of the current password — never the plaintext. See PasswordHasher.</summary>
    public string PasswordHash { get; set; } = default!;

    /// <summary>
    /// True from registration until the employee successfully changes their
    /// password. While true, every endpoint except the change-password flow
    /// is blocked for this account.
    /// </summary>
    public bool MustChangePassword { get; set; } = true;

    /// <summary>Empty = no IP restriction, this account may log in from any IP.</summary>
    public List<string> AllowedIpAddresses { get; set; } = new();

    public DateTimeOffset? DisabledAt { get; set; }
    public string? DisabledReason { get; set; }

    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
    public ICollection<DeviceSession> DeviceSessions { get; set; } = new List<DeviceSession>();
    public ICollection<LoginRecord> LoginRecords { get; set; } = new List<LoginRecord>();
}

public class DeviceSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public DeviceType DeviceType { get; set; }
    public string DeviceIdentifier { get; set; } = default!;
    public string IpAddress { get; set; } = default!;
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
    public DeviceAccessStatus AccessStatus { get; set; } = DeviceAccessStatus.Allowed;
}

/// <summary>
/// A single login attempt with the resolved IP address — captured on every
/// employee login (successful or blocked) per the access-control requirement.
/// </summary>
public class LoginRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string IpAddress { get; set; } = default!;
    public DeviceType DeviceType { get; set; }
    public string DeviceIdentifier { get; set; } = default!;
    public bool Allowed { get; set; }
    public string? Reason { get; set; }
}
