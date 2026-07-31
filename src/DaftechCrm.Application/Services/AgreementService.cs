using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class AgreementService : IAgreementService
{
    private readonly IAppDbContext _db;

    public AgreementService(IAppDbContext db) => _db = db;

    public async Task<AgreementDto> CreateAsync(CreateAgreementRequest request, CancellationToken ct = default)
    {
        var expiry = request.ExpiryDate ?? request.SignDate.AddYears(1);
        var agreement = new Agreement
        {
            ClientId = request.ClientId,
            DocumentNumber = request.DocumentNumber,
            ScannedFileUrl = request.ScannedFileUrl,
            AgreementPlace = request.AgreementPlace,
            SignDate = request.SignDate,
            ExpiryDate = expiry,
            SupportWindowMonths = request.SupportWindowMonths,
            BillingTier = request.BillingTier,
        };
        _db.Add(agreement);
        await _db.SaveChangesAsync(ct);
        return ToDto(agreement);
    }

    public async Task<IReadOnlyList<AgreementDto>> GetAllAsync(CancellationToken ct = default) =>
        (await _db.Agreements.ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<AgreementDto>> GetForClientAsync(Guid clientId, CancellationToken ct = default) =>
        (await _db.Agreements.Where(a => a.ClientId == clientId).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<AgreementDto>> GetExpiringSoonAsync(CancellationToken ct = default)
    {
        var in30 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        return (await _db.Agreements.Where(a => a.ExpiryDate <= in30).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<AgreementDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var agreement = await _db.Agreements.FirstOrDefaultAsync(a => a.Id == id, ct);
        return agreement is null ? null : ToDto(agreement);
    }

    /// <inheritdoc />
    public async Task<string?> SetScannedFileUrlAsync(Guid id, string? scannedFileUrl, CancellationToken ct = default)
    {
        var agreement = await _db.Agreements.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException($"Agreement {id} was not found.");

        var previous = agreement.ScannedFileUrl;
        agreement.ScannedFileUrl = scannedFileUrl;
        await _db.SaveChangesAsync(ct);
        return previous;
    }

    private static AgreementDto ToDto(Agreement a) => new(
        a.Id, a.ClientId, a.DocumentNumber, a.ScannedFileUrl, a.AgreementPlace,
        a.SignDate, a.ExpiryDate, a.SupportWindowMonths, a.Status, a.BillingTier
    );
}
