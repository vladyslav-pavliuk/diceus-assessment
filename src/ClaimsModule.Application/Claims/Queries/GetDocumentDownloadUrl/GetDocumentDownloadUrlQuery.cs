using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Claims.Documents;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetDocumentDownloadUrl;

/// <summary>
/// GET /api/claims/{id}/documents/{documentId}/url (D-08, FRS §11.3 Tab 4 "Download button: calls API to get SAS URL"):
/// a new one-hour URL, for when the one from the list has expired. 404 for an unknown document or another organisation's claim.
/// </summary>
public sealed record GetDocumentDownloadUrlQuery(Guid ClaimId, Guid DocumentId) : IQuery<DocumentDownloadUrlDto>;

internal sealed class GetDocumentDownloadUrlQueryHandler(IClaimQueries claims, IStorageService storage)
    : IRequestHandler<GetDocumentDownloadUrlQuery, DocumentDownloadUrlDto>
{
    public async Task<DocumentDownloadUrlDto> Handle(GetDocumentDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var document = await claims.GetDocumentAsync(request.ClaimId, request.DocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClaimDocument), request.DocumentId);

        var url = await storage.SignAsync(document, cancellationToken);
        return new DocumentDownloadUrlDto(document.Id, url.Url, url.ExpiresAt);
    }
}
