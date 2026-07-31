namespace DaftechCrm.Domain.Entities;

/// <summary>
/// An optional, separate 5-question satisfaction survey a client can fill
/// out after a ticket closes. This does NOT feed the 90/100 CSAT gate —
/// that's driven solely by Ticket.SatisfactionStars/Score. This is
/// additional qualitative/quantitative feedback for reporting.
/// </summary>
public class SatisfactionSurvey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = default!;

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = default!;

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    // Q1: How would you rate the speed of response?
    public int ResponseSpeedRating { get; set; } // 1-5

    // Q2: How would you rate the technician's professionalism?
    public int ProfessionalismRating { get; set; } // 1-5

    // Q3: How well was the issue explained to you?
    public int CommunicationClarityRating { get; set; } // 1-5

    // Q4: How likely are you to recommend DAFTECH support to a colleague?
    public int LikelihoodToRecommend { get; set; } // 1-5

    // Q5: Free-text — what could we have done better?
    public string? ImprovementFeedback { get; set; }
}
