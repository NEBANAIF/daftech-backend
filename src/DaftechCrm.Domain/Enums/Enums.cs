namespace DaftechCrm.Domain.Enums;

public enum ClientAccountStatus { Pending, Approved, Rejected }

public enum EmployeeAccountStatus { Active, Disabled }

public enum AgreementStatus { Active, Expired, Pending }

public enum BillingTier { Basic, Intermediate, Advanced }

public enum TicketCategory { SqlDatabaseError, Bug, Other }

public enum TicketStatus
{
    Submitted,
    Forwarded,
    Assigned,
    InProgress,
    Resolved,
    AwaitingClientConfirmation,
    Escalated,
    Closed
}

public enum MaintenanceStatus { Resolved, InProgress, Recurring }

public enum EmployeeRole { Admin, ItSupport, EmployeeTechnician }

public enum DeviceType { Laptop, Pc, Tablet, Other }

public enum DeviceAccessStatus { Allowed, Revoked }

public enum NotificationRecipientType { Admin, ItSupport, Employee, Client }

public enum ClosureReason
{
    ClientConfirmedSatisfied,
    AutoClosedNoResponse
}

/// <summary>Distinguishes which table AccountId on LoginSession/session-related records points into.</summary>
public enum SessionAccountType
{
    Employee,
    Client
}
