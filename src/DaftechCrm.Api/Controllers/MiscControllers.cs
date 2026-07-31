using DaftechCrm.Api.Extensions;
using DaftechCrm.Api.Services;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Controllers;

/// <summary>
/// Client account management (signup/approve/reject) is Admin-facing for the
/// mutating actions; a client may read their own profile but not the roster.
/// </summary>
[ApiController]
[Route("api/clients")]
[Authorize(Policy = AuthorizationPolicyNames.AnyAccount)]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clients;
    public ClientsController(IClientService clients) => _clients = clients;

    /// <summary>Full client roster — staff only.</summary>
    [Authorize(Policy = AuthorizationPolicyNames.AnyEmployee)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> GetAll(CancellationToken ct) => Ok(await _clients.GetAllAsync(ct));

    /// <summary>Staff can view any client; a client may only view their own profile.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientDto>> GetById(Guid id, CancellationToken ct)
    {
        if (this.GetAccountType() == SessionAccountType.Client && this.GetAccountId() != id) return Forbid();
        var c = await _clients.GetByIdAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [Authorize(Policy = AuthorizationPolicyNames.SupportStaff)]
    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> GetPending(CancellationToken ct) => Ok(await _clients.GetPendingAsync(ct));

    /// <summary>Public — a prospective client has no account yet to authenticate with.</summary>
    [AllowAnonymous]
    [HttpPost("signup")]
    public async Task<ActionResult<ClientDto>> Signup([FromBody] CreateClientSignupRequest request, CancellationToken ct)
    {
        var c = await _clients.SubmitSignupAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = c.Id }, c);
    }

    /// <summary>
    /// Admin registers a client directly — Approved and credentialed
    /// immediately, no separate approval step needed. The response's
    /// OneTimePassword is shown only once.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
    [HttpPost("register")]
    public async Task<ActionResult<ClientRegisteredResult>> Register([FromBody] RegisterClientRequest request, CancellationToken ct)
    {
        var result = await _clients.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Client.Id }, result);
    }

    [Authorize(Policy = AuthorizationPolicyNames.SupportStaff)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ClientDto>> Approve(Guid id, CancellationToken ct) => Ok(await _clients.ApproveAsync(id, ct));

    [Authorize(Policy = AuthorizationPolicyNames.SupportStaff)]
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ClientDto>> Reject(Guid id, [FromBody] RejectClientRequest request, CancellationToken ct) =>
        Ok(await _clients.RejectAsync(id, request, ct));

    /// <summary>Retries sending the credential email with a freshly regenerated one-time password (SRS v2.0 §4.3.1).</summary>
    [Authorize(Policy = AuthorizationPolicyNames.SupportStaff)]
    [HttpPost("{id:guid}/resend-credential-email")]
    public async Task<ActionResult<ResendClientCredentialEmailResult>> ResendCredentialEmail(Guid id, CancellationToken ct)
    {
        try { return Ok(await _clients.ResendCredentialEmailAsync(id, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}

[ApiController]
[Route("api/maintenance")]
[Authorize(Policy = AuthorizationPolicyNames.AnyEmployee)]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenance;
    public MaintenanceController(IMaintenanceService maintenance) => _maintenance = maintenance;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MaintenanceRecordDto>>> GetAll(CancellationToken ct) => Ok(await _maintenance.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<MaintenanceRecordDto>> Create([FromBody] CreateMaintenanceRecordRequest request, CancellationToken ct)
    {
        var r = await _maintenance.CreateAsync(request, ct);
        return Created($"/api/maintenance/{r.Id}", r);
    }
}

/// <summary>Clock-in/out is self-service: an employee may only punch their own clock, never another's.</summary>
[ApiController]
[Route("api/time-logs")]
[Authorize(Policy = AuthorizationPolicyNames.AnyEmployee)]
public class TimeLogsController : ControllerBase
{
    private readonly ITimeLogService _timeLogs;
    public TimeLogsController(ITimeLogService timeLogs) => _timeLogs = timeLogs;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TimeLogDto>>> GetAll([FromQuery] Guid? employeeId, CancellationToken ct)
    {
        // Non-admins can only pull their own logs (or the unfiltered "all" view is Admin-only).
        if (!this.IsInRole(EmployeeRole.Admin))
        {
            if (employeeId is null || employeeId != this.GetAccountId()) return Forbid();
        }
        return Ok(await _timeLogs.GetAllAsync(employeeId, ct));
    }

    [HttpPost("{employeeId:guid}/clock-in")]
    public async Task<IActionResult> ClockIn(Guid employeeId, CancellationToken ct)
    {
        if (this.GetAccountId() != employeeId) return Forbid();
        await _timeLogs.ClockInAsync(employeeId, ct);
        return NoContent();
    }

    [HttpPost("{employeeId:guid}/clock-out")]
    public async Task<IActionResult> ClockOut(Guid employeeId, CancellationToken ct)
    {
        if (this.GetAccountId() != employeeId) return Forbid();
        await _timeLogs.ClockOutAsync(employeeId, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/notifications")]
[Authorize(Policy = AuthorizationPolicyNames.AnyAccount)]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    /// <summary>
    /// Admin/ItSupport recipient types are role broadcasts — any staff member
    /// in that role may read them. Employee/Client types are scoped to a
    /// specific account's own id.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetForRecipient(
        [FromQuery] NotificationRecipientType recipientType, [FromQuery] string recipientId, CancellationToken ct)
    {
        if (!IsAllowedRecipient(recipientType, recipientId)) return Forbid();
        return Ok(await _notifications.GetForRecipientAsync(recipientType, recipientId, ct));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _notifications.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead([FromQuery] NotificationRecipientType recipientType, [FromQuery] string recipientId, CancellationToken ct)
    {
        if (!IsAllowedRecipient(recipientType, recipientId)) return Forbid();
        await _notifications.MarkAllReadAsync(recipientType, recipientId, ct);
        return NoContent();
    }

    private bool IsAllowedRecipient(NotificationRecipientType recipientType, string recipientId) => recipientType switch
    {
        NotificationRecipientType.Admin => this.IsInRole(EmployeeRole.Admin),
        NotificationRecipientType.ItSupport => this.IsInRole(EmployeeRole.ItSupport) || this.IsInRole(EmployeeRole.Admin),
        NotificationRecipientType.Employee => this.GetAccountType() == SessionAccountType.Employee && this.GetAccountId()?.ToString() == recipientId,
        NotificationRecipientType.Client => this.GetAccountType() == SessionAccountType.Client && this.GetAccountId()?.ToString() == recipientId,
        _ => false,
    };
}

/// <summary>Performance/resolution reporting — Admin only; this is staff-evaluation data.</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    public ReportsController(IReportService reports) => _reports = reports;

    /// <summary>Bar chart (per-employee) and donut chart (overall) data for on-time vs late ticket resolution.</summary>
    [HttpGet("on-time-resolution")]
    public async Task<ActionResult<OnTimeReportDto>> GetOnTimeResolution(CancellationToken ct) =>
        Ok(await _reports.GetOnTimeResolutionReportAsync(ct));

    /// <summary>
    /// Written/graphical performance metrics for one employee. Pass
    /// includeAiNarrative=true to also request the optional AI summary —
    /// omit or set false to skip the AI call entirely (e.g. for a fast
    /// numbers-only view).
    /// </summary>
    [HttpGet("employee-performance/{employeeId:guid}")]
    public async Task<ActionResult<EmployeePerformanceReportDto>> GetEmployeePerformance(
        Guid employeeId, [FromQuery] bool includeAiNarrative, CancellationToken ct)
    {
        try { return Ok(await _reports.GetEmployeePerformanceReportAsync(employeeId, includeAiNarrative, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }
}

[ApiController]
[Route("api/satisfaction-surveys")]
[Authorize(Policy = AuthorizationPolicyNames.AnyAccount)]
public class SatisfactionSurveysController : ControllerBase
{
    private readonly ISatisfactionSurveyService _surveys;
    public SatisfactionSurveysController(ISatisfactionSurveyService surveys) => _surveys = surveys;

    /// <summary>All surveys — staff only (aggregate satisfaction data across all clients).</summary>
    [Authorize(Policy = AuthorizationPolicyNames.AnyEmployee)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SatisfactionSurveyDto>>> GetAll(CancellationToken ct) => Ok(await _surveys.GetAllAsync(ct));

    /// <summary>Staff can view any ticket's survey; a client only one belonging to them.</summary>
    [HttpGet("ticket/{ticketId:guid}")]
    public async Task<ActionResult<SatisfactionSurveyDto>> GetForTicket(Guid ticketId, CancellationToken ct)
    {
        var survey = await _surveys.GetForTicketAsync(ticketId, ct);
        if (survey is null) return NotFound();
        if (this.GetAccountType() == SessionAccountType.Client && survey.ClientId != this.GetAccountId()) return Forbid();
        return Ok(survey);
    }

    /// <summary>A client submits a survey for their own ticket — the service layer should independently verify ticket ownership from the request.</summary>
    [Authorize(Policy = AuthorizationPolicyNames.ClientOnly)]
    [HttpPost]
    public async Task<ActionResult<SatisfactionSurveyDto>> Submit([FromBody] SubmitSatisfactionSurveyRequest request, CancellationToken ct)
    {
        try { return Ok(await _surveys.SubmitAsync(request, ct)); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(ex.Message); }
    }
}
