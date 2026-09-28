using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Claims.Documents;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetDocumentDownloadUrl;

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
