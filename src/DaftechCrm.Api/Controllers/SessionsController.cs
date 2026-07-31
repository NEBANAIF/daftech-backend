using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Controllers;

public record TouchSessionRequest(SessionAccountType AccountType, Guid AccountId);
public record CloseSessionRequest(SessionAccountType AccountType, Guid AccountId);

[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;
    public SessionsController(ISessionService sessions) => _sessions = sessions;

    /// <summary>Admin's Session Activity page — current online/offline status, last-seen, and most recent IP per account.</summary>
    [HttpGet("activity")]
    public async Task<ActionResult<IReadOnlyList<SessionActivityDto>>> GetActivity(CancellationToken ct) =>
        Ok(await _sessions.GetSessionActivityAsync(ct));

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<LoginSessionDto>>> GetHistory(
        [FromQuery] SessionAccountType accountType, [FromQuery] Guid accountId, CancellationToken ct) =>
        Ok(await _sessions.GetHistoryForAccountAsync(accountType, accountId, ct));

    /// <summary>Heartbeat — the frontend calls this periodically while the tab is active to keep OnlineStatus true and LastSeen current.</summary>
    [HttpPost("touch")]
    public async Task<IActionResult> Touch([FromBody] TouchSessionRequest request, CancellationToken ct)
    {
        await _sessions.TouchAsync(request.AccountType, request.AccountId, ct);
        return NoContent();
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CloseSessionRequest request, CancellationToken ct)
    {
        await _sessions.CloseSessionAsync(request.AccountType, request.AccountId, ct);
        return NoContent();
    }
}
