using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string IdNumber { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Office { get; set; } = default!;
    public string Location { get; set; } = default!;
    public string KycType { get; set; } = default!;
    public string KycContact { get; set; } = default!;
    public string? ItSupportContact { get; set; }
    public ClientAccountStatus AccountStatus { get; set; } = ClientAccountStatus.Pending;
    public DateOnly OnboardingDate { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>
    /// System-generated login username (initials + random digits) — null
    /// until credentials are issued (at registration for Admin-created
    /// clients, or at approval time for self-signup clients).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>PBKDF2 hash of the current password — null until credentials are issued. Never the plaintext.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>True until the client changes their password on first login.</summary>
    public bool MustChangePassword { get; set; } = true;

    public ICollection<Agreement> Agreements { get; set; } = new List<Agreement>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
