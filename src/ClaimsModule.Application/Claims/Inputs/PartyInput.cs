using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Inputs;

/// <summary>
/// The fields of a party (FRS §9.3). Shared by FNOL (a list of parties) and "Add party" (one party
/// at the top level of the body), so both are validated by the same rules.
/// Enums are nullable: an unknown or missing value arrives as null or undefined and is reported by
/// the validator with a message, instead of failing JSON binding (D-40).
/// </summary>
public interface IPartyFields
{
    PartyRole? Role { get; }

    PartyType? Type { get; }

    string? FirstName { get; }

    string? LastName { get; }

    string? CompanyName { get; }

    string? Email { get; }

    string? Phone { get; }

    string? Notes { get; }
}

/// <summary>A party entered at intake (FRS §5.2 step 2) or through POST /claims/{id}/parties.</summary>
public sealed record PartyInput(
    PartyRole? Role,
    PartyType? Type,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Notes) : IPartyFields;

internal static class PartyFieldsExtensions
{
    /// <summary>Call only after validation: role and type are then defined.</summary>
    public static PartyDetails ToPartyDetails(this IPartyFields party) =>
        new(party.Role!.Value, party.Type!.Value, party.FirstName, party.LastName, party.CompanyName, party.Email, party.Phone, party.Notes);
}

/// <summary>
/// Request-shape rules for a party (BR-P-02 roles, FRS §9.3 names per party type and column sizes).
/// The entity re-checks the same rules with the same messages (defence in depth).
/// </summary>
internal sealed class PartyFieldsValidator<T> : AbstractValidator<T>
    where T : IPartyFields
{
    public PartyFieldsValidator()
    {
        RuleFor(party => party.Role)
            .NotNull().WithMessage(DomainMessages.InvalidPartyRole)
            .IsInEnum().WithMessage(DomainMessages.InvalidPartyRole);

        RuleFor(party => party.Type)
            .NotNull().WithMessage(DomainMessages.InvalidPartyType)
            .IsInEnum().WithMessage(DomainMessages.InvalidPartyType);

        RuleFor(party => party.FirstName)
            .NotEmpty().When(party => party.Type == PartyType.Person).WithMessage(DomainMessages.PersonNameRequired)
            .MaximumLength(FieldLengths.PersonName).WithMessage(RequestMessages.TooLong("First name", FieldLengths.PersonName));

        RuleFor(party => party.LastName)
            .NotEmpty().When(party => party.Type == PartyType.Person).WithMessage(DomainMessages.PersonNameRequired)
            .MaximumLength(FieldLengths.PersonName).WithMessage(RequestMessages.TooLong("Last name", FieldLengths.PersonName));

        RuleFor(party => party.CompanyName)
            .NotEmpty().When(party => party.Type == PartyType.Company).WithMessage(DomainMessages.CompanyNameRequired)
            .MaximumLength(FieldLengths.Name).WithMessage(RequestMessages.TooLong("Company name", FieldLengths.Name));

        RuleFor(party => party.Email)
            .Must(email => ClaimParty.IsPlausibleEmail(email!.Trim())).When(party => !string.IsNullOrWhiteSpace(party.Email))
            .WithMessage(DomainMessages.EmailInvalid)
            .MaximumLength(FieldLengths.Email).WithMessage(RequestMessages.TooLong("Email", FieldLengths.Email));

        RuleFor(party => party.Phone)
            .MaximumLength(FieldLengths.Phone).WithMessage(RequestMessages.TooLong("Phone", FieldLengths.Phone));
    }
}
