using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DaftechCrm.Infrastructure.Health;

/// <summary>Liveness probe: reports process uptime and memory so the API itself can be monitored.</summary>
public class ApiHealthCheck : IHealthCheck
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var process = Process.GetCurrentProcess();
        var data = new Dictionary<string, object>
        {
            ["startedAtUtc"] = StartedAt,
            ["uptimeSeconds"] = (long)(DateTimeOffset.UtcNow - StartedAt).TotalSeconds,
            ["workingSetMb"] = process.WorkingSet64 / (1024 * 1024),
            ["threadCount"] = process.Threads.Count,
        };

        return Task.FromResult(HealthCheckResult.Healthy("API process is running.", data));
    }
}
