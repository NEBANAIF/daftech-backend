namespace DaftechCrm.Application.DTOs;

/// <summary>One bar in the per-employee on-time-vs-late chart.</summary>
public record EmployeeOnTimeStatsDto(
    Guid EmployeeId,
    string EmployeeName,
    int OnTimeCount,
    int LateCount,
    int TotalResolved,
    double OnTimeRate // 0-100
);

/// <summary>Overall on-time vs late split, for the donut chart.</summary>
public record OnTimeSummaryDto(
    int OnTimeCount,
    int LateCount,
    int TotalResolved,
    double OnTimeRate, // 0-100
    int TargetDays
);

public record OnTimeReportDto(
    OnTimeSummaryDto Summary,
    IReadOnlyList<EmployeeOnTimeStatsDto> ByEmployee
);

/// <summary>
/// Written + graphical performance data for one employee, per SRS v2.0
/// §4.10. AiNarrative is populated only when AI reporting is enabled and
/// the call succeeds — Available/AiUnavailableReason tell the frontend
/// whether to show it or silently omit it; the rest of this DTO is always
/// populated regardless of AI availability.
/// </summary>
public record EmployeePerformanceReportDto(
    Guid EmployeeId,
    string EmployeeName,
    int TicketsAssigned,
    int TicketsResolved,
    double? AverageResolutionHours,
    double OnTimeRate,
    double? AverageSatisfactionScore,
    double TotalHoursWorked,
    bool AiNarrativeAvailable,
    string? AiNarrative,
    string? AiUnavailableReason
);
