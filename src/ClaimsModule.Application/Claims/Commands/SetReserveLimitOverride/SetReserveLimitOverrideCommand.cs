using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.SetReserveLimitOverride;

/// <summary>
/// PUT /api/claims/{id}/reserve-limit-override (BR-R-05, FRS §3, D-08): a manager allows (or stops allowing)
/// approved reserves above $10,000,000 on this claim, with a reason. The endpoint admits managers only (403,
/// D-25) and the aggregate checks the role again. Audit: RESERVE_LIMIT_OVERRIDE_SET.
/// </summary>
public sealed record SetReserveLimitOverrideCommand(Guid ClaimId, bool? Enabled, string? Reason) : ICommand;

internal sealed class SetReserveLimitOverrideCommandValidator : AbstractValidator<SetReserveLimitOverrideCommand>
{
    public SetReserveLimitOverrideCommandValidator()
    {
        RuleFor(command => command.Enabled).NotNull().WithMessage(RequestMessages.OverrideEnabledRequired);

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage(DomainMessages.OverrideReasonRequired)
            .MaximumLength(FieldLengths.Reason).WithMessage(RequestMessages.TooLong("Reason", FieldLengths.Reason));
    }
}

internal sealed class SetReserveLimitOverrideCommandHandler(IClaimRepository claims, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<SetReserveLimitOverrideCommand>
{
    public async Task Handle(SetReserveLimitOverrideCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        claim.SetReserveLimitOverride(request.Enabled!.Value, request.Reason, currentUser.ToActor(), timeProvider.GetUtcNow());
    }
}
