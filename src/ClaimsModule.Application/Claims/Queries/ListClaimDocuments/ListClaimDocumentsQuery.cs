using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Claims.Documents;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.ListClaimDocuments;

/// <summary>Signing a URL is local computation, so the list costs one SQL query and no storage round trip per document.</summary>
public sealed record ListClaimDocumentsQuery(Guid ClaimId) : IQuery<IReadOnlyList<DocumentDto>>;

internal sealed class ListClaimDocumentsQueryHandler(IClaimQueries claims, IStorageService storage)
    : IRequestHandler<ListClaimDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    public async Task<IReadOnlyList<DocumentDto>> Handle(ListClaimDocumentsQuery request, CancellationToken cancellationToken)
    {
        var documents = await claims.ListDocumentsAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var result = new List<DocumentDto>(documents.Count);
        foreach (var document in documents)
        {
            result.Add(await storage.ToDtoAsync(document, cancellationToken));
        }

        return result;
    }
}
