using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RetryGlPosting;

/// <summary>The only way a Failed posting is posted again: the job itself only posts Pending rows (D-41).</summary>
public sealed record RetryGlPostingCommand(Guid ClaimId, Guid TransactionId) : ICommand<ReserveTransactionDto>;

internal sealed class RetryGlPostingCommandHandler(IClaimRepository claims, ICurrentUser currentUser, IMapper mapper)
    : IRequestHandler<RetryGlPostingCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(RetryGlPostingCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var transaction = claim.RetryGlPosting(request.TransactionId, currentUser.ToActor());
        return mapper.Map<ReserveTransactionDto>(transaction);
    }
}
