using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Domain.Claims.Events;

// Each event becomes an audit row before commit. ReserveAutoApproved, ReserveApproved and
// GlPostingRetryRequested also enqueue the GL posting job after commit.

public sealed record ClaimCreated(Guid ClaimId, string ClaimNumber) : IDomainEvent;

public sealed record ClaimStatusChanged(Guid ClaimId, ClaimStatus From, ClaimStatus To, string? Reason) : IDomainEvent;

public sealed record ClaimClosed(Guid ClaimId, string ClosureReason, string? Justification, decimal OpenReserveTotal) : IDomainEvent;

public sealed record ClaimReopened(Guid ClaimId, string Reason) : IDomainEvent;

public sealed record PartyAdded(Guid ClaimId, Guid PartyId, PartyRole Role, string DisplayName) : IDomainEvent;

public sealed record PartyRemoved(Guid ClaimId, Guid PartyId, PartyRole Role, string DisplayName) : IDomainEvent;

public sealed record RiskObjectAdded(Guid ClaimId, Guid RiskObjectId, AssetType AssetType, string AssetDescription) : IDomainEvent;

public sealed record ValidationIssueRaised(
    Guid ClaimId, Guid IssueId, string RuleCode, IssueSeverity Severity, string Field, string Message) : IDomainEvent;

public sealed record ValidationIssueResolved(Guid ClaimId, Guid IssueId, string RuleCode, string Note) : IDomainEvent;

public sealed record ValidationIssueAcknowledged(Guid ClaimId, Guid IssueId, string RuleCode, string Note) : IDomainEvent;

public sealed record PolicyLinked(
    Guid ClaimId, Guid? PreviousPolicyId, Guid PolicyId, string PolicyNumber) : IDomainEvent;

public sealed record HandlerAssigned(Guid ClaimId, Guid? PreviousHandlerId, Guid HandlerId) : IDomainEvent;

public sealed record ClaimDetailsUpdated(Guid ClaimId, string Field, string? OldValue, string? NewValue) : IDomainEvent;

public sealed record ReserveLimitOverrideSet(Guid ClaimId, bool Enabled, string Reason) : IDomainEvent;

public sealed record ReserveTransactionSubmitted(
    Guid ClaimId,
    Guid TransactionId,
    Guid ReserveComponentId,
    ReserveComponentType Component,
    ReserveTransactionType TransactionType,
    decimal Amount,
    decimal PreviousBalance,
    decimal NewBalance,
    ReserveApprovalStatus ApprovalStatus,
    ApprovalAuthority RequiredAuthority,
    bool ExceedsAggregateLimit) : IDomainEvent;

public sealed record ReserveAutoApproved(Guid ClaimId, Guid TransactionId, string IdempotencyKey, decimal Amount) : IDomainEvent;

public sealed record ReserveApproved(Guid ClaimId, Guid TransactionId, string IdempotencyKey, decimal Amount) : IDomainEvent;

public sealed record ReserveRejected(Guid ClaimId, Guid TransactionId, decimal Amount, string Reason) : IDomainEvent;

/// <summary>A user put a failed GL posting back to Pending (D-08).</summary>
public sealed record GlPostingRetryRequested(Guid ClaimId, Guid TransactionId, string IdempotencyKey, decimal Amount) : IDomainEvent;

public sealed record ReserveRetracted(Guid ClaimId, Guid TransactionId, decimal Amount) : IDomainEvent;

public sealed record DocumentUploaded(
    Guid ClaimId, Guid DocumentId, string DocumentName, DocumentType DocumentType, string ContentType, long FileSizeBytes) : IDomainEvent;
