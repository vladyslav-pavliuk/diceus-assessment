namespace ClaimsModule.Domain.Claims;

// Enums are stored by name, so renaming a member is a data migration. The numeric values are never persisted.

public enum ClaimStatus
{
    Draft = 1,
    Open,
    UnderInvestigation,
    PendingPayment,
    Closed,
    Reopened,
    Withdrawn,
}

/// <summary>Separate from <see cref="IssueSeverity"/>, because "Critical" means different things in each (D-33).</summary>
public enum ClaimSeverity
{
    Catastrophic = 1,
    Critical,
    Standard,
    Minor,
}

public enum PartyRole
{
    Claimant = 1,
    Insured,
    ThirdParty,
    Witness,
    Attorney,
}

public enum PartyType
{
    Person = 1,
    Company,
}

public enum AssetType
{
    Vehicle = 1,
    Property,
    Person,
    Equipment,
    Other,
}

public enum IssueSeverity
{
    Critical = 1,
    Warning,
}

/// <summary>
/// Open until the rule passes again (Resolved, set by the system) or a user accepts a Warning
/// (Acknowledged). See D-07.
/// </summary>
public enum IssueStatus
{
    Open = 1,
    Resolved,
    Acknowledged,
}
