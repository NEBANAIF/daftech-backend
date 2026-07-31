using DaftechCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace DaftechCrm.Infrastructure.Health;

/// <summary>Verifies the PostgreSQL database is reachable and has no pending migrations.</summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(AppDbContext db, ILogger<DatabaseHealthCheck> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Cannot connect to the PostgreSQL database.");

            var pending = (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            var data = new Dictionary<string, object> { ["pendingMigrations"] = pending.Count };

            return pending.Count == 0
                ? HealthCheckResult.Healthy("Database reachable, schema up to date.", data)
                : HealthCheckResult.Degraded($"{pending.Count} pending migration(s).", data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database health check threw an exception.", ex);
        }
    }
}
