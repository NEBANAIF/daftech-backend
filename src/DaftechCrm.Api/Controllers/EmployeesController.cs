using DaftechCrm.Api.Middleware;
using Microsoft.AspNetCore.RateLimiting;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Controllers;

[ApiController]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employees;
    public EmployeesController(IEmployeeService employees) => _employees = employees;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetAll(CancellationToken ct) => Ok(await _employees.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken ct)
    {
        var e = await _employees.GetByIdAsync(id, ct);
        return e is null ? NotFound() : Ok(e);
    }

    /// <summary>
    /// Admin registers a new staff account. The response includes the
    /// system-generated username and a one-time password — this is the
    /// ONLY time the plaintext one-time password is ever available. The
    /// Admin must relay it to the employee immediately; it cannot be
    /// retrieved again afterward.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<EmployeeRegisteredResult>> Register([FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var result = await _employees.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Employee.Id }, result);
    }

    /// <summary>Disables the account (offboarding) — revokes all device sessions and blocks future logins immediately.</summary>
    [HttpPost("{id:guid}/disable")]
    public async Task<ActionResult<EmployeeDto>> Disable(Guid id, [FromBody] DisableEmployeeRequest request, CancellationToken ct)
    {
        try { return Ok(await _employees.DisableAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("{id:guid}/enable")]
    public async Task<ActionResult<EmployeeDto>> Enable(Guid id, CancellationToken ct)
    {
        try { return Ok(await _employees.EnableAsync(id, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("{id:guid}/allowed-ips")]
    public async Task<ActionResult<EmployeeDto>> AddAllowedIp(Guid id, [FromBody] AddAllowedIpRequest request, CancellationToken ct) =>
        Ok(await _employees.AddAllowedIpAsync(id, request, ct));

    [HttpDelete("{id:guid}/allowed-ips/{ip}")]
    public async Task<ActionResult<EmployeeDto>> RemoveAllowedIp(Guid id, string ip, CancellationToken ct) =>
        Ok(await _employees.RemoveAllowedIpAsync(id, ip, ct));

    [HttpGet("{id:guid}/devices")]
    public async Task<ActionResult<IReadOnlyList<DeviceSessionDto>>> GetDevices(Guid id, CancellationToken ct) =>
        Ok(await _employees.GetDevicesAsync(id, ct));

    [HttpPost("devices/{deviceSessionId:guid}/revoke")]
    public async Task<IActionResult> RevokeDevice(Guid deviceSessionId, CancellationToken ct)
    {
        await _employees.RevokeDeviceAsync(deviceSessionId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/login-history")]
    public async Task<ActionResult<IReadOnlyList<LoginRecordDto>>> GetLoginHistory(Guid id, CancellationToken ct) =>
        Ok(await _employees.GetLoginHistoryAsync(id, ct));

    /// <summary>Retries sending the credential email with a freshly regenerated one-time password (SRS v2.0 §4.3.1).</summary>
    [HttpPost("{id:guid}/resend-credential-email")]
    public async Task<ActionResult<ResendCredentialEmailResult>> ResendCredentialEmail(Guid id, CancellationToken ct)
    {
        try { return Ok(await _employees.ResendCredentialEmailAsync(id, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }
}

[ApiController]
[Route("api/auth")]
// Stricter rate limit: 10 attempts per minute per IP, to blunt brute-force logins.
[EnableRateLimiting(RateLimitingMiddleware.AuthPolicy)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>
    /// <summary>
    /// Employee login. The server resolves the caller's IP address itself
    /// (see HttpCurrentRequestContext) — it is not supplied by the client —
    /// and records it on every attempt, successful or blocked. The response's
    /// MustChangePassword flag tells the frontend to route straight to the
    /// change-password screen before anything else.
    /// </summary>
    [HttpPost("employee-login")]
    public async Task<ActionResult<EmployeeLoginResult>> LoginEmployee([FromBody] EmployeeLoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginEmployeeAsync(request, ct));

    [HttpPost("employee/{employeeId:guid}/change-password")]
    public async Task<IActionResult> ChangeEmployeePassword(Guid employeeId, [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        try
        {
            await _auth.ChangeEmployeePasswordAsync(employeeId, request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("client-login")]
    public async Task<ActionResult<ClientLoginResult>> LoginClient([FromBody] ClientLoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginClientAsync(request, ct));

    [HttpPost("client/{clientId:guid}/change-password")]
    public async Task<IActionResult> ChangeClientPassword(Guid clientId, [FromBody] ClientChangePasswordRequest request, CancellationToken ct)
    {
        try
        {
            await _auth.ChangeClientPasswordAsync(clientId, request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
