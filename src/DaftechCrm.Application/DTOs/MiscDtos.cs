using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

public record AgreementDto(
    Guid Id, Guid ClientId, string DocumentNumber, string? ScannedFileUrl, string AgreementPlace,
    DateOnly SignDate, DateOnly ExpiryDate, int SupportWindowMonths, AgreementStatus Status, BillingTier BillingTier
);

public record CreateAgreementRequest(
    Guid ClientId, string DocumentNumber, string? ScannedFileUrl, string AgreementPlace,
    DateOnly SignDate, DateOnly? ExpiryDate, int SupportWindowMonths, BillingTier BillingTier
);

public record TimeLogDto(Guid Id, Guid EmployeeId, DateOnly Date, DateTimeOffset? StartTime, DateTimeOffset? FinishTime, double? TotalHours);

public record MaintenanceRecordDto(
    Guid Id, DateOnly Date, string Category, string Description,
    Guid PerformedByEmployeeId, MaintenanceStatus Status, string? Remarks
);

public record CreateMaintenanceRecordRequest(string Category, string Description, Guid PerformedByEmployeeId, MaintenanceStatus Status, string? Remarks);

public record NotificationDto(Guid Id, NotificationRecipientType RecipientType, string RecipientId, string EventType, string Message, DateTimeOffset DateSent, bool ReadStatus);
