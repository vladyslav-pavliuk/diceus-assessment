using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Inputs;

/// <summary>
/// Shared by FNOL and Add Party so both use the same rules. Enums are nullable, so a missing or unknown value is a
/// validation message rather than a JSON binding failure (D-40).
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
    /// <summary>Call only after validation.</summary>
    public static PartyDetails ToPartyDetails(this IPartyFields party) =>
        new(party.Role!.Value, party.Type!.Value, party.FirstName, party.LastName, party.CompanyName, party.Email, party.Phone, party.Notes);
}

/// <summary>The entity re-checks the same rules with the same messages.</summary>
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
