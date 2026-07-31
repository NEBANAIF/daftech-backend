namespace DaftechCrm.Application.DTOs;

/// <summary>Input metrics handed to the AI summarizer — same numbers already shown in written/graphical reports, never the only source of truth.</summary>
public record EmployeePerformanceMetrics(
    string EmployeeName,
    int TicketsAssigned,
    int TicketsResolved,
    double? AverageResolutionHours,
    double OnTimeRate,
    double? AverageSatisfactionScore,
    double TotalHoursWorked
);

public record AiPerformanceSummaryResult(bool Available, string? Narrative, string? UnavailableReason);
