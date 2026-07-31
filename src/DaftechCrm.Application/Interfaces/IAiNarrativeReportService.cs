using DaftechCrm.Application.DTOs;

namespace DaftechCrm.Application.Interfaces;

/// <summary>
/// SRS v2.0 §3.2 "AI Reporting Assist" / §4.10 / NFR-11: generates an
/// optional narrative summary of employee performance trends from metrics
/// already computed elsewhere (this never derives numbers itself — it
/// only narrates numbers the written/graphical reports already show).
/// Must degrade gracefully: if no provider is configured or the call
/// fails, callers get Available=false and fall back to the underlying
/// written/graphical report with no AI content — this is never allowed to
/// block or replace the base report.
/// </summary>
public interface IAiNarrativeReportService
{
    Task<AiPerformanceSummaryResult> SummarizeEmployeePerformanceAsync(EmployeePerformanceMetrics metrics, CancellationToken ct = default);
}
