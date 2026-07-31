namespace DaftechCrm.Application.DTOs;

public record SatisfactionSurveyDto(
    Guid Id,
    Guid TicketId,
    Guid ClientId,
    DateTimeOffset SubmittedAt,
    int ResponseSpeedRating,
    int ProfessionalismRating,
    int CommunicationClarityRating,
    int LikelihoodToRecommend,
    string? ImprovementFeedback
);

/// <summary>
/// The 5 questions, in order:
/// 1. ResponseSpeedRating        — "How would you rate the speed of our response?" (1-5)
/// 2. ProfessionalismRating      — "How would you rate the technician's professionalism?" (1-5)
/// 3. CommunicationClarityRating — "How clearly was the issue explained to you?" (1-5)
/// 4. LikelihoodToRecommend      — "How likely are you to recommend DAFTECH support to a colleague?" (1-5)
/// 5. ImprovementFeedback        — "What could we have done better?" (free text, optional)
/// </summary>
public record SubmitSatisfactionSurveyRequest(
    Guid TicketId,
    Guid ClientId,
    int ResponseSpeedRating,
    int ProfessionalismRating,
    int CommunicationClarityRating,
    int LikelihoodToRecommend,
    string? ImprovementFeedback
);
