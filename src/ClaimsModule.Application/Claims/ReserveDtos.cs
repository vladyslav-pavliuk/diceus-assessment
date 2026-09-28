using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Claims;

// Read models of the reserve endpoints (FRS §10.2, §11.3 Tab 3).

/// <summary>A reserve summary card (FRS §11.3 Tab 3): current balance and the amount awaiting approval (D-11, D-21).</summary>
public sealed record ReserveComponentSummaryDto
{
    public required Guid Id { get; init; }

    public required ReserveComponentType Component { get; init; }

    public required decimal CurrentAmount { get; init; }

    public required decimal PendingAmount { get; init; }

    public required bool HasPendingApproval { get; init; }

    public required ReserveComponentStatus Status { get; init; }
}

/// <summary>One ReserveHistory row (FRS §9.6), as returned by the reserve commands.</summary>
public sealed record ReserveTransactionDto(
    Guid Id,
    Guid ReserveComponentId,
    ReserveTransactionType TransactionType,
    decimal Amount,
    decimal PreviousBalance,
    decimal NewBalance,
    ReserveApprovalStatus ApprovalStatus,
    ApprovalAuthority RequiredAuthority,
    bool ExceedsAggregateLimit,
    string ChangeReason,
    int ChangeSequence,
    string IdempotencyKey,
    PostingStatus PostingStatus,
    Guid SubmittedByUserId,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    Guid? RejectedByUserId,
    DateTimeOffset? RejectedAt,
    string? RejectionReason);

/// <summary>
/// A submitted reserve transaction (POST /claims/{id}/reserves, the PUT alias, and the FNOL initial
/// reserve): the component, the new row with its approval status, and non-blocking warnings (BR-R-05).
/// </summary>
public sealed record ReserveSubmittedDto(ReserveComponentType Component, ReserveTransactionDto Transaction, IReadOnlyList<string> Warnings);

/// <summary>
/// GET /api/claims/{id}/reserves (FRS §10.2): the balance per component and the full transaction history,
/// plus the BR-R-05 figures the Reserves tab needs (D-11).
/// </summary>
public sealed record ClaimReservesDto
{
    public required Guid ClaimId { get; init; }

    /// <summary>In FRS §6.2 order: Indemnity, Expense, ALAE, SubrogationRecoverable.</summary>
    public required IReadOnlyList<ReserveComponentSummaryDto> Components { get; init; }

    /// <summary>Every transaction of the claim, newest first, whatever its status.</summary>
    public required IReadOnlyList<ReserveHistoryEntryDto> Transactions { get; init; }

    /// <summary>Net Σ CurrentAmount over all components, SubrogationRecoverable included (D-29).</summary>
    public required decimal TotalReserves { get; init; }

    /// <summary>Σ CurrentAmount of the cost components: the figure the $10,000,000 limit applies to (BR-R-05, D-11).</summary>
    public required decimal ApprovedAggregate { get; init; }

    public required decimal AggregateLimit { get; init; }

    public required bool ReserveLimitOverride { get; init; }
}

/// <summary>A row of the Reserves tab history table (FRS §11.3): the transaction with its component, date and the people involved.</summary>
public sealed record ReserveHistoryEntryDto
{
    public required Guid Id { get; init; }

    public required Guid ReserveComponentId { get; init; }

    public required ReserveComponentType Component { get; init; }

    public required ReserveTransactionType TransactionType { get; init; }

    public required decimal Amount { get; init; }

    public required decimal PreviousBalance { get; init; }

    public required decimal NewBalance { get; init; }

    public required ReserveApprovalStatus ApprovalStatus { get; init; }

    public required ApprovalAuthority RequiredAuthority { get; init; }

    public required bool ExceedsAggregateLimit { get; init; }

    public required string ChangeReason { get; init; }

    public required int ChangeSequence { get; init; }

    public required string IdempotencyKey { get; init; }

    public required PostingStatus PostingStatus { get; init; }

    public required string? PostingJobId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required Guid SubmittedByUserId { get; init; }

    public required string? SubmittedByName { get; init; }

    public required Guid? ApprovedByUserId { get; init; }

    public required string? ApprovedByName { get; init; }

    public required DateTimeOffset? ApprovedAt { get; init; }

    public required Guid? RejectedByUserId { get; init; }

    public required string? RejectedByName { get; init; }

    public required DateTimeOffset? RejectedAt { get; init; }

    public required string? RejectionReason { get; init; }
}
