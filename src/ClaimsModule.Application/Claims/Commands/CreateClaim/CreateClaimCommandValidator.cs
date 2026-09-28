using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Claims.Inputs;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

/// <summary>
/// Only the rules that reject FNOL with a 422. Completeness rules (claimant, policy, risk object, policy period)
/// are persisted as issues on the Draft instead (D-06).
/// </summary>
internal sealed class CreateClaimCommandValidator : AbstractValidator<CreateClaimCommand>
{
    public CreateClaimCommandValidator(TimeProvider timeProvider, IReferenceDataQueries referenceData, IPolicyQueries policies)
    {
        RuleFor(command => command.LossDate)
            .Cascade(CascadeMode.Stop)
            .Must(lossDate => lossDate is { } value && value != default).WithMessage(DomainMessages.LossDateRequired)
            .Must(lossDate => lossDate <= timeProvider.GetUtcNow()).WithMessage(DomainMessages.LossDateInFuture);

        RuleFor(command => command.LossDescription)
            .Must(description => (description?.Trim().Length ?? 0) >= LossEvent.MinimumDescriptionLength)
            .WithMessage(DomainMessages.LossDescriptionTooShort);

        RuleFor(command => command.CauseOfLossCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(DomainMessages.CauseOfLossCodeInvalid)
            .MaximumLength(FieldLengths.ShortCode).WithMessage(DomainMessages.CauseOfLossCodeInvalid)
            .MustAsync((code, cancellationToken) => referenceData.IsActiveCauseOfLossCodeAsync(code!.Trim(), cancellationToken))
            .WithMessage(DomainMessages.CauseOfLossCodeInvalid);

        RuleFor(command => command.PolicyId)
            .MustAsync(async (policyId, cancellationToken) => await policies.GetAsync(policyId!.Value, cancellationToken) is not null)
            .When(command => command.PolicyId is not null)
            .WithMessage(RequestMessages.PolicyNotFound);

        // BR-C-06: reserves are blocked until a policy is linked, so an initial reserve needs one.
        RuleFor(command => command.PolicyId)
            .NotNull().When(command => command.InitialReserve is not null)
            .WithMessage(DomainMessages.NoPolicyLinked);

        RuleFor(command => command.EstimatedLossAmount)
            .GreaterThanOrEqualTo(0).WithMessage(DomainMessages.EstimatedLossAmountNegative)
            .Must(amount => Amounts.HasValidScale(amount!.Value)).When(command => command.EstimatedLossAmount is not null)
            .WithMessage(DomainMessages.TooManyDecimalPlaces("Estimated loss amount"));

        RuleFor(command => command.LossLocation)
            .MaximumLength(FieldLengths.LossLocation).WithMessage(RequestMessages.TooLong("Loss location", FieldLengths.LossLocation));

        RuleFor(command => command.PoliceReportNumber)
            .MaximumLength(FieldLengths.PoliceReportNumber).WithMessage(RequestMessages.TooLong("Police report number", FieldLengths.PoliceReportNumber));

        RuleFor(command => command.Severity)
            .IsInEnum().WithMessage(DomainMessages.InvalidClaimSeverity);

        RuleForEach(command => command.Parties).SetValidator(new PartyFieldsValidator<PartyInput>());
        RuleForEach(command => command.RiskObjects).SetValidator(new RiskObjectFieldsValidator<RiskObjectInput>());
        RuleFor(command => command.InitialReserve!).SetValidator(new InitialReserveInputValidator()).When(command => command.InitialReserve is not null);
    }
}
