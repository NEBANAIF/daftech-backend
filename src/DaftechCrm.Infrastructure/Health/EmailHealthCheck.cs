using DaftechCrm.Application.Interfaces;
using DaftechCrm.Infrastructure.Email;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DaftechCrm.Infrastructure.Health;

/// <summary>Opens (and immediately closes) an SMTP connection to confirm mail delivery is possible.</summary>
public class EmailHealthCheck : IHealthCheck
{
    private readonly IEmailSender _emailSender;

    public EmailHealthCheck(IEmailSender emailSender) => _emailSender = emailSender;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_emailSender is not MailKitEmailSender mailKit)
            return HealthCheckResult.Healthy("Email sender does not expose a health probe.");

        // Email is non-critical for serving requests: report Degraded, not Unhealthy.
        return await mailKit.CheckHealthAsync(cancellationToken)
            ? HealthCheckResult.Healthy("SMTP endpoint reachable.")
            : HealthCheckResult.Degraded("SMTP endpoint unreachable — credential emails will be queued for manual retry.");
    }
}
