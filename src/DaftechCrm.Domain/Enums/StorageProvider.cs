namespace DaftechCrm.Domain.Enums;

/// <summary>Backing store used by <c>IFileStorageService</c> for scanned agreement documents.</summary>
public enum StorageProvider
{
    /// <summary>Local (or container-mounted) file system.</summary>
    Local = 0,

    /// <summary>Azure Blob Storage (reserved for a future implementation).</summary>
    AzureBlob = 1,

    /// <summary>Amazon S3 (reserved for a future implementation).</summary>
    AmazonS3 = 2,
}
