using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RetractReserveTransaction;

/// <summary>
/// POST /api/claims/{id}/reserves/{txnId}/retract (FRS §6.4 rule box, §10.2): only the submitter, only while
/// pending (422 otherwise, D-25). The row becomes Cancelled and a new transaction may be submitted on the
/// component. Audit: RESERVE_RETRACTED.
/// </summary>
public sealed record RetractReserveTransactionCommand(Guid ClaimId, Guid TransactionId) : ICommand<ReserveTransactionDto>;

internal sealed class RetractReserveTransactionCommandHandler(IClaimRepository claims, ICurrentUser currentUser, IMapper mapper)
    : IRequestHandler<RetractReserveTransactionCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(RetractReserveTransactionCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        claim.RetractReserveTransaction(request.TransactionId, currentUser.ToActor());

        return mapper.Map<ReserveTransactionDto>(claim.FindReserveTransaction(request.TransactionId));
    }
}
