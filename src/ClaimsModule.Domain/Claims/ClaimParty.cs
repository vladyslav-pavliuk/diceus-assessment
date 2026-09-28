using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Claims;

public sealed record PartyDetails(
    PartyRole Role,
    PartyType Type,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Notes);

/// <summary>
/// Removing a party clears <see cref="IsActive"/>. The IsDeleted column is a separate technical flag that
/// business flows never set (D-27).
/// </summary>
public sealed class ClaimParty : Entity
{
    private ClaimParty()
    {
    }

    private ClaimParty(Guid id)
        : base(id)
    {
    }

    public Guid ClaimId { get; private set; }

    public PartyRole PartyRole { get; private set; }

    public PartyType PartyType { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string? CompanyName { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsActiveClaimant => IsActive && PartyRole == PartyRole.Claimant;

    public string DisplayName => PartyType == PartyType.Company ? CompanyName! : $"{FirstName} {LastName}";

    internal static ClaimParty Create(Guid claimId, PartyDetails details)
    {
        var violations = new RuleViolations();

        if (!Enum.IsDefined(details.Role))
        {
            violations.Add(ErrorKeys.ClaimParties, DomainMessages.InvalidPartyRole);
        }

        if (!Enum.IsDefined(details.Type))
        {
            violations.Add(ErrorKeys.ClaimParties, DomainMessages.InvalidPartyType);
        }

        var firstName = Text.NullIfBlank(details.FirstName);
        var lastName = Text.NullIfBlank(details.LastName);
        var companyName = Text.NullIfBlank(details.CompanyName);

        if (details.Type == PartyType.Person && (firstName is null || lastName is null))
        {
            violations.Add(ErrorKeys.ClaimParties, DomainMessages.PersonNameRequired);
        }

        if (details.Type == PartyType.Company && companyName is null)
        {
            violations.Add(ErrorKeys.ClaimParties, DomainMessages.CompanyNameRequired);
        }

        var email = Text.NullIfBlank(details.Email);
        if (email is not null && !IsPlausibleEmail(email))
        {
            violations.Add(ErrorKeys.ClaimParties, DomainMessages.EmailInvalid);
        }

        violations.ThrowIfAny();

        return new ClaimParty(SequentialGuid.NewGuid())
        {
            ClaimId = claimId,
            PartyRole = details.Role,
            PartyType = details.Type,
            FirstName = details.Type == PartyType.Person ? firstName : null,
            LastName = details.Type == PartyType.Person ? lastName : null,
            CompanyName = details.Type == PartyType.Company ? companyName : null,
            Email = email,
            Phone = Text.NullIfBlank(details.Phone),
            Notes = Text.NullIfBlank(details.Notes),
            IsActive = true,
        };
    }

    internal void Deactivate() => IsActive = false;

    /// <summary>Deliberately loose: deliverability is not a domain rule. Shared with the request validator.</summary>
    public static bool IsPlausibleEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0
            && at == email.LastIndexOf('@')
            && email.IndexOf('.', at) > at + 1
            && !email.EndsWith('.')
            && !email.Any(char.IsWhiteSpace);
    }
}
