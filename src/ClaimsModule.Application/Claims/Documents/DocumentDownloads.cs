using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Application.Claims.Documents;

internal static class DocumentDownloads
{
    /// <summary>Fixed by BR-D-02, so not configurable.</summary>
    public static readonly TimeSpan UrlLifetime = TimeSpan.FromHours(1);

    public static Task<SignedDownloadUrl> SignAsync(this IStorageService storage, ClaimDocumentRecord document, CancellationToken cancellationToken)
    {
        // The headers come from the stored metadata, never from the request.
        var inline = DocumentFormat.FromContentType(document.ContentType)?.DisplayInline ?? false;
        var headers = new DownloadHeaders(document.ContentType, document.DocumentName, inline);
        return storage.GetDownloadUrlAsync(document.BlobPath, headers, UrlLifetime, cancellationToken);
    }

    public static async Task<DocumentDto> ToDtoAsync(this IStorageService storage, ClaimDocumentRecord document, CancellationToken cancellationToken)
    {
        var url = await storage.SignAsync(document, cancellationToken);
        return new DocumentDto
        {
            Id = document.Id,
            DocumentType = document.DocumentType,
            DocumentName = document.DocumentName,
            ContentType = document.ContentType,
            FileSizeBytes = document.FileSizeBytes,
            UploadedAt = document.UploadedAt,
            UploadedByUserId = document.UploadedByUserId,
            UploadedByName = document.UploadedByName,
            Notes = document.Notes,
            DownloadUrl = url.Url,
            DownloadUrlExpiresAt = url.ExpiresAt,
        };
    }
}
