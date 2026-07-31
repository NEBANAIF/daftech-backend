using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.Interfaces;

/// <summary>
/// Assigns tickets automatically. There is no Admin override: as soon as a
/// ticket is forwarded, the employee with the fewest open tickets (among
/// Active Employee/Technician staff) is assigned. If two employees tie on
/// open-ticket count, the one who has gone longest without a new
/// assignment is picked, keeping the rotation fair.
/// </summary>
public interface ITicketAssignmentService
{
    /// <summary>Returns the employee who should receive the next ticket, or null if no eligible employee exists.</summary>
    Task<Employee?> SelectAssigneeAsync(CancellationToken ct = default);
}
