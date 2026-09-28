using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RejectReserveTransaction;

/// <summary>
/// POST /api/claims/{id}/reserves/{txnId}/reject (FRS §6.4 step 9, §10.2): supervisors and managers, with
/// the same authority as approving (D-39 item 8). The row stays in history as Rejected (BR-R-04) and its
/// posting is Cancelled. Audit: RESERVE_REJECTED with the reason.
/// </summary>
public sealed record RejectReserveTransactionCommand(Guid ClaimId, Guid TransactionId, string? RejectionReason)
    : ICommand<ReserveTransactionDto>;

internal sealed class RejectReserveTransactionCommandValidator : AbstractValidator<RejectReserveTransactionCommand>
{
    public RejectReserveTransactionCommandValidator()
    {
        // NVARCHAR(MAX) "free text" (FRS §9.6), so only presence is checked.
        RuleFor(command => command.RejectionReason)
            .NotEmpty().WithMessage(DomainMessages.RejectionReasonRequired);
    }
}

internal sealed class RejectReserveTransactionCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IMapper mapper) : IRequestHandler<RejectReserveTransactionCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(RejectReserveTransactionCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        claim.RejectReserveTransaction(request.TransactionId, request.RejectionReason, currentUser.ToActor(), timeProvider.GetUtcNow());

        return mapper.Map<ReserveTransactionDto>(claim.FindReserveTransaction(request.TransactionId));
    }
}
