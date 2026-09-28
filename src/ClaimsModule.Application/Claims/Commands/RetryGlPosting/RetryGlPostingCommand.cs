using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RetryGlPosting;

/// <summary>
/// POST /api/claims/{id}/reserves/{txnId}/retry-posting (FRS §11.3 "retry button for Failed", D-08): a failed
/// GL posting goes back to Pending (audit GL_POSTING_RETRIED) and the GL job is enqueued after commit. This is
/// the only way a Failed posting is posted again: the job itself only posts Pending rows (D-41).
/// </summary>
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
