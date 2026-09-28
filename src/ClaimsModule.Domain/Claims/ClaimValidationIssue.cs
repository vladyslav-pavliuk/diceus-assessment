using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Claims;

public static class ValidationRuleCodes
{
    /// <summary>Warning: loss date outside the policy period. Blocks Draft → Open until acknowledged (D-19).</summary>
    public const string LossDateOutsidePolicyPeriod = "BR-C-02";

    /// <summary>Critical: no active Claimant.</summary>
    public const string NoClaimant = "BR-C-03";

    /// <summary>Warning: no policy linked. Blocks reserves, not Open.</summary>
    public const string NoPolicy = "BR-C-06";

    /// <summary>Warning: no risk objects.</summary>
    public const string NoRiskObject = "NO-RISK-OBJECT";
}

/// <summary>
/// Critical issues block Draft → Open and closure (BR-ST-02, CC-02). Warnings do not, except BR-C-02 (D-19).
/// </summary>
public sealed class ClaimValidationIssue : Entity
{
    private ClaimValidationIssue()
    {
    }

    private ClaimValidationIssue(Guid id)
        : base(id)
    {
    }

    public Guid ClaimId { get; private set; }

    public string RuleCode { get; private set; } = null!;

    public IssueSeverity Severity { get; private set; }

    public string Field { get; private set; } = null!;

    public string Message { get; private set; } = null!;

    public IssueStatus Status { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Null when the system resolved the issue.</summary>
    public Guid? ResolvedByUserId { get; private set; }

    public string? ResolutionNote { get; private set; }

    /// <summary>Open or Acknowledged: the rule still fails.</summary>
    public bool IsActive => Status is IssueStatus.Open or IssueStatus.Acknowledged;

    public bool IsOpenCritical => Status == IssueStatus.Open && Severity == IssueSeverity.Critical;

    internal static ClaimValidationIssue Raise(
        Guid claimId, string ruleCode, IssueSeverity severity, string field, string message, DateTimeOffset now) =>
        new(SequentialGuid.NewGuid())
        {
            ClaimId = claimId,
            RuleCode = ruleCode,
            Severity = severity,
            Field = field,
            Message = message,
            Status = IssueStatus.Open,
            RaisedAt = now,
        };

    internal void Resolve(string note, DateTimeOffset now)
    {
        Status = IssueStatus.Resolved;
        ResolvedAt = now;
        ResolvedByUserId = null;
        ResolutionNote = note;
    }

    internal void Acknowledge(Guid userId, string note, DateTimeOffset now)
    {
        Status = IssueStatus.Acknowledged;
        ResolvedAt = now;
        ResolvedByUserId = userId;
        ResolutionNote = note;
    }
}
