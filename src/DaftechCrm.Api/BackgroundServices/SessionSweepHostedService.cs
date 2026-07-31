using DaftechCrm.Application.Interfaces;

namespace DaftechCrm.Api.BackgroundServices;

/// <summary>
/// Sweeps for sessions with no heartbeat within the configured window
/// (SessionOptions.OfflineAfterMinutes) and marks them offline. Runs
/// every 2 minutes.
/// </summary>
public class SessionSweepHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    private readonly IServiceProvider _services;
    private readonly ILogger<SessionSweepHostedService> _logger;

    public SessionSweepHostedService(IServiceProvider services, ILogger<SessionSweepHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
                var count = await sessionService.MarkStaleSessionsOfflineAsync(stoppingToken);
                if (count > 0)
                    _logger.LogInformation("Marked {Count} stale session(s) offline.", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error while sweeping stale sessions.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }
}
