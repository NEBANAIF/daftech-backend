using DaftechCrm.Application.Interfaces;

namespace DaftechCrm.Api.BackgroundServices;

/// <summary>
/// Sweeps for tickets whose ClientConfirmationDeadline has passed with no
/// client response and auto-closes them (see TicketService.AutoCloseUnansweredTicketsAsync).
/// Runs every 15 minutes — frequent enough that a 5-day deadline never
/// slips by more than a few minutes, without hammering the database.
/// </summary>
public class AutoCloseTicketsHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<AutoCloseTicketsHostedService> _logger;

    public AutoCloseTicketsHostedService(IServiceProvider services, ILogger<AutoCloseTicketsHostedService> logger)
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
                var ticketService = scope.ServiceProvider.GetRequiredService<ITicketService>();
                var closedCount = await ticketService.AutoCloseUnansweredTicketsAsync(stoppingToken);
                if (closedCount > 0)
                    _logger.LogInformation("Auto-closed {Count} ticket(s) with no client response.", closedCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error while auto-closing unanswered tickets.");
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
