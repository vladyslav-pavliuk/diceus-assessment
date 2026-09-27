namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// Document storage (FRS §13, D-28). Implementations: AzureBlobStorageService (container
/// claim-documents) and LocalFileSystemStorageService, selected by Storage:Provider (Phase 5).
/// Object paths are built by the caller as {organisationId}/{claimId}/{documentId}_{sanitisedFileName}.
/// </summary>
public interface IStorageService
{
    Task UploadAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a short-lived read URL (a SAS for Azure). Document bytes never pass through the API
    /// for Azure (BR-D-02).
    /// </summary>
    Task<SignedDownloadUrl> GetDownloadUrlAsync(string objectPath, TimeSpan validFor, CancellationToken cancellationToken);

    /// <summary>
    /// Best-effort removal of an uploaded object whose metadata transaction failed to commit, so
    /// that no orphan blob is left behind.
    /// </summary>
    Task DeleteAsync(string objectPath, CancellationToken cancellationToken);
}

public sealed record SignedDownloadUrl(Uri Url, DateTimeOffset ExpiresAt);
