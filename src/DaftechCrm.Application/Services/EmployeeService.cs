using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class EmployeeService : IEmployeeService
{
    private static readonly TicketStatus[] OpenStatuses = { TicketStatus.Assigned, TicketStatus.InProgress };

    private readonly IAppDbContext _db;
    private readonly AccountCredentialService _credentials;

    public EmployeeService(IAppDbContext db, AccountCredentialService credentials)
    {
        _db = db;
        _credentials = credentials;
    }

    public async Task<EmployeeRegisteredResult> RegisterAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        var issued = await _credentials.IssueForNameAsync(request.FullName, ct);

        var employee = new Employee
        {
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Specialization = request.Specialization,
            Roles = request.Roles.ToList(),
            AllowedIpAddresses = request.AllowedIpAddresses.ToList(),
            Username = issued.Username,
            PasswordHash = PasswordHasher.Hash(issued.OneTimePassword),
            MustChangePassword = true,
        };
        _db.Add(employee);
        await _db.SaveChangesAsync(ct);

        var (sent, error) = await _credentials.SendCredentialEmailAsync(
            employee.Email, employee.FullName, issued.Username, issued.OneTimePassword, ct);

        var dto = await ToDto(employee, ct);
        return new EmployeeRegisteredResult(dto, issued.Username, issued.OneTimePassword, sent, error);
    }

    /// <summary>Admin retry — SRS v2.0 §4.3.1: if the original credential email failed, generate a fresh OTP and resend (the old OTP is invalidated, since the plaintext was never persisted).</summary>
    public async Task<ResendCredentialEmailResult> ResendCredentialEmailAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        var newOneTimePassword = await _credentials.RegenerateOneTimePasswordAsync(ct);
        employee.PasswordHash = PasswordHasher.Hash(newOneTimePassword);
        employee.MustChangePassword = true;
        _db.Update(employee);
        await _db.SaveChangesAsync(ct);

        var (sent, error) = await _credentials.SendCredentialEmailAsync(
            employee.Email, employee.FullName, employee.Username, newOneTimePassword, ct);
        return new ResendCredentialEmailResult(sent, error);
    }

    public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var employees = await _db.Employees.ToListAsync(ct);
        var result = new List<EmployeeDto>();
        foreach (var e in employees) result.Add(await ToDto(e, ct));
        return result;
    }

    public async Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        return employee is null ? null : await ToDto(employee, ct);
    }

    public async Task<EmployeeDto> DisableAsync(Guid employeeId, DisableEmployeeRequest request, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        employee.AccountStatus = EmployeeAccountStatus.Disabled;
        employee.DisabledAt = DateTimeOffset.UtcNow;
        employee.DisabledReason = request.Reason;
        _db.Update(employee);

        // Revoke every active device session immediately — offboarding cuts access now, not on next login.
        var sessions = await _db.DeviceSessions.Where(d => d.EmployeeId == employeeId && d.AccessStatus == DeviceAccessStatus.Allowed).ToListAsync(ct);
        foreach (var s in sessions)
        {
            s.AccessStatus = DeviceAccessStatus.Revoked;
            _db.Update(s);
        }

        await _db.SaveChangesAsync(ct);
        return await ToDto(employee, ct);
    }

    public async Task<EmployeeDto> EnableAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        employee.AccountStatus = EmployeeAccountStatus.Active;
        employee.DisabledAt = null;
        employee.DisabledReason = null;
        _db.Update(employee);
        await _db.SaveChangesAsync(ct);
        return await ToDto(employee, ct);
    }

    public async Task<EmployeeDto> AddAllowedIpAsync(Guid employeeId, AddAllowedIpRequest request, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        if (!employee.AllowedIpAddresses.Contains(request.IpAddress))
            employee.AllowedIpAddresses.Add(request.IpAddress);

        _db.Update(employee);
        await _db.SaveChangesAsync(ct);
        return await ToDto(employee, ct);
    }

    public async Task<EmployeeDto> RemoveAllowedIpAsync(Guid employeeId, string ip, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        employee.AllowedIpAddresses.Remove(ip);
        _db.Update(employee);
        await _db.SaveChangesAsync(ct);
        return await ToDto(employee, ct);
    }

    public async Task<IReadOnlyList<DeviceSessionDto>> GetDevicesAsync(Guid employeeId, CancellationToken ct = default) =>
        await _db.DeviceSessions.Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.LastSeen)
            .Select(d => new DeviceSessionDto(d.Id, d.DeviceType, d.DeviceIdentifier, d.IpAddress, d.LastSeen, d.AccessStatus))
            .ToListAsync(ct);

    public async Task RevokeDeviceAsync(Guid deviceSessionId, CancellationToken ct = default)
    {
        var session = await _db.DeviceSessions.FirstOrDefaultAsync(d => d.Id == deviceSessionId, ct)
            ?? throw new InvalidOperationException("Device session not found.");
        session.AccessStatus = DeviceAccessStatus.Revoked;
        _db.Update(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LoginRecordDto>> GetLoginHistoryAsync(Guid employeeId, CancellationToken ct = default) =>
        await _db.LoginRecords.Where(l => l.EmployeeId == employeeId)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new LoginRecordDto(l.Id, l.Timestamp, l.IpAddress, l.DeviceType, l.DeviceIdentifier, l.Allowed, l.Reason))
            .ToListAsync(ct);

    private async Task<EmployeeDto> ToDto(Employee e, CancellationToken ct)
    {
        var openCount = await _db.Tickets.CountAsync(t => t.AssignedEmployeeId == e.Id && OpenStatuses.Contains(t.Status), ct);

        // Average satisfaction score across the employee's tickets that were
        // actually rated (auto-closes with no rating are excluded).
        var scores = await _db.Tickets
            .Where(t => t.AssignedEmployeeId == e.Id && t.SatisfactionScore != null)
            .Select(t => t.SatisfactionScore!.Value)
            .ToListAsync(ct);
        double? avgScore = scores.Count > 0 ? scores.Average() : null;

        return new EmployeeDto(
            e.Id, e.FullName, e.Email, e.PhoneNumber, e.Specialization, e.Roles, e.AccountStatus, e.AllowedIpAddresses,
            e.DisabledAt, e.DisabledReason, openCount, avgScore, e.Username, e.MustChangePassword
        );
    }
}
