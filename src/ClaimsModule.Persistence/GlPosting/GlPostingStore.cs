using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.GlPosting;

/// <summary>
/// Each move is one conditional UPDATE in the caller's transaction, so the row stays locked until the audit row commits.
/// A concurrent run blocks on the lock, then re-evaluates the WHERE clause against the committed row and changes nothing,
/// under READ_COMMITTED_SNAPSHOT too.
/// <para>
/// ExecuteUpdate skips the interceptors on purpose: the claim row is not touched, so a system posting neither resets the
/// SLA clock nor causes a user's edit to fail with 409. The convention columns are therefore set by hand.
/// </para>
/// </summary>
internal sealed class GlPostingStore(ClaimsDbContext dbContext, TimeProvider timeProvider) : IGlPostingStore
{
    public async Task<GlPostingTarget?> TryMarkPostedAsync(GlPostingRequest request, string? jobId, CancellationToken cancellationToken)
    {
        var changed = await Pending(request).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(transaction => transaction.PostingStatus, PostingStatus.Posted)
                .SetProperty(transaction => transaction.PostingJobId, jobId)
                .SetProperty(transaction => EF.Property<DateTimeOffset?>(transaction, ShadowColumns.UpdatedAt), timeProvider.GetUtcNow())
                .SetProperty(transaction => EF.Property<Guid?>(transaction, ShadowColumns.UserModified), (Guid?)null),
            cancellationToken);

        return changed == 1 ? await LoadTargetAsync(request.ReserveHistoryId, cancellationToken) : null;
    }

    public async Task<GlPostingTarget?> TryMarkFailedAsync(GlPostingRequest request, string? jobId, CancellationToken cancellationToken)
    {
        // Same predicate as posting, so Failed never overwrites Posted.
        var changed = await Pending(request).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(transaction => transaction.PostingStatus, PostingStatus.Failed)
                .SetProperty(transaction => transaction.PostingJobId, jobId)
                .SetProperty(transaction => EF.Property<DateTimeOffset?>(transaction, ShadowColumns.UpdatedAt), timeProvider.GetUtcNow())
                .SetProperty(transaction => EF.Property<Guid?>(transaction, ShadowColumns.UserModified), (Guid?)null),
            cancellationToken);

        return changed == 1 ? await LoadTargetAsync(request.ReserveHistoryId, cancellationToken) : null;
    }

    public async Task<IReadOnlyList<GlPostingRequest>> ListAwaitingPostingAsync(
        DateTimeOffset approvedBefore, int maxCount, CancellationToken cancellationToken) =>
        await dbContext.ReserveHistory.AsNoTracking()
            .Where(transaction => transaction.PostingStatus == PostingStatus.Pending
                && (transaction.ApprovalStatus == ReserveApprovalStatus.Approved || transaction.ApprovalStatus == ReserveApprovalStatus.AutoApproved)
                && transaction.ApprovedAt < approvedBefore)
            .OrderBy(transaction => transaction.ApprovedAt)
            .Take(maxCount)
            .Select(transaction => new GlPostingRequest(transaction.Id, transaction.ClaimId, transaction.IdempotencyKey))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// All three job arguments must match. Pending rather than "not Posted": a Failed posting is posted again only after
    /// the audited retry, so a stray duplicate job cannot bypass it (D-41).
    /// </summary>
    private IQueryable<ReserveTransaction> Pending(GlPostingRequest request) =>
        dbContext.ReserveHistory.Where(transaction =>
            transaction.Id == request.ReserveHistoryId
            && transaction.ClaimId == request.ClaimId
            && transaction.IdempotencyKey == request.IdempotencyKey
            && transaction.PostingStatus == PostingStatus.Pending
            && (transaction.ApprovalStatus == ReserveApprovalStatus.Approved || transaction.ApprovalStatus == ReserveApprovalStatus.AutoApproved));

    // Read after the UPDATE, inside the same transaction: the row is ours (exclusive lock) until commit.
    private Task<GlPostingTarget> LoadTargetAsync(Guid reserveHistoryId, CancellationToken cancellationToken) =>
        dbContext.ReserveHistory.AsNoTracking()
            .Where(transaction => transaction.Id == reserveHistoryId)
            .Join(
                dbContext.Set<ReserveComponent>(),
                transaction => transaction.ReserveComponentId,
                component => component.Id,
                (transaction, component) => new GlPostingTarget(
                    transaction.Id,
                    transaction.ClaimId,
                    transaction.IdempotencyKey,
                    component.Component,
                    transaction.Amount,
                    transaction.ChangeSequence))
            .SingleAsync(cancellationToken);
}
