namespace DaftechCrm.Application.Options;

/// <summary>
/// Business-rule knobs for the ticket confirmation/satisfaction workflow.
/// Bound from appsettings.json ("TicketWorkflow" section) so the threshold
/// and auto-close window can be tuned without a code change.
/// </summary>
public class TicketWorkflowOptions
{
    public const string SectionName = "TicketWorkflow";

    /// <summary>Minimum satisfaction score (0-100) required to close normally. Below this, the ticket escalates.</summary>
    public int MinimumSatisfactionScore { get; set; } = 90;

    /// <summary>Days after ResolvedAt before an unanswered confirmation auto-closes the ticket.</summary>
    public int ClientConfirmationWindowDays { get; set; } = 5;

    /// <summary>
    /// Target number of days from assignment to resolution. A ticket
    /// resolved within this window counts as "on time" for the Reports
    /// on-time/late charts. Default 3 business-day-equivalent target.
    /// </summary>
    public int OnTimeResolutionTargetDays { get; set; } = 3;
}
