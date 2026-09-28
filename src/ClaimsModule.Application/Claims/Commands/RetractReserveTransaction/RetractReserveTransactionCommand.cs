using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RetractReserveTransaction;

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
