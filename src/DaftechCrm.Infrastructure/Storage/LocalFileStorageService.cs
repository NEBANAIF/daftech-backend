using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Infrastructure.Storage;

/// <summary>
/// Local file-system implementation of <see cref="IFileStorageService"/>. Files are
/// written to <c>{RootPath}/{yyyy}/{MM}/{guid}{ext}</c> so a single folder never grows
/// unbounded, and the original name is never used on disk (avoids path traversal and
/// collisions).
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
    };

    private readonly StorageOptions _options;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IOptions<StorageOptions> options, ILogger<LocalFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private string Root => Path.GetFullPath(_options.RootPath);

    /// <inheritdoc />
    public async Task<StoredFile> UploadAsync(Stream content, string fileName, string? contentType, CancellationToken ct = default)
    {
        if (content is null) throw new FileValidationException("No file content was supplied.");
        if (string.IsNullOrWhiteSpace(fileName)) throw new FileValidationException("A file name is required.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Contains(extension))
        {
            throw new FileValidationException(
                $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _options.AllowedExtensions)}.");
        }

        if (content.CanSeek)
        {
            if (content.Length == 0) throw new FileValidationException("The uploaded file is empty.");
            if (content.Length > _options.MaxFileSizeBytes)
            {
                throw new FileValidationException(
                    $"File is larger than the {_options.MaxFileSizeBytes / (1024 * 1024)} MB limit.");
            }
        }

        var now = DateTime.UtcNow;
        var relativeFolder = Path.Combine(now.ToString("yyyy"), now.ToString("MM"));
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var absoluteFolder = Path.Combine(Root, relativeFolder);

        try
        {
            Directory.CreateDirectory(absoluteFolder);
            var absolutePath = Path.Combine(absoluteFolder, storedName);

            await using (var target = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(target, ct);
            }

            var written = new FileInfo(absolutePath);
            if (written.Length > _options.MaxFileSizeBytes)
            {
                // Non-seekable streams can only be measured after the copy.
                File.Delete(absolutePath);
                throw new FileValidationException(
                    $"File is larger than the {_options.MaxFileSizeBytes / (1024 * 1024)} MB limit.");
            }

            var storedPath = $"{relativeFolder.Replace(Path.DirectorySeparatorChar, '/')}/{storedName}";
            var publicUrl = $"{_options.PublicBaseUrl.TrimEnd('/')}/{storedPath}";

            _logger.LogInformation("Stored uploaded document {StoredPath} ({SizeBytes} bytes)", storedPath, written.Length);

            return new StoredFile(storedPath, publicUrl, Path.GetFileName(fileName), written.Length, ResolveContentType(extension, contentType));
        }
        catch (FileValidationException)
        {
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O failure while storing upload {FileName}", fileName);
            throw new InvalidOperationException("The file could not be saved. Please try again.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied writing to storage root {Root}", Root);
            throw new InvalidOperationException("The server is not permitted to write to document storage.", ex);
        }
    }

    /// <inheritdoc />
    public Task<FileDownload?> DownloadAsync(string storedPathOrUrl, CancellationToken ct = default)
    {
        var absolutePath = ResolveAbsolutePath(storedPathOrUrl);
        if (absolutePath is null || !File.Exists(absolutePath))
        {
            _logger.LogWarning("Requested document was not found: {Path}", storedPathOrUrl);
            return Task.FromResult<FileDownload?>(null);
        }

        var extension = Path.GetExtension(absolutePath);
        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<FileDownload?>(new FileDownload(stream, ResolveContentType(extension, null), Path.GetFileName(absolutePath)));
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string storedPathOrUrl, CancellationToken ct = default)
    {
        var absolutePath = ResolveAbsolutePath(storedPathOrUrl);
        if (absolutePath is null || !File.Exists(absolutePath)) return Task.FromResult(false);

        try
        {
            File.Delete(absolutePath);
            _logger.LogInformation("Deleted document {Path}", storedPathOrUrl);
            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to delete document {Path}", storedPathOrUrl);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        var probePath = Path.Combine(Root, $".health-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(Root);
            await File.WriteAllTextAsync(probePath, "ok", ct);
            var readBack = await File.ReadAllTextAsync(probePath, ct);
            return readBack == "ok";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage health probe failed for root {Root}", Root);
            return false;
        }
        finally
        {
            try { if (File.Exists(probePath)) File.Delete(probePath); } catch { /* best effort */ }
        }
    }

    /// <summary>Maps a stored path or public URL back to an absolute path, rejecting traversal attempts.</summary>
    private string? ResolveAbsolutePath(string storedPathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(storedPathOrUrl)) return null;

        var relative = storedPathOrUrl.Trim();
        var prefix = _options.PublicBaseUrl.TrimEnd('/');
        if (relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            relative = relative[prefix.Length..];

        relative = relative.TrimStart('/', '\\');
        if (relative.Length == 0) return null;

        var candidate = Path.GetFullPath(Path.Combine(Root, relative));
        // Guard against "../" escaping the storage root.
        return candidate.StartsWith(Root, StringComparison.Ordinal) ? candidate : null;
    }

    private static string ResolveContentType(string extension, string? supplied) =>
        ContentTypes.TryGetValue(extension, out var known) ? known : supplied ?? "application/octet-stream";
}
