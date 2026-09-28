namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// Document storage (FRS §13, BR-D-03, D-28). Implementations: AzureBlobStorageService (container
/// claim-documents) and LocalFileSystemStorageService, selected by Storage:Provider.
/// Object paths are built by the caller as {organisationId}/{claimId}/{documentId}_{sanitisedFileName}
/// (<see cref="Domain.Documents.DocumentBlobPath"/>). An existing object is never overwritten.
/// </summary>
public interface IStorageService
{
    /// <summary>Stores the content under <paramref name="objectPath"/>; fails if an object already exists there.</summary>
    Task UploadAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a short-lived read URL: a SAS for Azure (BR-D-02), a signed link to the development-only download
    /// endpoint for the local fallback. The response headers are part of the signature, so the browser gets the stored
    /// content type and the intended disposition whatever the client asks for. Document bytes never pass through the
    /// API for Azure.
    /// </summary>
    Task<SignedDownloadUrl> GetDownloadUrlAsync(
        string objectPath, DownloadHeaders headers, TimeSpan validFor, CancellationToken cancellationToken);

    /// <summary>
    /// Best-effort removal of an uploaded object whose metadata transaction failed to commit, so
    /// that no orphan blob is left behind. Deleting a missing object is not an error.
    /// </summary>
    Task DeleteAsync(string objectPath, CancellationToken cancellationToken);
}

public sealed record SignedDownloadUrl(Uri Url, DateTimeOffset ExpiresAt);

/// <summary>
/// The response headers a download URL fixes: the content type, the file name offered to the user and whether the
/// browser should display the file (PDF, images) or save it (everything else).
/// </summary>
public sealed record DownloadHeaders(string ContentType, string FileName, bool Inline);
