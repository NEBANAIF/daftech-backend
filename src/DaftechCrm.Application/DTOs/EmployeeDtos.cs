using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

public record EmployeeDto(
    Guid Id, string FullName, string Email, string PhoneNumber, string Specialization,
    IReadOnlyList<EmployeeRole> Roles, EmployeeAccountStatus AccountStatus, IReadOnlyList<string> AllowedIpAddresses,
    DateTimeOffset? DisabledAt, string? DisabledReason, int OpenTicketCount,
    double? AverageSatisfactionScore, string Username, bool MustChangePassword
);

public record CreateEmployeeRequest(
    string FullName, string Email, string PhoneNumber, string Specialization,
    IReadOnlyList<EmployeeRole> Roles, IReadOnlyList<string> AllowedIpAddresses
);

/// <summary>
/// Returned once, immediately after registration. OneTimePassword is never
/// retrievable again after this response. EmailDelivery reports whether the
/// credential email actually sent — per SRS v2.0 §4.3.1, if it failed the
/// Admin still has the plaintext here to relay manually or retry sending.
/// </summary>
public record EmployeeRegisteredResult(EmployeeDto Employee, string Username, string OneTimePassword, bool EmailSent, string? EmailError);

public record DisableEmployeeRequest(string Reason);

public record AddAllowedIpRequest(string IpAddress);

public record DeviceSessionDto(Guid Id, DeviceType DeviceType, string DeviceIdentifier, string IpAddress, DateTimeOffset LastSeen, DeviceAccessStatus AccessStatus);

public record LoginRecordDto(Guid Id, DateTimeOffset Timestamp, string IpAddress, DeviceType DeviceType, string DeviceIdentifier, bool Allowed, string? Reason);

/// <summary>Employee logs in with the system-issued username and their current password (one-time or self-chosen).</summary>
public record EmployeeLoginRequest(string Username, string Password, DeviceType DeviceType, string DeviceIdentifier);

public record EmployeeLoginResult(bool Success, string? Message, string IpAddress, EmployeeDto? Employee, bool MustChangePassword);

/// <summary>Both fields are sent so the server can enforce match — never trust confirmation logic to the client alone.</summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);

/// <summary>Admin can retry sending the credential email if the original attempt failed (SRS v2.0 §4.3.1).</summary>
public record ResendCredentialEmailResult(bool EmailSent, string? EmailError);
