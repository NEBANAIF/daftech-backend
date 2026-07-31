using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class SatisfactionSurveyService : ISatisfactionSurveyService
{
    private readonly IAppDbContext _db;
    public SatisfactionSurveyService(IAppDbContext db) => _db = db;

    public async Task<SatisfactionSurveyDto> SubmitAsync(SubmitSatisfactionSurveyRequest request, CancellationToken ct = default)
    {
        foreach (var (value, name) in new[]
        {
            (request.ResponseSpeedRating, nameof(request.ResponseSpeedRating)),
            (request.ProfessionalismRating, nameof(request.ProfessionalismRating)),
            (request.CommunicationClarityRating, nameof(request.CommunicationClarityRating)),
            (request.LikelihoodToRecommend, nameof(request.LikelihoodToRecommend)),
        })
        {
            if (value is < 1 or > 5)
                throw new ArgumentOutOfRangeException(name, $"{name} must be between 1 and 5.");
        }

        var existing = await _db.SatisfactionSurveys.FirstOrDefaultAsync(s => s.TicketId == request.TicketId, ct);

        if (existing is not null)
        {
            existing.ResponseSpeedRating = request.ResponseSpeedRating;
            existing.ProfessionalismRating = request.ProfessionalismRating;
            existing.CommunicationClarityRating = request.CommunicationClarityRating;
            existing.LikelihoodToRecommend = request.LikelihoodToRecommend;
            existing.ImprovementFeedback = request.ImprovementFeedback;
            existing.SubmittedAt = DateTimeOffset.UtcNow;
            _db.Update(existing);
            await _db.SaveChangesAsync(ct);
            return ToDto(existing);
        }

        var survey = new SatisfactionSurvey
        {
            TicketId = request.TicketId,
            ClientId = request.ClientId,
            ResponseSpeedRating = request.ResponseSpeedRating,
            ProfessionalismRating = request.ProfessionalismRating,
            CommunicationClarityRating = request.CommunicationClarityRating,
            LikelihoodToRecommend = request.LikelihoodToRecommend,
            ImprovementFeedback = request.ImprovementFeedback,
        };
        _db.Add(survey);
        await _db.SaveChangesAsync(ct);
        return ToDto(survey);
    }

    public async Task<SatisfactionSurveyDto?> GetForTicketAsync(Guid ticketId, CancellationToken ct = default)
    {
        var survey = await _db.SatisfactionSurveys.FirstOrDefaultAsync(s => s.TicketId == ticketId, ct);
        return survey is null ? null : ToDto(survey);
    }

    public async Task<IReadOnlyList<SatisfactionSurveyDto>> GetAllAsync(CancellationToken ct = default) =>
        (await _db.SatisfactionSurveys.OrderByDescending(s => s.SubmittedAt).ToListAsync(ct)).Select(ToDto).ToList();

    private static SatisfactionSurveyDto ToDto(SatisfactionSurvey s) => new(
        s.Id, s.TicketId, s.ClientId, s.SubmittedAt,
        s.ResponseSpeedRating, s.ProfessionalismRating, s.CommunicationClarityRating,
        s.LikelihoodToRecommend, s.ImprovementFeedback
    );
}
