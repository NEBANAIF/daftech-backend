using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Application.Services;

public class TicketService : ITicketService
{
    private readonly IAppDbContext _db;
    private readonly ITicketAssignmentService _assignment;
    private readonly INotificationService _notifications;
    private readonly TicketWorkflowOptions _options;

    public TicketService(
        IAppDbContext db,
        ITicketAssignmentService assignment,
        INotificationService notifications,
        IOptions<TicketWorkflowOptions> options)
    {
        _db = db;
        _assignment = assignment;
        _notifications = notifications;
        _options = options.Value;
    }

    public async Task<TicketDto> SubmitFromClientAsync(SubmitTicketRequest request, CancellationToken ct = default)
    {
        var agreement = await _db.Agreements.FirstOrDefaultAsync(a => a.Id == request.AgreementId, ct)
            ?? throw new InvalidOperationException("Agreement not found.");

        var chargeable = !agreement.IsWithinSupportWindow(DateOnly.FromDateTime(DateTime.UtcNow));

        var ticket = new Ticket
        {
            ClientId = request.ClientId,
            AgreementId = request.AgreementId,
            Description = request.Description,
            Category = request.Category,
            Chargeable = chargeable,
            Status = TicketStatus.Submitted,
        };
        ticket.AuditTrail.Add(new TicketAuditEntry { TicketId = ticket.Id, Actor = "Client", Action = "Submitted ticket" });

        _db.Add(ticket);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(NotificationRecipientType.ItSupport, "ALL_IT_SUPPORT", "new_ticket", $"New ticket {ticket.Id} submitted.", ct);

        return await LoadDtoAsync(ticket.Id, ct);
    }

    public async Task<TicketDto> ForwardAsync(Guid ticketId, ForwardTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new InvalidOperationException("Ticket not found.");

        ticket.ForwardedByEmployeeId = request.ForwardedByEmployeeId;
        ticket.Status = TicketStatus.Forwarded;
        ticket.AuditTrail.Add(new TicketAuditEntry { TicketId = ticket.Id, Actor = "IT Support", Action = "Forwarded to assignment queue" });

        // Auto-assignment — no Admin choice. The moment a ticket is forwarded,
        // the system assigns it to whoever currently has the fewest open tickets.
        var assignee = await _assignment.SelectAssigneeAsync(ct);
        if (assignee is not null)
        {
            ticket.AssignedEmployeeId = assignee.Id;
            ticket.AssignedAt = DateTimeOffset.UtcNow;
            ticket.Status = TicketStatus.Assigned;
            ticket.AuditTrail.Add(new TicketAuditEntry
            {
                TicketId = ticket.Id,
                Actor = "System",
                Action = $"Auto-assigned to {assignee.FullName} (lowest open-ticket count)"
            });
        }

        _db.Update(ticket);
        await _db.SaveChangesAsync(ct);

        if (assignee is not null)
        {
            await _notifications.NotifyAsync(NotificationRecipientType.Employee, assignee.Id.ToString(), "ticket_assigned", $"You were assigned ticket {ticket.Id}.", ct);
            await _notifications.NotifyAsync(NotificationRecipientType.Client, ticket.ClientId.ToString(), "ticket_assigned", $"Your ticket {ticket.Id} has been assigned to a technician.", ct);
        }
        else
        {
            await _notifications.NotifyAsync(NotificationRecipientType.Admin, "ALL_ADMIN", "assignment_failed", $"Ticket {ticket.Id} forwarded but no eligible employee was available for auto-assignment.", ct);
        }

        return await LoadDtoAsync(ticket.Id, ct);
    }

    public async Task<TicketDto> UpdateStatusAsync(Guid ticketId, UpdateTicketStatusRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new InvalidOperationException("Ticket not found.");

        if (request.Status == TicketStatus.Resolved)
        {
            // Resolving doesn't close the ticket — it starts the client
            // confirmation window. The employee's "done" isn't the final word.
            ticket.Status = TicketStatus.AwaitingClientConfirmation;
            ticket.ResolvedAt = DateTimeOffset.UtcNow;
            ticket.ClientConfirmationDeadline = ticket.ResolvedAt.Value.AddDays(_options.ClientConfirmationWindowDays);
            ticket.AuditTrail.Add(new TicketAuditEntry
            {
                TicketId = ticket.Id,
                Actor = request.ActorName,
                Action = $"Marked Resolved by {request.ActorName}; awaiting client confirmation (deadline {ticket.ClientConfirmationDeadline:u})"
            });

            _db.Update(ticket);
            await _db.SaveChangesAsync(ct);

            await _notifications.NotifyAsync(NotificationRecipientType.Client, ticket.ClientId.ToString(), "awaiting_confirmation",
                $"Ticket {ticket.Id} has been marked resolved — please confirm it's working and rate your experience.", ct);
        }
        else
        {
            ticket.Status = request.Status;
            ticket.AuditTrail.Add(new TicketAuditEntry { TicketId = ticket.Id, Actor = request.ActorName, Action = $"Status changed to {request.Status}" });
            _db.Update(ticket);
            await _db.SaveChangesAsync(ct);
        }

        return await LoadDtoAsync(ticket.Id, ct);
    }

    public async Task<TicketDto> ConfirmResolutionAsync(Guid ticketId, ClientConfirmationRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new InvalidOperationException("Ticket not found.");

        if (ticket.Status != TicketStatus.AwaitingClientConfirmation)
            throw new InvalidOperationException("This ticket is not currently awaiting client confirmation.");

        if (!request.IsFixed)
        {
            // SRS v2.0 §4.5.1: "No" reopens the ticket to the assigned
            // employee — no rating is recorded, and this does NOT go
            // through the Escalated queue (that's reserved for a client
            // who says it IS fixed but rates the experience poorly).
            ticket.Status = TicketStatus.InProgress;
            ticket.ResolvedAt = null;
            ticket.ClientConfirmationDeadline = null;
            ticket.AuditTrail.Add(new TicketAuditEntry
            {
                TicketId = ticket.Id,
                Actor = "Client",
                Action = "Reported issue is NOT fixed — reopened to assigned employee"
            });

            _db.Update(ticket);
            await _db.SaveChangesAsync(ct);

            if (ticket.AssignedEmployeeId is { } reopenedEmpId)
                await _notifications.NotifyAsync(NotificationRecipientType.Employee, reopenedEmpId.ToString(), "ticket_reopened", $"Ticket {ticket.Id} was reopened — the client says it isn't fixed yet.", ct);
            await _notifications.NotifyAsync(NotificationRecipientType.Admin, "ALL_ADMIN", "ticket_reopened", $"Ticket {ticket.Id} reopened — client reported it's not fixed.", ct);

            return await LoadDtoAsync(ticket.Id, ct);
        }

        if (request.SatisfactionStars is not (>= 1 and <= 5))
            throw new ArgumentOutOfRangeException(nameof(request), "Satisfaction rating must be between 1 and 5 stars.");

        var stars = request.SatisfactionStars!.Value;
        var score = stars * 20;
        ticket.SatisfactionStars = stars;
        ticket.SatisfactionScore = score;

        if (score >= _options.MinimumSatisfactionScore)
        {
            ticket.Status = TicketStatus.Closed;
            ticket.ClosureReason = ClosureReason.ClientConfirmedSatisfied;
            ticket.ClosedAt = DateTimeOffset.UtcNow;
            ticket.AuditTrail.Add(new TicketAuditEntry
            {
                TicketId = ticket.Id,
                Actor = "Client",
                Action = $"Confirmed fixed and rated {stars}★ ({score}/100). Closed."
            });

            _db.Update(ticket);
            await _db.SaveChangesAsync(ct);

            await _notifications.NotifyAsync(NotificationRecipientType.Admin, "ALL_ADMIN", "ticket_closed", $"Ticket {ticket.Id} closed — {score}/100 satisfaction.", ct);
            if (ticket.AssignedEmployeeId is { } empId)
                await _notifications.NotifyAsync(NotificationRecipientType.Employee, empId.ToString(), "ticket_closed", $"Ticket {ticket.Id} closed — client rated {score}/100.", ct);
        }
        else
        {
            // Below the 90/100 threshold — this does not go back to the
            // employee, it escalates to Admin for review.
            ticket.Status = TicketStatus.Escalated;
            ticket.AuditTrail.Add(new TicketAuditEntry
            {
                TicketId = ticket.Id,
                Actor = "Client",
                Action = $"Confirmed fixed but rated {stars}★ ({score}/100) — below threshold, escalated to Admin"
            });

            _db.Update(ticket);
            await _db.SaveChangesAsync(ct);

            await _notifications.NotifyAsync(NotificationRecipientType.Admin, "ALL_ADMIN", "ticket_escalated", $"Ticket {ticket.Id} escalated — client rated it {score}/100.", ct);
        }

        return await LoadDtoAsync(ticket.Id, ct);
    }

    public async Task<int> AutoCloseUnansweredTicketsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var overdue = await _db.Tickets
            .Where(t => t.Status == TicketStatus.AwaitingClientConfirmation
                        && t.ClientConfirmationDeadline != null
                        && t.ClientConfirmationDeadline <= now)
            .ToListAsync(ct);

        foreach (var ticket in overdue)
        {
            ticket.Status = TicketStatus.Closed;
            ticket.ClosureReason = ClosureReason.AutoClosedNoResponse;
            ticket.ClosedAt = now;
            // No SatisfactionStars/Score recorded — an unanswered ticket
            // does not count toward the employee's CSAT average.
            ticket.AuditTrail.Add(new TicketAuditEntry
            {
                TicketId = ticket.Id,
                Actor = "System",
                Action = $"Auto-closed after {_options.ClientConfirmationWindowDays} days with no client response"
            });
            _db.Update(ticket);

            await _notifications.NotifyAsync(NotificationRecipientType.Client, ticket.ClientId.ToString(), "ticket_autoclosed",
                $"Ticket {ticket.Id} was automatically closed after no response — assumed resolved.", ct);
        }

        if (overdue.Count > 0)
            await _db.SaveChangesAsync(ct);

        return overdue.Count;
    }

    public async Task<IReadOnlyList<TicketDto>> GetAllAsync(CancellationToken ct = default) =>
        await ProjectAsync(_db.Tickets, ct);

    public async Task<TicketDto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await ProjectAsync(_db.Tickets.Where(t => t.Id == id), ct)).FirstOrDefault();

    public async Task<IReadOnlyList<TicketDto>> GetForClientAsync(Guid clientId, CancellationToken ct = default) =>
        await ProjectAsync(_db.Tickets.Where(t => t.ClientId == clientId), ct);

    public async Task<IReadOnlyList<TicketDto>> GetForEmployeeAsync(Guid employeeId, CancellationToken ct = default) =>
        await ProjectAsync(_db.Tickets.Where(t => t.AssignedEmployeeId == employeeId), ct);

    public async Task<IReadOnlyList<TicketDto>> GetAwaitingConfirmationForClientAsync(Guid clientId, CancellationToken ct = default) =>
        await ProjectAsync(_db.Tickets.Where(t => t.ClientId == clientId && t.Status == TicketStatus.AwaitingClientConfirmation), ct);

    public async Task<IReadOnlyList<TicketDto>> GetEscalatedAsync(CancellationToken ct = default) =>
        await ProjectAsync(_db.Tickets.Where(t => t.Status == TicketStatus.Escalated), ct);

    private async Task<TicketDto> LoadDtoAsync(Guid id, CancellationToken ct) =>
        (await ProjectAsync(_db.Tickets.Where(t => t.Id == id), ct)).First();

    private static async Task<IReadOnlyList<TicketDto>> ProjectAsync(IQueryable<Ticket> query, CancellationToken ct)
    {
        var tickets = await query
            .Include(t => t.Client)
            .Include(t => t.AssignedEmployee)
            .Include(t => t.AuditTrail)
            .OrderByDescending(t => t.DateSubmitted)
            .ToListAsync(ct);

        return tickets.Select(t => new TicketDto(
            t.Id, t.ClientId, t.Client.Name, t.AgreementId, t.Description, t.Category, t.DateSubmitted,
            t.ForwardedByEmployeeId, t.AssignedEmployeeId, t.AssignedEmployee?.FullName, t.AssignedAt,
            t.Chargeable, t.Status, t.ResolvedAt, t.ClientConfirmationDeadline,
            t.SatisfactionStars, t.SatisfactionScore, t.ClosureReason,
            t.AuditTrail.OrderBy(a => a.Timestamp).Select(a => new TicketAuditEntryDto(a.Timestamp, a.Actor, a.Action)).ToList()
        )).ToList();
    }
}
