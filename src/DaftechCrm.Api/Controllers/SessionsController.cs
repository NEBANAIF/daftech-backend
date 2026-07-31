using DaftechCrm.Api.Extensions;
using DaftechCrm.Api.Services;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Controllers;

public record TouchSessionRequest(SessionAccountType AccountType, Guid AccountId);
public record CloseSessionRequest(SessionAccountType AccountType, Guid AccountId);

[ApiController]
[Route("api/sessions")]
[Authorize(Policy = AuthorizationPolicyNames.AnyAccount)]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;
    public SessionsController(ISessionService sessions) => _sessions = sessions;

    /// <summary>Admin's Session Activity page — current online/offline status, last-seen, and most recent IP per account.</summary>
    [Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
    [HttpGet("activity")]
    public async Task<ActionResult<IReadOnlyList<SessionActivityDto>>> GetActivity(CancellationToken ct) =>
        Ok(await _sessions.GetSessionActivityAsync(ct));

    /// <summary>Login history for one account — Admin can view any account; everyone else only their own.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<LoginSessionDto>>> GetHistory(
        [FromQuery] SessionAccountType accountType, [FromQuery] Guid accountId, CancellationToken ct)
    {
        if (!this.IsInRole(EmployeeRole.Admin) && (this.GetAccountType() != accountType || this.GetAccountId() != accountId))
            return Forbid();
        return Ok(await _sessions.GetHistoryForAccountAsync(accountType, accountId, ct));
    }

    /// <summary>Heartbeat — the frontend calls this periodically while the tab is active. Only for the caller's own session.</summary>
    [HttpPost("touch")]
    public async Task<IActionResult> Touch([FromBody] TouchSessionRequest request, CancellationToken ct)
    {
        if (this.GetAccountType() != request.AccountType || this.GetAccountId() != request.AccountId) return Forbid();
        await _sessions.TouchAsync(request.AccountType, request.AccountId, ct);
        return NoContent();
    }

    /// <summary>Only for the caller's own session — for logging out someone else's session, disable/revoke the account instead.</summary>
    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CloseSessionRequest request, CancellationToken ct)
    {
        if (this.GetAccountType() != request.AccountType || this.GetAccountId() != request.AccountId) return Forbid();
        await _sessions.CloseSessionAsync(request.AccountType, request.AccountId, ct);
        return NoContent();
    }
}
