using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.Options;

/// <summary>Bound from configuration ("Storage" section). Controls document upload limits and location.</summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Which storage backend to use. Only <see cref="StorageProvider.Local"/> is implemented today.</summary>
    public StorageProvider Provider { get; set; } = StorageProvider.Local;

    /// <summary>Absolute or relative root folder for uploaded documents.</summary>
    public string RootPath { get; set; } = "App_Data/uploads";

    /// <summary>Public URL prefix that maps to <see cref="RootPath"/> (served by the API or a reverse proxy).</summary>
    public string PublicBaseUrl { get; set; } = "/files";

    /// <summary>Maximum accepted file size in bytes (default 10 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Allowed file extensions (lower case, leading dot).</summary>
    public string[] AllowedExtensions { get; set; } =
        [".pdf", ".doc", ".docx", ".png", ".jpg", ".jpeg"];
}
