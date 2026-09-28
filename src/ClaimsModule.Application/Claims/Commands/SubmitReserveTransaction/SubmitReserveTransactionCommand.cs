using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.SubmitReserveTransaction;

/// <summary>
/// POST /api/claims/{id}/reserves (FRS §10.2): opens or adjusts a reserve component. The aggregate decides
/// everything that depends on the claim: Add or Adjust when the type is omitted, the balance rule, one
/// pending transaction per component, the approval tier and the $10M check (D-05, D-11, D-22).
/// An auto-approved transaction enqueues the GL posting job after commit.
/// </summary>
/// <param name="TransactionType">Optional: Add for a new component, Adjust otherwise (D-05).</param>
/// <param name="Amount">The signed delta; must be omitted for Reverse, which the system computes.</param>
public sealed record SubmitReserveTransactionCommand(
    Guid ClaimId,
    ReserveComponentType? Component,
    ReserveTransactionType? TransactionType,
    decimal? Amount,
    string? ChangeReason) : ICommand<ReserveSubmittedDto>;

/// <summary>
/// The request-shape rules (FRS §8 ReserveAmount / ReserveComponent). Whether an omitted type means Add or
/// Adjust depends on the claim, so the sign rules for an omitted type are the aggregate's (D-05, D-41).
/// </summary>
internal sealed class SubmitReserveTransactionCommandValidator : AbstractValidator<SubmitReserveTransactionCommand>
{
    public SubmitReserveTransactionCommandValidator()
    {
        RuleFor(command => command.Component)
            .NotNull().WithMessage(DomainMessages.InvalidReserveComponent)
            .IsInEnum().WithMessage(DomainMessages.InvalidReserveComponent)
            .OverridePropertyName(ErrorKeys.ReserveComponent);

        RuleFor(command => command.TransactionType)
            .IsInEnum().WithMessage(DomainMessages.InvalidTransactionType);

        RuleFor(command => command.Amount)
            .Null().WithMessage(DomainMessages.ReverseAmountNotAllowed)
            .When(command => command.TransactionType == ReserveTransactionType.Reverse)
            .OverridePropertyName(ErrorKeys.ReserveAmount);

        // BR-R-01 for an explicit Add: greater than zero, except SubrogationRecoverable (not zero).
        RuleFor(command => command.Amount)
            .Must((command, amount) => amount is { } value && (ReserveLimits.MayGoNegative(command.Component.GetValueOrDefault()) ? value != 0 : value > 0))
            .WithMessage(command => ReserveLimits.MayGoNegative(command.Component.GetValueOrDefault())
                ? DomainMessages.SubrogationAmountZero
                : DomainMessages.ReserveAmountNotPositive)
            .When(command => command.TransactionType == ReserveTransactionType.Add)
            .OverridePropertyName(ErrorKeys.ReserveAmount);

        RuleFor(command => command.Amount)
            .Must(amount => amount is not null and not 0m).WithMessage(DomainMessages.AdjustmentAmountZero)
            .When(command => command.TransactionType == ReserveTransactionType.Adjust)
            .OverridePropertyName(ErrorKeys.ReserveAmount);

        RuleFor(command => command.Amount)
            .NotNull().WithMessage(DomainMessages.ReserveAmountNotPositive)
            .When(command => command.TransactionType is null)
            .OverridePropertyName(ErrorKeys.ReserveAmount);

        RuleFor(command => command.Amount)
            .Must(amount => Amounts.HasValidScale(amount!.Value)).WithMessage(DomainMessages.TooManyDecimalPlaces("Reserve amount"))
            .When(command => command.Amount is not null)
            .OverridePropertyName(ErrorKeys.ReserveAmount);

        RuleFor(command => command.ChangeReason)
            .NotEmpty().WithMessage(DomainMessages.ChangeReasonRequired)
            .MaximumLength(FieldLengths.Reason).WithMessage(RequestMessages.TooLong("Change reason", FieldLengths.Reason));
    }
}

internal sealed class SubmitReserveTransactionCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IMapper mapper) : IRequestHandler<SubmitReserveTransactionCommand, ReserveSubmittedDto>
{
    public async Task<ReserveSubmittedDto> Handle(SubmitReserveTransactionCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var submitted = claim.SubmitReserveTransaction(
            request.Component!.Value,
            request.TransactionType,
            request.Amount,
            request.ChangeReason,
            currentUser.ToActor(),
            timeProvider.GetUtcNow());

        return new ReserveSubmittedDto(
            request.Component.Value, mapper.Map<ReserveTransactionDto>(submitted.Transaction), submitted.Warnings);
    }
}
