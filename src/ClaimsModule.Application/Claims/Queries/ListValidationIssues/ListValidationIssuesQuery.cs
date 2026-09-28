using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.ListValidationIssues;

/// <summary>Every status, resolved included.</summary>
public sealed record ListValidationIssuesQuery(Guid ClaimId) : IQuery<IReadOnlyList<ValidationIssueDto>>;

internal sealed class ListValidationIssuesQueryHandler(IClaimQueries claims)
    : IRequestHandler<ListValidationIssuesQuery, IReadOnlyList<ValidationIssueDto>>
{
    public async Task<IReadOnlyList<ValidationIssueDto>> Handle(ListValidationIssuesQuery request, CancellationToken cancellationToken) =>
        await claims.ListValidationIssuesAsync(request.ClaimId, cancellationToken)
        ?? throw new NotFoundException(nameof(Claim), request.ClaimId);
}
