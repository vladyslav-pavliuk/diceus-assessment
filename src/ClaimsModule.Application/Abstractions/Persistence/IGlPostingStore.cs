using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// The GL posting state of ReserveHistory rows, for the GL jobs (FRS §12.1, D-15). The two status moves
/// are compare-and-set UPDATEs: one statement that both checks and writes, run inside the caller's unit of
/// work, so the audit row commits or rolls back with it. Never "read the status, then write" (ARCHITECTURE-PLAN
/// §6.1 R6, R8).
/// </summary>
public interface IGlPostingStore
{
    /// <summary>
    /// Pending → Posted, only for an approved transaction matching all three job arguments, and records the
    /// Hangfire job id. Returns the transaction when this call changed it, or null when there was nothing
    /// to post (already posted, failed, not approved, unknown). A concurrent caller for the same row waits
    /// for this transaction to finish and then gets null.
    /// </summary>
    Task<GlPostingTarget?> TryMarkPostedAsync(GlPostingRequest request, string? jobId, CancellationToken cancellationToken);

    /// <summary>
    /// Pending → Failed, after the job's last attempt (D-35). Returns the transaction when this call changed
    /// it, or null when it is no longer Pending (for example another run posted it meanwhile).
    /// </summary>
    Task<GlPostingTarget?> TryMarkFailedAsync(GlPostingRequest request, string? jobId, CancellationToken cancellationToken);

    /// <summary>
    /// Approved transactions still Pending that were approved before <paramref name="approvedBefore"/>: their
    /// enqueue may have been lost between COMMIT and enqueue (D-15). Oldest first, at most <paramref name="maxCount"/>.
    /// </summary>
    Task<IReadOnlyList<GlPostingRequest>> ListAwaitingPostingAsync(DateTimeOffset approvedBefore, int maxCount, CancellationToken cancellationToken);
}

/// <summary>The three arguments of PostGLReserveChangeJob (FRS §12.1).</summary>
public sealed record GlPostingRequest(Guid ReserveHistoryId, Guid ClaimId, string IdempotencyKey);

/// <summary>The transaction whose posting status a compare-and-set just changed.</summary>
public sealed record GlPostingTarget(
    Guid ReserveHistoryId,
    Guid ClaimId,
    string IdempotencyKey,
    ReserveComponentType Component,
    decimal Amount,
    int ChangeSequence);
