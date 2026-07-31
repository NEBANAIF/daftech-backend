using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Application.Services;

public class ReportService : IReportService
{
    private readonly IAppDbContext _db;
    private readonly TicketWorkflowOptions _options;
    private readonly IAiNarrativeReportService _aiNarrative;

    public ReportService(IAppDbContext db, IOptions<TicketWorkflowOptions> options, IAiNarrativeReportService aiNarrative)
    {
        _db = db;
        _options = options.Value;
        _aiNarrative = aiNarrative;
    }

    /// <summary>
    /// "On time" = ResolvedAt - AssignedAt is within OnTimeResolutionTargetDays.
    /// Only tickets that have both AssignedAt and ResolvedAt set are counted
    /// (i.e. tickets that actually reached Resolved at some point) —
    /// tickets still in progress or never assigned don't factor in yet.
    /// </summary>
    public async Task<OnTimeReportDto> GetOnTimeResolutionReportAsync(CancellationToken ct = default)
    {
        var targetSpan = TimeSpan.FromDays(_options.OnTimeResolutionTargetDays);

        var resolvedTickets = await _db.Tickets
            .Include(t => t.AssignedEmployee)
            .Where(t => t.AssignedAt != null && t.ResolvedAt != null)
            .ToListAsync(ct);

        bool IsOnTime(Ticket t) => (t.ResolvedAt!.Value - t.AssignedAt!.Value) <= targetSpan;

        var onTime = resolvedTickets.Count(IsOnTime);
        var late = resolvedTickets.Count - onTime;
        var overallRate = resolvedTickets.Count > 0 ? Math.Round(onTime * 100.0 / resolvedTickets.Count, 1) : 0;

        var summary = new OnTimeSummaryDto(onTime, late, resolvedTickets.Count, overallRate, _options.OnTimeResolutionTargetDays);

        var byEmployee = resolvedTickets
            .Where(t => t.AssignedEmployeeId != null)
            .GroupBy(t => new { t.AssignedEmployeeId, Name = t.AssignedEmployee?.FullName ?? "Unknown" })
            .Select(g =>
            {
                var onTimeCount = g.Count(IsOnTime);
                var total = g.Count();
                return new EmployeeOnTimeStatsDto(
                    g.Key.AssignedEmployeeId!.Value,
                    g.Key.Name,
                    onTimeCount,
                    total - onTimeCount,
                    total,
                    total > 0 ? Math.Round(onTimeCount * 100.0 / total, 1) : 0
                );
            })
            .OrderByDescending(e => e.OnTimeRate)
            .ToList();

        return new OnTimeReportDto(summary, byEmployee);
    }

    public async Task<EmployeePerformanceReportDto> GetEmployeePerformanceReportAsync(Guid employeeId, bool includeAiNarrative, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        var assignedTickets = await _db.Tickets.Where(t => t.AssignedEmployeeId == employeeId).ToListAsync(ct);
        var resolvedOrClosed = assignedTickets.Where(t => t.ResolvedAt != null).ToList();

        double? avgResolutionHours = null;
        var withBothTimestamps = assignedTickets.Where(t => t.AssignedAt != null && t.ResolvedAt != null).ToList();
        if (withBothTimestamps.Count > 0)
            avgResolutionHours = withBothTimestamps.Average(t => (t.ResolvedAt!.Value - t.AssignedAt!.Value).TotalHours);

        var targetSpan = TimeSpan.FromDays(_options.OnTimeResolutionTargetDays);
        var onTimeCount = withBothTimestamps.Count(t => (t.ResolvedAt!.Value - t.AssignedAt!.Value) <= targetSpan);
        var onTimeRate = withBothTimestamps.Count > 0 ? Math.Round(onTimeCount * 100.0 / withBothTimestamps.Count, 1) : 0;

        var scores = assignedTickets.Where(t => t.SatisfactionScore != null).Select(t => t.SatisfactionScore!.Value).ToList();
        double? avgSatisfaction = scores.Count > 0 ? scores.Average() : null;

        var totalHours = await _db.TimeLogs
            .Where(l => l.EmployeeId == employeeId && l.TotalHours != null)
            .SumAsync(l => l.TotalHours!.Value, ct);

        bool aiAvailable = false;
        string? narrative = null;
        string? unavailableReason = includeAiNarrative ? null : "AI narrative not requested.";

        if (includeAiNarrative)
        {
            var metrics = new EmployeePerformanceMetrics(
                employee.FullName, assignedTickets.Count, resolvedOrClosed.Count,
                avgResolutionHours, onTimeRate, avgSatisfaction, totalHours
            );
            var aiResult = await _aiNarrative.SummarizeEmployeePerformanceAsync(metrics, ct);
            aiAvailable = aiResult.Available;
            narrative = aiResult.Narrative;
            unavailableReason = aiResult.UnavailableReason;
        }

        return new EmployeePerformanceReportDto(
            employee.Id, employee.FullName, assignedTickets.Count, resolvedOrClosed.Count,
            avgResolutionHours, onTimeRate, avgSatisfaction, totalHours,
            aiAvailable, narrative, unavailableReason
        );
    }
}
