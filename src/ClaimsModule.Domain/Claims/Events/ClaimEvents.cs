using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Domain.Claims.Events;

// Raised by the Claim aggregate. Before commit each one becomes an audit row; ReserveAutoApproved,
// ReserveApproved and GlPostingRetryRequested also enqueue the GL posting job after commit (ARCHITECTURE-PLAN §2.5).

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

/// <summary>Enqueues the GL posting job after commit (FRS §6.3).</summary>
public sealed record ReserveAutoApproved(Guid ClaimId, Guid TransactionId, string IdempotencyKey, decimal Amount) : IDomainEvent;

/// <summary>Enqueues the GL posting job after commit (FRS §6.4 step 8).</summary>
public sealed record ReserveApproved(Guid ClaimId, Guid TransactionId, string IdempotencyKey, decimal Amount) : IDomainEvent;

public sealed record ReserveRejected(Guid ClaimId, Guid TransactionId, decimal Amount, string Reason) : IDomainEvent;

/// <summary>A failed GL posting was put back to Pending by a user; enqueues the GL posting job after commit (D-08).</summary>
public sealed record GlPostingRetryRequested(Guid ClaimId, Guid TransactionId, string IdempotencyKey, decimal Amount) : IDomainEvent;

public sealed record ReserveRetracted(Guid ClaimId, Guid TransactionId, decimal Amount) : IDomainEvent;

public sealed record DocumentUploaded(Guid ClaimId, Guid DocumentId, string DocumentName) : IDomainEvent;
