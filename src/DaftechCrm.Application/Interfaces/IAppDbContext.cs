using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.Interfaces;

/// <summary>
/// Minimal repository/unit-of-work seam so Application services depend on
/// abstractions, not EF Core directly (Clean Architecture / DDD, matching
/// the pattern used on the Trade License Workflow project).
/// </summary>
public interface IAppDbContext
{
    IQueryable<Client> Clients { get; }
    IQueryable<Agreement> Agreements { get; }
    IQueryable<Ticket> Tickets { get; }
    IQueryable<TicketAuditEntry> TicketAuditEntries { get; }
    IQueryable<Employee> Employees { get; }
    IQueryable<DeviceSession> DeviceSessions { get; }
    IQueryable<LoginRecord> LoginRecords { get; }
    IQueryable<TimeLog> TimeLogs { get; }
    IQueryable<MaintenanceRecord> MaintenanceRecords { get; }
    IQueryable<AppNotification> Notifications { get; }
    IQueryable<SatisfactionSurvey> SatisfactionSurveys { get; }
    IQueryable<LoginSession> LoginSessions { get; }

    void Add<TEntity>(TEntity entity) where TEntity : class;
    void Update<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
