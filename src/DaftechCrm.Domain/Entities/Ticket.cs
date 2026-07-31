using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = default!;

    public Guid AgreementId { get; set; }
    public Agreement Agreement { get; set; } = default!;

    public string Description { get; set; } = default!;
    public TicketCategory Category { get; set; }
    public DateTimeOffset DateSubmitted { get; set; } = DateTimeOffset.UtcNow;

    public Guid? ForwardedByEmployeeId { get; set; }
    public Employee? ForwardedByEmployee { get; set; }

    /// <summary>
    /// Set automatically by the assignment engine the moment the ticket is
    /// forwarded — the Admin no longer chooses. See ITicketAssignmentService.
    /// </summary>
    public Guid? AssignedEmployeeId { get; set; }
    public Employee? AssignedEmployee { get; set; }

    /// <summary>Set the moment auto-assignment picks an employee (see TicketAssignmentService). Basis for the on-time/late resolution report.</summary>
    public DateTimeOffset? AssignedAt { get; set; }

    public bool Chargeable { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Submitted;

    // --- Client confirmation / satisfaction ---

    /// <summary>Set when the employee marks the ticket Resolved; starts the client-response clock.</summary
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Deadline after which an unanswered confirmation auto-closes the ticket (ResolvedAt + N days).</summary>
    public DateTimeOffset? ClientConfirmationDeadline { get; set; }

    /// <summary>1-5 stars, set when the client responds. Null if never rated (e.g. auto-closed).</summary>
    public int? SatisfactionStars { get; set; }

    /// <summary>Stars converted to a 0-100 score (stars * 20). Null if never rated.</summary>
    public int? SatisfactionScore { get; set; }

    public ClosureReason? ClosureReason { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    public ICollection<TicketAuditEntry> AuditTrail { get; set; } = new List<TicketAuditEntry>();
}

public class TicketAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = default!;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Actor { get; set; } = default!;
    public string Action { get; set; } = default!;
}
