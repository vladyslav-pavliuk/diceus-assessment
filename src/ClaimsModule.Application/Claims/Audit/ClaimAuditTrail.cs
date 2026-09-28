using System.Text.Json;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Auditing;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims.Events;

namespace ClaimsModule.Application.Claims.Audit;

/// <summary>
/// Writes an audit row for every domain event, in the same transaction as the change (BR-A-02). Registered by the
/// assembly scan in DependencyInjection, so nothing references it by name.
/// </summary>
internal sealed class ClaimAuditTrail(IAuditLogService auditLog) :
    IBeforeCommitHandler<ClaimCreated>,
    IBeforeCommitHandler<ClaimStatusChanged>,
    IBeforeCommitHandler<ClaimClosed>,
    IBeforeCommitHandler<ClaimReopened>,
    IBeforeCommitHandler<PartyAdded>,
    IBeforeCommitHandler<PartyRemoved>,
    IBeforeCommitHandler<RiskObjectAdded>,
    IBeforeCommitHandler<ValidationIssueRaised>,
    IBeforeCommitHandler<ValidationIssueResolved>,
    IBeforeCommitHandler<ValidationIssueAcknowledged>,
    IBeforeCommitHandler<PolicyLinked>,
    IBeforeCommitHandler<HandlerAssigned>,
    IBeforeCommitHandler<ClaimDetailsUpdated>,
    IBeforeCommitHandler<ReserveLimitOverrideSet>,
    IBeforeCommitHandler<ReserveTransactionSubmitted>,
    IBeforeCommitHandler<ReserveAutoApproved>,
    IBeforeCommitHandler<ReserveApproved>,
    IBeforeCommitHandler<ReserveRejected>,
    IBeforeCommitHandler<ReserveRetracted>,
    IBeforeCommitHandler<GlPostingRetryRequested>,
    IBeforeCommitHandler<DocumentUploaded>
{
    private const string PartyEntity = "ClaimParty";
    private const string RiskObjectEntity = "ClaimRiskObject";
    private const string IssueEntity = "ClaimValidationIssue";
    private const string ReserveTransactionEntity = AuditValues.ReserveTransactionEntity;
    private const string DocumentEntity = "ClaimDocument";
    private const string PolicyEntity = "Policy";
    private const string UserEntity = "User";

    public Task HandleAsync(ClaimCreated e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ClaimCreated, $"Claim {e.ClaimNumber} created.",
        newValue: new { e.ClaimNumber });

    public Task HandleAsync(ClaimStatusChanged e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.StatusChanged, $"Status changed from {e.From} to {e.To}.",
        oldValue: new { Status = e.From },
        newValue: new { Status = e.To, e.Reason });

    /// <summary>The CC-04 justification goes with the closure reason (D-26).</summary>
    public Task HandleAsync(ClaimClosed e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ClaimClosed, "Claim closed.",
        newValue: new { e.ClosureReason, e.Justification, e.OpenReserveTotal });

    public Task HandleAsync(ClaimReopened e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ClaimReopened, "Claim reopened.",
        newValue: new { e.Reason });

    public Task HandleAsync(PartyAdded e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.PartyAdded, $"{e.Role} {e.DisplayName} added.",
        newValue: new { e.Role, e.DisplayName, IsActive = true },
        relatedEntityId: e.PartyId, relatedEntityType: PartyEntity);

    public Task HandleAsync(PartyRemoved e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.PartyRemoved, $"{e.Role} {e.DisplayName} removed.",
        oldValue: new { e.Role, e.DisplayName, IsActive = true },
        newValue: new { IsActive = false },
        relatedEntityId: e.PartyId, relatedEntityType: PartyEntity);

    public Task HandleAsync(RiskObjectAdded e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.RiskObjectAdded, $"{e.AssetType} risk object added: {e.AssetDescription}",
        newValue: new { e.AssetType, e.AssetDescription },
        relatedEntityId: e.RiskObjectId, relatedEntityType: RiskObjectEntity);

    public Task HandleAsync(ValidationIssueRaised e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ValidationIssueAdded, $"{e.Severity} validation issue {e.RuleCode}: {e.Message}",
        newValue: new { e.RuleCode, e.Severity, e.Field, e.Message },
        relatedEntityId: e.IssueId, relatedEntityType: IssueEntity);

    public Task HandleAsync(ValidationIssueResolved e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ValidationIssueResolved, $"Validation issue {e.RuleCode} resolved: {e.Note}",
        newValue: new { e.RuleCode, Status = "Resolved", e.Note },
        relatedEntityId: e.IssueId, relatedEntityType: IssueEntity);

    public Task HandleAsync(ValidationIssueAcknowledged e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ValidationIssueAcknowledged, $"Validation issue {e.RuleCode} acknowledged: {e.Note}",
        newValue: new { e.RuleCode, Status = "Acknowledged", e.Note },
        relatedEntityId: e.IssueId, relatedEntityType: IssueEntity);

    public Task HandleAsync(PolicyLinked e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.PolicyLinked, $"Policy {e.PolicyNumber} linked.",
        oldValue: new { PolicyId = e.PreviousPolicyId },
        newValue: new { e.PolicyId, e.PolicyNumber },
        relatedEntityId: e.PolicyId, relatedEntityType: PolicyEntity);

    public Task HandleAsync(HandlerAssigned e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.HandlerAssigned, "Handler assigned.",
        oldValue: new { AssignedHandlerId = e.PreviousHandlerId },
        newValue: new { AssignedHandlerId = e.HandlerId },
        relatedEntityId: e.HandlerId, relatedEntityType: UserEntity);

    public Task HandleAsync(ClaimDetailsUpdated e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ClaimUpdated, $"{e.Field} updated.",
        oldValue: new Dictionary<string, string?> { [JsonNamingPolicy.CamelCase.ConvertName(e.Field)] = e.OldValue },
        newValue: new Dictionary<string, string?> { [JsonNamingPolicy.CamelCase.ConvertName(e.Field)] = e.NewValue });

    public Task HandleAsync(ReserveLimitOverrideSet e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ReserveLimitOverrideSet,
        e.Enabled ? "Reserve limit override enabled." : "Reserve limit override disabled.",
        newValue: new { ReserveLimitOverride = e.Enabled, e.Reason });

    public Task HandleAsync(ReserveTransactionSubmitted e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ReserveCreated,
        $"{e.TransactionType} of {Money(e.Amount)} submitted on the {e.Component} reserve ({e.ApprovalStatus}).",
        newValue: new
        {
            e.Component,
            e.TransactionType,
            e.Amount,
            e.PreviousBalance,
            e.NewBalance,
            e.ApprovalStatus,
            e.RequiredAuthority,
            e.ExceedsAggregateLimit,
            e.ReserveComponentId,
        },
        relatedEntityId: e.TransactionId, relatedEntityType: ReserveTransactionEntity);

    public Task HandleAsync(ReserveAutoApproved e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ReserveAutoApproved, $"Reserve change of {Money(e.Amount)} auto-approved (within $10,000).",
        newValue: new { ApprovalStatus = "AutoApproved", e.Amount, e.IdempotencyKey },
        relatedEntityId: e.TransactionId, relatedEntityType: ReserveTransactionEntity);

    public Task HandleAsync(ReserveApproved e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ReserveApproved, $"Reserve change of {Money(e.Amount)} approved.",
        oldValue: new { ApprovalStatus = "PendingApproval" },
        newValue: new { ApprovalStatus = "Approved", e.Amount, e.IdempotencyKey },
        relatedEntityId: e.TransactionId, relatedEntityType: ReserveTransactionEntity);

    /// <summary>FRS §14.1 puts the rejection reason in OldValue; NewValue has it too.</summary>
    public Task HandleAsync(ReserveRejected e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ReserveRejected, $"Reserve change of {Money(e.Amount)} rejected: {e.Reason}",
        oldValue: new { ApprovalStatus = "PendingApproval", RejectionReason = e.Reason },
        newValue: new { ApprovalStatus = "Rejected", RejectionReason = e.Reason },
        relatedEntityId: e.TransactionId, relatedEntityType: ReserveTransactionEntity);

    public Task HandleAsync(ReserveRetracted e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.ReserveRetracted, $"Pending reserve change of {Money(e.Amount)} retracted by the submitter.",
        oldValue: new { ApprovalStatus = "PendingApproval" },
        newValue: new { ApprovalStatus = "Cancelled" },
        relatedEntityId: e.TransactionId, relatedEntityType: ReserveTransactionEntity);

    public Task HandleAsync(GlPostingRetryRequested e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.GlPostingRetried, $"GL posting of the reserve change of {Money(e.Amount)} retried.",
        oldValue: new { PostingStatus = "Failed" },
        newValue: new { PostingStatus = "Pending", e.IdempotencyKey },
        relatedEntityId: e.TransactionId, relatedEntityType: ReserveTransactionEntity);

    public Task HandleAsync(DocumentUploaded e, CancellationToken cancellationToken) => Record(
        e.ClaimId, AuditEventTypes.DocumentUploaded, $"Document {e.DocumentName} uploaded.",
        newValue: new { e.DocumentName, e.DocumentType, e.ContentType, e.FileSizeBytes },
        relatedEntityId: e.DocumentId, relatedEntityType: DocumentEntity);

    private static string Money(decimal amount) => AuditValues.Money(amount);

    private Task Record(
        Guid claimId,
        string eventType,
        string description,
        object? oldValue = null,
        object? newValue = null,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        auditLog.Record(new AuditEntry(
            claimId,
            eventType,
            description,
            AuditValues.ToJson(oldValue),
            AuditValues.ToJson(newValue),
            relatedEntityId,
            relatedEntityType));

        return Task.CompletedTask;
    }
}
