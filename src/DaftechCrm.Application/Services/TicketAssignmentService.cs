using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class TicketAssignmentService : ITicketAssignmentService
{
    private static readonly TicketStatus[] OpenStatuses =
    {
        TicketStatus.Assigned, TicketStatus.InProgress
    };

    private readonly IAppDbContext _db;

    public TicketAssignmentService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Employee?> SelectAssigneeAsync(CancellationToken ct = default)
    {
        // Roles is stored via a value converter (MySQL has no array type), so
        // .Contains() on it can't translate to SQL — filter AccountStatus in
        // the query, then filter by role in memory after materializing.
        var activeEmployees = await _db.Employees
            .Where(e => e.AccountStatus == EmployeeAccountStatus.Active)
            .ToListAsync(ct);

        var candidates = activeEmployees.Where(e => e.Roles.Contains(EmployeeRole.EmployeeTechnician)).ToList();
        if (candidates.Count == 0) return null;

        var openCounts = await _db.Tickets
            .Where(t => t.AssignedEmployeeId != null && OpenStatuses.Contains(t.Status))
            .GroupBy(t => t.AssignedEmployeeId!.Value)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Count, ct);

        var lastAssigned = await _db.Tickets
            .Where(t => t.AssignedEmployeeId != null)
            .GroupBy(t => t.AssignedEmployeeId!.Value)
            .Select(g => new { EmployeeId = g.Key, LastAssignedAt = g.Max(t => t.DateSubmitted) })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.LastAssignedAt, ct);

        return candidates
            .OrderBy(e => openCounts.GetValueOrDefault(e.Id, 0))
            .ThenBy(e => lastAssigned.GetValueOrDefault(e.Id, DateTimeOffset.MinValue))
            .First();
    }
}
