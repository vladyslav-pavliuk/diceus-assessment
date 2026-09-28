namespace ClaimsModule.Application.Abstractions;

/// <summary>Document storage (BR-D-03, D-28). An existing object is never overwritten.</summary>
public interface IStorageService
{
    Task UploadAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// A short-lived signed URL (BR-D-02). The response headers are part of the signature, so the browser gets the
    /// stored content type and disposition whatever the client asks for.
    /// </summary>
    Task<SignedDownloadUrl> GetDownloadUrlAsync(
        string objectPath, DownloadHeaders headers, TimeSpan validFor, CancellationToken cancellationToken);

    /// <summary>Deleting a missing object is not an error.</summary>
    Task DeleteAsync(string objectPath, CancellationToken cancellationToken);
}

public sealed record SignedDownloadUrl(Uri Url, DateTimeOffset ExpiresAt);

public sealed record DownloadHeaders(string ContentType, string FileName, bool Inline);
