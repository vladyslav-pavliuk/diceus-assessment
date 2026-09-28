using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaimDetail;

/// <summary>
/// GET /api/claims/{id} (FRS §10.1): the claim with parties, risk objects, validation issues, reserve
/// summary, document list and its most recent audit entries. 404 for another organisation's claim.
/// </summary>
public sealed record GetClaimDetailQuery(Guid ClaimId) : IQuery<ClaimDetailDto>;

internal sealed class GetClaimDetailQueryHandler(IClaimQueries claims) : IRequestHandler<GetClaimDetailQuery, ClaimDetailDto>
{
    public async Task<ClaimDetailDto> Handle(GetClaimDetailQuery request, CancellationToken cancellationToken) =>
        await claims.GetDetailAsync(request.ClaimId, ClaimQueryLimits.RecentAuditEntries, cancellationToken)
        ?? throw new NotFoundException(nameof(Claim), request.ClaimId);
}
