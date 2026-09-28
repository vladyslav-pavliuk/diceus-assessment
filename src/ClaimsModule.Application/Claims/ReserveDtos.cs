using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Claims;


public sealed record ReserveComponentSummaryDto
{
    public required Guid Id { get; init; }

    public required ReserveComponentType Component { get; init; }

    public required decimal CurrentAmount { get; init; }

    public required decimal PendingAmount { get; init; }

    public required bool HasPendingApproval { get; init; }

    public required ReserveComponentStatus Status { get; init; }
}

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

/// <summary>Warnings are non-blocking (BR-R-05).</summary>
public sealed record ReserveSubmittedDto(ReserveComponentType Component, ReserveTransactionDto Transaction, IReadOnlyList<string> Warnings);

public sealed record ClaimReservesDto
{
    public required Guid ClaimId { get; init; }

    /// <summary>In FRS §6.2 order.</summary>
    public required IReadOnlyList<ReserveComponentSummaryDto> Components { get; init; }

    /// <summary>Newest first, every status.</summary>
    public required IReadOnlyList<ReserveHistoryEntryDto> Transactions { get; init; }

    /// <summary>Net of all components, subrogation included (D-29).</summary>
    public required decimal TotalReserves { get; init; }

    /// <summary>Cost components only: the figure the $10,000,000 limit applies to (BR-R-05, D-11).</summary>
    public required decimal ApprovedAggregate { get; init; }

    public required decimal AggregateLimit { get; init; }

    public required bool ReserveLimitOverride { get; init; }
}

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
