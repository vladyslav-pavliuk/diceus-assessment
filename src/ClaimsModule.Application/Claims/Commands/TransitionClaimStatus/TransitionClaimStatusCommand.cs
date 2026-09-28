using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.TransitionClaimStatus;

/// <param name="Justification">CC-04: confirms closing a claim whose reserves are still open.</param>
public sealed record TransitionClaimStatusCommand(
    Guid ClaimId,
    ClaimStatus? TargetStatus,
    string? Reason,
    string? Justification) : ICommand<ClaimStatusChangedDto>;

/// <summary>After a reopen, <see cref="Status"/> is Open (BR-ST-04).</summary>
public sealed record ClaimStatusChangedDto(Guid ClaimId, ClaimStatus PreviousStatus, ClaimStatus Status);

internal sealed class TransitionClaimStatusCommandValidator : AbstractValidator<TransitionClaimStatusCommand>
{
    public TransitionClaimStatusCommandValidator()
    {
        RuleFor(command => command.TargetStatus)
            .NotNull().WithMessage(RequestMessages.TargetStatusRequired)
            .IsInEnum().WithMessage(RequestMessages.InvalidClaimStatus);

        RuleFor(command => command.Reason)
            .MaximumLength(FieldLengths.Reason).WithMessage(RequestMessages.TooLong("Reason", FieldLengths.Reason));
    }
}

internal sealed class TransitionClaimStatusCommandHandler(
    IClaimRepository claims,
    IStatusTransitionRepository transitions,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<TransitionClaimStatusCommand, ClaimStatusChangedDto>
{
    public async Task<ClaimStatusChangedDto> Handle(TransitionClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var table = await transitions.GetTableAsync(cancellationToken);
        var previousStatus = claim.Status;

        claim.ChangeStatus(
            request.TargetStatus!.Value,
            request.Reason,
            request.Justification,
            currentUser.ToActor(),
            table,
            timeProvider.GetUtcNow());

        return new ClaimStatusChangedDto(claim.Id, previousStatus, claim.Status);
    }
}
