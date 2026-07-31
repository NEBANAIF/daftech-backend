using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IAppDbContext _db;
    public NotificationService(IAppDbContext db) => _db = db;

    public async Task NotifyAsync(NotificationRecipientType recipientType, string recipientId, string eventType, string message, CancellationToken ct = default)
    {
        _db.Add(new AppNotification
        {
            RecipientType = recipientType,
            RecipientId = recipientId,
            EventType = eventType,
            Message = message,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetForRecipientAsync(NotificationRecipientType recipientType, string recipientId, CancellationToken ct = default) =>
        await _db.Notifications
            .Where(n => n.RecipientType == recipientType && n.RecipientId == recipientId)
            .OrderByDescending(n => n.DateSent)
            .Select(n => new NotificationDto(n.Id, n.RecipientType, n.RecipientId, n.EventType, n.Message, n.DateSent, n.ReadStatus))
            .ToListAsync(ct);

    public async Task MarkReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId, ct);
        if (n is null) return;
        n.ReadStatus = true;
        _db.Update(n);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(NotificationRecipientType recipientType, string recipientId, CancellationToken ct = default)
    {
        var items = await _db.Notifications.Where(n => n.RecipientType == recipientType && n.RecipientId == recipientId && !n.ReadStatus).ToListAsync(ct);
        foreach (var n in items) { n.ReadStatus = true; _db.Update(n); }
        if (items.Count > 0) await _db.SaveChangesAsync(ct);
    }
}

public class MaintenanceService : IMaintenanceService
{
    private readonly IAppDbContext _db;
    public MaintenanceService(IAppDbContext db) => _db = db;

    public async Task<MaintenanceRecordDto> CreateAsync(CreateMaintenanceRecordRequest request, CancellationToken ct = default)
    {
        var record = new MaintenanceRecord
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Category = request.Category,
            Description = request.Description,
            PerformedByEmployeeId = request.PerformedByEmployeeId,
            Status = request.Status,
            Remarks = request.Remarks,
        };
        _db.Add(record);
        await _db.SaveChangesAsync(ct);
        return ToDto(record);
    }

    public async Task<IReadOnlyList<MaintenanceRecordDto>> GetAllAsync(CancellationToken ct = default) =>
        (await _db.MaintenanceRecords.OrderByDescending(r => r.Date).ToListAsync(ct)).Select(ToDto).ToList();

    private static MaintenanceRecordDto ToDto(MaintenanceRecord r) => new(
        r.Id, r.Date, r.Category, r.Description, r.PerformedByEmployeeId, r.Status, r.Remarks
    );
}

public class TimeLogService : ITimeLogService
{
    private readonly IAppDbContext _db;
    public TimeLogService(IAppDbContext db) => _db = db;

    public async Task ClockInAsync(Guid employeeId, CancellationToken ct = default)
    {
        _db.Add(new TimeLog
        {
            EmployeeId = employeeId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClockOutAsync(Guid employeeId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var log = await _db.TimeLogs.FirstOrDefaultAsync(l => l.EmployeeId == employeeId && l.Date == today && l.FinishTime == null, ct);
        if (log is null) return;

        log.FinishTime = DateTimeOffset.UtcNow;
        log.TotalHours = Math.Round((log.FinishTime.Value - log.StartTime!.Value).TotalHours, 2);
        _db.Update(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TimeLogDto>> GetAllAsync(Guid? employeeId = null, CancellationToken ct = default)
    {
        var query = _db.TimeLogs.AsQueryable();
        if (employeeId is not null) query = query.Where(l => l.EmployeeId == employeeId);
        return await query.OrderByDescending(l => l.Date)
            .Select(l => new TimeLogDto(l.Id, l.EmployeeId, l.Date, l.StartTime, l.FinishTime, l.TotalHours))
            .ToListAsync(ct);
    }
}
