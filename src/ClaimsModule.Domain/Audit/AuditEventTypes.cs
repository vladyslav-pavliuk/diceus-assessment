namespace ClaimsModule.Domain.Audit;

/// <summary>
/// ClaimAuditLog.EventType values. The FRS §14.1 list, then the additions of D-08 (BR-A-02: "every
/// significant business action"). Strings, not an enum: the list is open-ended.
/// </summary>
public static class AuditEventTypes
{
    // FRS §14.1.
    public const string ClaimCreated = "CLAIM_CREATED";
    public const string StatusChanged = "STATUS_CHANGED";
    public const string PartyAdded = "PARTY_ADDED";
    public const string PartyRemoved = "PARTY_REMOVED";
    public const string ReserveCreated = "RESERVE_CREATED";
    public const string ReserveAutoApproved = "RESERVE_AUTO_APPROVED";
    public const string ReserveApproved = "RESERVE_APPROVED";
    public const string ReserveRejected = "RESERVE_REJECTED";
    public const string ReserveRetracted = "RESERVE_RETRACTED";
    public const string GlPostingSimulated = "GL_POSTING_SIMULATED";
    public const string GlPostingFailed = "GL_POSTING_FAILED";
    public const string DocumentUploaded = "DOCUMENT_UPLOADED";
    public const string ClaimClosed = "CLAIM_CLOSED";
    public const string ClaimReopened = "CLAIM_REOPENED";
    public const string SlaBreachDetected = "SLA_BREACH_DETECTED";
    public const string ValidationIssueAdded = "VALIDATION_ISSUE_ADDED";

    // Additions (D-08).
    public const string RiskObjectAdded = "RISK_OBJECT_ADDED";
    public const string PolicyLinked = "POLICY_LINKED";
    public const string HandlerAssigned = "HANDLER_ASSIGNED";
    public const string ClaimUpdated = "CLAIM_UPDATED";
    public const string ReserveLimitOverrideSet = "RESERVE_LIMIT_OVERRIDE_SET";
    public const string GlPostingRetried = "GL_POSTING_RETRIED";
    public const string ValidationIssueResolved = "VALIDATION_ISSUE_RESOLVED";
    public const string ValidationIssueAcknowledged = "VALIDATION_ISSUE_ACKNOWLEDGED";
}
