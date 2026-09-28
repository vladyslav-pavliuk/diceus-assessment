using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Inputs;

/// <summary>Always an Add (D-05). The FNOL form has no reason field, so a blank reason gets a default (D-40).</summary>
public sealed record InitialReserveInput(ReserveComponentType? Component, decimal? Amount, string? ChangeReason)
{
    public const string DefaultChangeReason = "Initial reserve at FNOL.";
}

internal sealed class InitialReserveInputValidator : AbstractValidator<InitialReserveInput>
{
    public InitialReserveInputValidator()
    {
        RuleFor(reserve => reserve.Component)
            .NotNull().WithMessage(DomainMessages.InvalidReserveComponent)
            .IsInEnum().WithMessage(DomainMessages.InvalidReserveComponent);

        // BR-R-01: SubrogationRecoverable may be negative, but never zero.
        RuleFor(reserve => reserve.Amount)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(DomainMessages.ReserveAmountNotPositive)
            .Must((reserve, amount) => ReserveLimits.MayGoNegative(reserve.Component.GetValueOrDefault()) ? amount != 0 : amount > 0)
            .WithMessage(reserve => ReserveLimits.MayGoNegative(reserve.Component.GetValueOrDefault())
                ? DomainMessages.SubrogationAmountZero
                : DomainMessages.ReserveAmountNotPositive)
            .Must(amount => Amounts.HasValidScale(amount!.Value)).WithMessage(DomainMessages.TooManyDecimalPlaces("Reserve amount"));

        RuleFor(reserve => reserve.ChangeReason)
            .MaximumLength(FieldLengths.Reason).WithMessage(RequestMessages.TooLong("Change reason", FieldLengths.Reason));
    }
}
