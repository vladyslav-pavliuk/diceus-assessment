using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.ApproveReserveTransaction;

/// <summary>Of two concurrent approvers, one wins and the other gets a 409.</summary>
public sealed record ApproveReserveTransactionCommand(Guid ClaimId, Guid TransactionId) : ICommand<ReserveTransactionDto>;

internal sealed class ApproveReserveTransactionCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IMapper mapper) : IRequestHandler<ApproveReserveTransactionCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(ApproveReserveTransactionCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        claim.ApproveReserveTransaction(request.TransactionId, currentUser.ToActor(), timeProvider.GetUtcNow());

        return mapper.Map<ReserveTransactionDto>(claim.FindReserveTransaction(request.TransactionId));
    }
}
