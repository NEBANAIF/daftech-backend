using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _tickets;

    public TicketsController(ITicketService tickets) => _tickets = tickets;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetAll(CancellationToken ct) =>
        Ok(await _tickets.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDto>> GetById(Guid id, CancellationToken ct)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpGet("client/{clientId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetForClient(Guid clientId, CancellationToken ct) =>
        Ok(await _tickets.GetForClientAsync(clientId, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetForEmployee(Guid employeeId, CancellationToken ct) =>
        Ok(await _tickets.GetForEmployeeAsync(employeeId, ct));

    [HttpGet("client/{clientId:guid}/awaiting-confirmation")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetAwaitingConfirmation(Guid clientId, CancellationToken ct) =>
        Ok(await _tickets.GetAwaitingConfirmationForClientAsync(clientId, ct));

    /// <summary>Admin review queue for tickets the client rated below the satisfaction threshold.</summary>
    [HttpGet("escalated")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetEscalated(CancellationToken ct) =>
        Ok(await _tickets.GetEscalatedAsync(ct));

    [HttpPost]
    public async Task<ActionResult<TicketDto>> Submit([FromBody] SubmitTicketRequest request, CancellationToken ct)
    {
        var ticket = await _tickets.SubmitFromClientAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    /// <summary>IT Support forwards the ticket — this triggers automatic assignment; there is no Admin "assign" endpoint.</summary>
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
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TicketDto>> UpdateStatus(Guid id, [FromBody] UpdateTicketStatusRequest request, CancellationToken ct)
    {
        try { return Ok(await _tickets.UpdateStatusAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Client confirms the fix and rates 1-5 stars. Score = stars * 20;
    /// &gt;= 90 closes the ticket, below that escalates it to Admin.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<TicketDto>> Confirm(Guid id, [FromBody] ClientConfirmationRequest request, CancellationToken ct)
    {
        try { return Ok(await _tickets.ConfirmResolutionAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(ex.Message); }
    }
}
