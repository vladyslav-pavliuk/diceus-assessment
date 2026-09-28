using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Application.Claims;

/// <summary>
/// A claim document with a fresh download URL (FRS §10.1 GET /documents, §11.3 Tab 4: name, type, upload date, uploader,
/// size). Returned by the list and by the upload. The URL is valid for one hour (BR-D-02); the blob path never leaves the API.
/// </summary>
public sealed record DocumentDto
{
    public required Guid Id { get; init; }

    public required DocumentType DocumentType { get; init; }

    public required string DocumentName { get; init; }

    public required string ContentType { get; init; }

    public required long FileSizeBytes { get; init; }

    public required DateTimeOffset UploadedAt { get; init; }

    public required Guid? UploadedByUserId { get; init; }

    public required string? UploadedByName { get; init; }

    public required string? Notes { get; init; }

    public required Uri DownloadUrl { get; init; }

    public required DateTimeOffset DownloadUrlExpiresAt { get; init; }
}

/// <summary>GET /api/claims/{id}/documents/{documentId}/url (D-08): a new URL when the one from the list has expired.</summary>
public sealed record DocumentDownloadUrlDto(Guid DocumentId, Uri DownloadUrl, DateTimeOffset ExpiresAt);
