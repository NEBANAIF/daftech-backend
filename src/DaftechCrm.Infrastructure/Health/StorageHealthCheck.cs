using DaftechCrm.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DaftechCrm.Infrastructure.Health;

/// <summary>Writes, reads back and deletes a probe file to prove document storage is usable.</summary>
public class StorageHealthCheck : IHealthCheck
{
    private readonly IFileStorageService _storage;

    public StorageHealthCheck(IFileStorageService storage) => _storage = storage;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await _storage.CheckHealthAsync(cancellationToken)
            ? HealthCheckResult.Healthy("Document storage is writable.")
            : HealthCheckResult.Unhealthy("Document storage is not writable — agreement uploads will fail.");
}
