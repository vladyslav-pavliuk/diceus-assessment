using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaimReserves;

/// <summary>GET /api/claims/{id}/reserves (FRS §10.2, brief §3.3.3): the balance per component and the full transaction history.</summary>
public sealed record GetClaimReservesQuery(Guid ClaimId) : IQuery<ClaimReservesDto>;

internal sealed class GetClaimReservesQueryHandler(IClaimQueries claims) : IRequestHandler<GetClaimReservesQuery, ClaimReservesDto>
{
    public async Task<ClaimReservesDto> Handle(GetClaimReservesQuery request, CancellationToken cancellationToken) =>
        await claims.GetReservesAsync(request.ClaimId, cancellationToken)
        ?? throw new NotFoundException(nameof(Claim), request.ClaimId);
}
