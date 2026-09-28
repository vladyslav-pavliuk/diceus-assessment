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

/// <summary>
/// PUT /api/claims/{id}/status (FRS §10.1, D-26). The aggregate checks the transition against the
/// seeded table (BR-ST-01), the caller's role against the row's minimum role (D-09: 403 when the role
/// can never make the move, D-25), and the target's conditions (BR-ST-02, BR-ST-03 / CC-01..04). Every
/// failed condition comes back in one 422. Reopen continues to Open in the same transaction (BR-ST-04).
/// </summary>
/// <param name="Reason">Closure, withdrawal or reopen reason, where the transition requires one.</param>
/// <param name="Justification">CC-04: confirms closing a claim whose reserves are still open.</param>
public sealed record TransitionClaimStatusCommand(
    Guid ClaimId,
    ClaimStatus? TargetStatus,
    string? Reason,
    string? Justification) : ICommand<ClaimStatusChangedDto>;

/// <summary>The outcome of a transition. After a reopen, <see cref="Status"/> is Open (BR-ST-04).</summary>
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
