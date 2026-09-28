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

public sealed record RejectReserveTransactionCommand(Guid ClaimId, Guid TransactionId, string? RejectionReason)
    : ICommand<ReserveTransactionDto>;

internal sealed class RejectReserveTransactionCommandValidator : AbstractValidator<RejectReserveTransactionCommand>
{
    public RejectReserveTransactionCommandValidator()
    {
        // Free text in NVARCHAR(MAX) (FRS §9.6), so only presence is checked.
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
