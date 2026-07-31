using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

public record ClientDto(
    Guid Id, string Name, string IdNumber, string PhoneNumber, string Email, string Office, string Location,
    string KycType, string KycContact, string? ItSupportContact,
    ClientAccountStatus AccountStatus, DateOnly OnboardingDate, string? RejectionReason,
    string? Username, bool MustChangePassword
);

/// <summary>Self-service signup — still available, still lands in Pending for Admin approval, still has no credentials until approved.</summary>
public record CreateClientSignupRequest(
    string Name, string IdNumber, string PhoneNumber, string Email, string Office, string Location
);

/// <summary>Admin registers a client directly — Approved immediately, with credentials issued and emailed in the same request.</summary>
public record RegisterClientRequest(
    string Name, string IdNumber, string PhoneNumber, string Email, string Office, string Location,
    string KycType, string KycContact, string? ItSupportContact
);

public record ClientRegisteredResult(ClientDto Client, string Username, string OneTimePassword, bool EmailSent, string? EmailError);

public record RejectClientRequest(string Reason);

/// <summary>Client logs in with the system-issued username and their current password.</summary>
public record ClientLoginRequest(string Username, string Password);

/// <summary>AccessToken/RefreshToken: see EmployeeLoginResult remarks — same pattern, both null on failed logins.</summary>
public record ClientLoginResult(
    bool Success, string? Message, ClientDto? Client, bool MustChangePassword,
    string? AccessToken = null, DateTimeOffset? AccessTokenExpiresAt = null, string? RefreshToken = null);

public record ClientChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);

public record ResendClientCredentialEmailResult(bool EmailSent, string? EmailError);
