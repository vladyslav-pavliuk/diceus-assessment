using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.ApproveReserveTransaction;

/// <summary>
/// POST /api/claims/{id}/reserves/{txnId}/approve (FRS §6.4 step 8, §10.2). The endpoint admits supervisors
/// and managers (403 otherwise, D-25); the aggregate then re-checks, with the caller from the validated token,
/// that the approver is not the submitter (BR-R-03), has the authority for this amount (BR-R-02) and that the
/// $10M limit still holds (BR-R-05). Audit: RESERVE_APPROVED; the GL job is enqueued after commit.
/// Two approvers at once: one wins, the other gets 409 (ARCHITECTURE-PLAN §6.1 R1).
/// </summary>
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
