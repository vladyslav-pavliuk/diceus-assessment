namespace ClaimsModule.Domain.Claims;

// Every enum is stored as its name in an NVARCHAR(50) column (CLAUDE.md rule 10), so renaming a
// member is a data migration. The numeric values are never persisted.

/// <summary>FRS §4.1.</summary>
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

/// <summary>
/// FRS §9.1. A separate type from <see cref="IssueSeverity"/>, because "Critical" means
/// different things in the two (D-33).
/// </summary>
public enum ClaimSeverity
{
    Catastrophic = 1,
    Critical,
    Standard,
    Minor,
}

/// <summary>FRS §7.5 BR-P-02, §9.3.</summary>
public enum PartyRole
{
    Claimant = 1,
    Insured,
    ThirdParty,
    Witness,
    Attorney,
}

/// <summary>FRS §9.3.</summary>
public enum PartyType
{
    Person = 1,
    Company,
}

/// <summary>FRS §5.2 step 2, §9.4.</summary>
public enum AssetType
{
    Vehicle = 1,
    Property,
    Person,
    Equipment,
    Other,
}

/// <summary>Severity of a persisted validation issue (FRS §5.4, D-07).</summary>
public enum IssueSeverity
{
    Critical = 1,
    Warning,
}

/// <summary>
/// Lifecycle of a validation issue (D-07): Open until the rule passes again (Resolved, set by the
/// system) or a user accepts a Warning (Acknowledged).
/// </summary>
public enum IssueStatus
{
    Open = 1,
    Resolved,
    Acknowledged,
}
