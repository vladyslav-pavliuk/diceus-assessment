namespace ClaimsModule.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProvider Provider { get; init; } = StorageProvider.AzureBlob;

    public AzureBlobStorageOptions AzureBlob { get; init; } = new();

    public LocalFileSystemStorageOptions LocalFileSystem { get; init; } = new();
}

public enum StorageProvider
{
    AzureBlob = 1,
    LocalFileSystem,
}

/// <summary>
/// Exactly one of <see cref="ConnectionString"/> (Azurite) or <see cref="ServiceUri"/> (Azure, managed identity with
/// user-delegation SAS, so no account key exists in configuration).
/// </summary>
public sealed class AzureBlobStorageOptions
{
    public string? ConnectionString { get; init; }

    public Uri? ServiceUri { get; init; }

    public string ContainerName { get; init; } = "claim-documents";
}

public sealed class LocalFileSystemStorageOptions
{
    /// <summary>Relative to the content root.</summary>
    public string RootPath { get; init; } = "uploads";

    /// <summary>The API's own base address, for absolute download links.</summary>
    public Uri? PublicBaseUrl { get; init; }
}
