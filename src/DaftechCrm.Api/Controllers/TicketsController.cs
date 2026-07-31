using DaftechCrm.Api.Extensions;
using DaftechCrm.Api.Services;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize(Policy = AuthorizationPolicyNames.AnyAccount)]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _tickets;

    public TicketsController(ITicketService tickets) => _tickets = tickets;

    /// <summary>Full ticket list — staff only; a client has no legitimate use for every client's tickets.</summary>
    [Authorize(Policy = AuthorizationPolicyNames.AnyEmployee)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetAll(CancellationToken ct) =>
        Ok(await _tickets.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDto>> GetById(Guid id, CancellationToken ct)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        if (ticket is null) return NotFound();

        // Staff can view any ticket; a client may only view their own.
        if (this.GetAccountType() == SessionAccountType.Client && ticket.ClientId != this.GetAccountId())
            return Forbid();

        return Ok(ticket);
    }

    /// <summary>A client may only ever request their own ticket history — the id in the route is ignored in favor of the token's account id.</summary>
    [Authorize(Policy = AuthorizationPolicyNames.ClientOnly)]
    [HttpGet("client/{clientId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetForClient(Guid clientId, CancellationToken ct)
    {
        var callerId = this.GetAccountId();
        if (callerId is null || callerId != clientId) return Forbid();
        return Ok(await _tickets.GetForClientAsync(clientId, ct));
    }

    /// <summary>An employee may only request their own assigned tickets this way; Admin/IT Support use the full list instead.</summary>
    [Authorize(Policy = AuthorizationPolicyNames.AnyEmployee)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetForEmployee(Guid employeeId, CancellationToken ct)
    {
        var callerId = this.GetAccountId();
        if (callerId != employeeId && !this.IsInRole(EmployeeRole.Admin) && !this.IsInRole(EmployeeRole.ItSupport))
            return Forbid();
        return Ok(await _tickets.GetForEmployeeAsync(employeeId, ct));
    }

    [Authorize(Policy = AuthorizationPolicyNames.ClientOnly)]
    [HttpGet("client/{clientId:guid}/awaiting-confirmation")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetAwaitingConfirmation(Guid clientId, CancellationToken ct)
    {
        var callerId = this.GetAccountId();
        if (callerId is null || callerId != clientId) return Forbid();
        return Ok(await _tickets.GetAwaitingConfirmationForClientAsync(clientId, ct));
    }

    /// <summary>Admin review queue for tickets the client rated below the satisfaction threshold.</summary>
    [Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
    [HttpGet("escalated")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetEscalated(CancellationToken ct) =>
        Ok(await _tickets.GetEscalatedAsync(ct));

    /// <summary>Client submits a new ticket for themselves.</summary>
    [Authorize(Policy = AuthorizationPolicyNames.ClientOnly)]
    [HttpPost]
    public async Task<ActionResult<TicketDto>> Submit([FromBody] SubmitTicketRequest request, CancellationToken ct)
    {
        var ticket = await _tickets.SubmitFromClientAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    /// <summary>IT Support forwards the ticket — this triggers automatic assignment; there is no Admin "assign" endpoint.</summary>
    [Authorize(Policy = AuthorizationPolicyNames.SupportStaff)]
    [HttpPost("{id:guid}/forward")]
    public async Task<ActionResult<TicketDto>> Forward(Guid id, [FromBody] ForwardTicketRequest request, CancellationToken ct)
    {
        try { return Ok(await _tickets.ForwardAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Employee updates status. Setting Resolved does not close the ticket —
    /// it starts the client confirmation window (see /confirm below).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicyNames.AnyEmployee)]
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TicketDto>> UpdateStatus(Guid id, [FromBody] UpdateTicketStatusRequest request, CancellationToken ct)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        if (ticket is null) return NotFound();

        // Only the assigned technician (or Admin/IT Support) may update status.
        var callerId = this.GetAccountId();
        if (ticket.AssignedEmployeeId != callerId && !this.IsInRole(EmployeeRole.Admin) && !this.IsInRole(EmployeeRole.ItSupport))
            return Forbid();

        try { return Ok(await _tickets.UpdateStatusAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Client confirms the fix and rates 1-5 stars. Score = stars * 20;
    /// &gt;= 90 closes the ticket, below that escalates it to Admin.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicyNames.ClientOnly)]
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<TicketDto>> Confirm(Guid id, [FromBody] ClientConfirmationRequest request, CancellationToken ct)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        if (ticket is null) return NotFound();
        if (ticket.ClientId != this.GetAccountId()) return Forbid();

        try { return Ok(await _tickets.ConfirmResolutionAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(ex.Message); }
    }
}
