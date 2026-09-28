using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Inputs;

/// <summary>Shared by FNOL and Add Risk Object so both use the same rules.</summary>
public interface IRiskObjectFields
{
    AssetType? AssetType { get; }

    string? AssetDescription { get; }

    string? DamageDescription { get; }

    string? AssetReference { get; }

    bool IsPrimary { get; }
}

public sealed record RiskObjectInput(
    AssetType? AssetType,
    string? AssetDescription,
    string? DamageDescription,
    string? AssetReference,
    bool IsPrimary = false) : IRiskObjectFields;

internal static class RiskObjectFieldsExtensions
{
    /// <summary>Call only after validation.</summary>
    public static RiskObjectDetails ToRiskObjectDetails(this IRiskObjectFields riskObject) =>
        new(riskObject.AssetType!.Value, riskObject.AssetDescription!, riskObject.DamageDescription, riskObject.AssetReference, riskObject.IsPrimary);
}

internal sealed class RiskObjectFieldsValidator<T> : AbstractValidator<T>
    where T : IRiskObjectFields
{
    public RiskObjectFieldsValidator()
    {
        RuleFor(riskObject => riskObject.AssetType)
            .NotNull().WithMessage(DomainMessages.InvalidAssetType)
            .IsInEnum().WithMessage(DomainMessages.InvalidAssetType);

        RuleFor(riskObject => riskObject.AssetDescription)
            .NotEmpty().WithMessage(DomainMessages.AssetDescriptionRequired)
            .MaximumLength(FieldLengths.AssetDescription).WithMessage(RequestMessages.TooLong("Asset description", FieldLengths.AssetDescription));

        RuleFor(riskObject => riskObject.AssetReference)
            .MaximumLength(FieldLengths.AssetReference).WithMessage(RequestMessages.TooLong("Asset reference", FieldLengths.AssetReference));
    }
}
