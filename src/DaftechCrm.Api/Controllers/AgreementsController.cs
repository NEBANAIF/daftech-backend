using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Api.Controllers;

/// <summary>
/// Agreement CRUD plus scanned-document upload/download (SRS §4.2 Document Storage).
/// </summary>
[ApiController]
[Route("api/agreements")]
public class AgreementsController : ControllerBase
{
    private readonly IAgreementService _agreements;
    private readonly IFileStorageService _storage;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<AgreementsController> _logger;

    public AgreementsController(
        IAgreementService agreements,
        IFileStorageService storage,
        IOptions<StorageOptions> storageOptions,
        ILogger<AgreementsController> logger)
    {
        _agreements = agreements;
        _storage = storage;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    /// <summary>All agreements.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgreementDto>>> GetAll(CancellationToken ct) =>
        Ok(await _agreements.GetAllAsync(ct));

    /// <summary>A single agreement by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AgreementDto>> GetById(Guid id, CancellationToken ct)
    {
        var agreement = await _agreements.GetByIdAsync(id, ct);
        return agreement is null ? NotFound() : Ok(agreement);
    }

    /// <summary>Agreements belonging to one client.</summary>
    [HttpGet("client/{clientId:guid}")]
    public async Task<ActionResult<IReadOnlyList<AgreementDto>>> GetForClient(Guid clientId, CancellationToken ct) =>
        Ok(await _agreements.GetForClientAsync(clientId, ct));

    /// <summary>Agreements expiring within the next 30 days (or already expired).</summary>
    [HttpGet("expiring-soon")]
    public async Task<ActionResult<IReadOnlyList<AgreementDto>>> GetExpiringSoon(CancellationToken ct) =>
        Ok(await _agreements.GetExpiringSoonAsync(ct));

    /// <summary>Creates an agreement. The scanned document is uploaded separately.</summary>
    [HttpPost]
    public async Task<ActionResult<AgreementDto>> Create([FromBody] CreateAgreementRequest request, CancellationToken ct)
    {
        var a = await _agreements.CreateAsync(request, ct);
        return Created($"/api/agreements/{a.Id}", a);
    }

    /// <summary>
    /// Uploads (or replaces) the scanned agreement document. Accepts .pdf, .doc, .docx,
    /// .png, .jpg and .jpeg up to the configured size limit. Any previously stored file
    /// is deleted once the new one is committed.
    /// </summary>
    [HttpPost("{id:guid}/document")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType(typeof(AgreementDocumentUploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgreementDocumentUploadResult>> UploadDocument(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("Please choose a file to upload.");

        var agreement = await _agreements.GetByIdAsync(id, ct);
        if (agreement is null) return NotFound($"Agreement {id} was not found.");

        try
        {
            await using var stream = file.OpenReadStream();
            var stored = await _storage.UploadAsync(stream, file.FileName, file.ContentType, ct);

            var previousUrl = await _agreements.SetScannedFileUrlAsync(id, stored.PublicUrl, ct);

            if (!string.IsNullOrWhiteSpace(previousUrl) && previousUrl != stored.PublicUrl)
            {
                // Best-effort cleanup: a stale file must never block a successful upload.
                var deleted = await _storage.DeleteAsync(previousUrl, ct);
                if (!deleted) _logger.LogWarning("Could not delete replaced document {PreviousUrl}", previousUrl);
            }

            _logger.LogInformation("Uploaded scanned document for agreement {AgreementId}", id);

            return Ok(new AgreementDocumentUploadResult(
                id, stored.PublicUrl, stored.OriginalFileName, stored.SizeBytes, stored.ContentType));
        }
        catch (FileValidationException ex)
        {
            _logger.LogWarning("Rejected upload for agreement {AgreementId}: {Reason}", id, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Upload failed for agreement {AgreementId}", id);
            return Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>Streams the scanned document back to the caller as a download.</summary>
    [HttpGet("{id:guid}/document")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken ct)
    {
        var agreement = await _agreements.GetByIdAsync(id, ct);
        if (agreement is null) return NotFound($"Agreement {id} was not found.");
        if (string.IsNullOrWhiteSpace(agreement.ScannedFileUrl))
            return NotFound("This agreement has no scanned document attached.");

        var download = await _storage.DownloadAsync(agreement.ScannedFileUrl, ct);
        if (download is null) return NotFound("The stored document is no longer available.");

        var fileName = $"{agreement.DocumentNumber}{Path.GetExtension(download.FileName)}";
        return File(download.Content, download.ContentType, fileName);
    }

    /// <summary>Removes the scanned document from an agreement and deletes it from storage.</summary>
    [HttpDelete("{id:guid}/document")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken ct)
    {
        var agreement = await _agreements.GetByIdAsync(id, ct);
        if (agreement is null) return NotFound($"Agreement {id} was not found.");
        if (string.IsNullOrWhiteSpace(agreement.ScannedFileUrl)) return NoContent();

        await _agreements.SetScannedFileUrlAsync(id, null, ct);
        await _storage.DeleteAsync(agreement.ScannedFileUrl, ct);
        return NoContent();
    }

    /// <summary>Upload limits so the frontend can validate before sending bytes over the wire.</summary>
    [HttpGet("document-constraints")]
    public ActionResult<DocumentConstraints> GetDocumentConstraints() =>
        Ok(new DocumentConstraints(_storageOptions.MaxFileSizeBytes, _storageOptions.AllowedExtensions));
}

/// <summary>Response returned after a successful document upload.</summary>
public record AgreementDocumentUploadResult(
    Guid AgreementId, string ScannedFileUrl, string OriginalFileName, long SizeBytes, string ContentType);

/// <summary>Client-side validation hints.</summary>
public record DocumentConstraints(long MaxFileSizeBytes, string[] AllowedExtensions);
