namespace ClaimsModule.Infrastructure.Storage;

/// <summary>
/// The "Storage" configuration section (FRS §13, BR-D-03, D-28, D-42). <see cref="Provider"/> selects the implementation of
/// IStorageService: <c>AzureBlob</c> (every deployed environment) or <c>LocalFileSystem</c> (the development fallback).
/// </summary>
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
/// Exactly one of <see cref="ConnectionString"/> and <see cref="ServiceUri"/>:
/// <list type="bullet">
/// <item>a connection string with an account key (Azurite locally): SAS URLs are signed with the account key;</item>
/// <item>a service URI (Azure): the client authenticates with DefaultAzureCredential (the Container App's managed identity),
/// and SAS URLs are user-delegation SAS, so no account key exists anywhere in configuration.</item>
/// </list>
/// </summary>
public sealed class AzureBlobStorageOptions
{
    public string? ConnectionString { get; init; }

    public Uri? ServiceUri { get; init; }

    /// <summary>FRS §13: "claim-documents".</summary>
    public string ContainerName { get; init; } = "claim-documents";
}

public sealed class LocalFileSystemStorageOptions
{
    /// <summary>FRS §13: files go under /uploads/{organisationId}/{claimId}/. Relative paths are resolved against the content root.</summary>
    public string RootPath { get; init; } = "uploads";

    /// <summary>The API's own base address, used to build absolute download links the SPA can open in a new tab.</summary>
    public Uri? PublicBaseUrl { get; init; }
}
