using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// Status moves are compare-and-set UPDATEs that check and write in one statement, inside the caller's unit of
/// work, so the audit row commits with them. Never read-then-write.
/// </summary>
public interface IGlPostingStore
{
    /// <summary>
    /// Pending → Posted for an approved transaction matching all three arguments. Null when this call changed
    /// nothing; a concurrent caller for the same row blocks, then gets null.
    /// </summary>
    Task<GlPostingTarget?> TryMarkPostedAsync(GlPostingRequest request, string? jobId, CancellationToken cancellationToken);

    /// <summary>Pending → Failed after the last attempt (D-35). Null when it is no longer Pending.</summary>
    Task<GlPostingTarget?> TryMarkFailedAsync(GlPostingRequest request, string? jobId, CancellationToken cancellationToken);

    /// <summary>Approved but still Pending: the enqueue may have been lost after commit (D-15). Oldest first.</summary>
    Task<IReadOnlyList<GlPostingRequest>> ListAwaitingPostingAsync(DateTimeOffset approvedBefore, int maxCount, CancellationToken cancellationToken);
}

public sealed record GlPostingRequest(Guid ReserveHistoryId, Guid ClaimId, string IdempotencyKey);

public sealed record GlPostingTarget(
    Guid ReserveHistoryId,
    Guid ClaimId,
    string IdempotencyKey,
    ReserveComponentType Component,
    decimal Amount,
    int ChangeSequence);
