namespace DaftechCrm.Application.Interfaces;

/// <summary>Result of a successful upload.</summary>
/// <param name="StoredPath">Provider-relative path used for later download/delete (e.g. <c>2026/07/guid.pdf</c>).</param>
/// <param name="PublicUrl">URL the frontend can use to display or download the file.</param>
/// <param name="OriginalFileName">File name as supplied by the client.</param>
/// <param name="SizeBytes">Stored size in bytes.</param>
/// <param name="ContentType">Best-effort content type.</param>
public record StoredFile(string StoredPath, string PublicUrl, string OriginalFileName, long SizeBytes, string ContentType);

/// <summary>A file streamed back out of storage.</summary>
public record FileDownload(Stream Content, string ContentType, string FileName);

/// <summary>Raised when an upload fails validation (bad extension, too large, empty).</summary>
public class FileValidationException : Exception
{
    public FileValidationException(string message) : base(message) { }
}

/// <summary>
/// Abstraction over document storage for scanned agreements (SRS §4.2 Document Storage).
/// Implemented in Infrastructure so the Application layer stays provider-agnostic.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Validates and stores a file, returning its public URL. Throws <see cref="FileValidationException"/> on invalid input.</summary>
    Task<StoredFile> UploadAsync(Stream content, string fileName, string? contentType, CancellationToken ct = default);

    /// <summary>Opens a stored file for reading. Returns <c>null</c> when the file no longer exists.</summary>
    Task<FileDownload?> DownloadAsync(string storedPathOrUrl, CancellationToken ct = default);

    /// <summary>Deletes a stored file. Returns <c>false</c> when nothing was deleted.</summary>
    Task<bool> DeleteAsync(string storedPathOrUrl, CancellationToken ct = default);

    /// <summary>True when the backing store is reachable and writable (used by the storage health check).</summary>
    Task<bool> CheckHealthAsync(CancellationToken ct = default);
}
