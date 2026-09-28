using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.GlPosting;

/// <summary>
/// Compare-and-set moves of ReserveHistory.PostingStatus for the GL jobs (FRS §12.1, ARCHITECTURE-PLAN §6.1).
/// <para>
/// Each move is ONE conditional UPDATE: the WHERE clause is the check and the SET is the write, so there is no
/// window between them. It runs in the caller's unit of work (EF enlists ExecuteUpdate in the open transaction),
/// so the row keeps its exclusive lock until the audit row is committed with it. A concurrent run of the same job
/// blocks on that lock; when it resumes, SQL Server evaluates the WHERE clause against the committed row, finds
/// PostingStatus is no longer Pending, and changes 0 rows. That holds under READ COMMITTED and under
/// READ_COMMITTED_SNAPSHOT (Azure SQL's default): an UPDATE never writes through a stale snapshot.
/// </para>
/// <para>
/// ExecuteUpdate bypasses the change tracker and its interceptors on purpose: the claim row is not touched, so a
/// system posting neither resets the SLA clock nor makes a user's concurrent edit fail with 409 (R10). The
/// UpdatedAt/UserModified convention columns are therefore set here by hand (system actor = null, D-33).
/// The tenant query filter still applies, and the job has set its tenant scope (D-31).
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
        // Same predicate as posting: Failed never overwrites Posted (R8).
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
    /// The row the job was enqueued for, if it is an approved transaction still waiting to be posted. All three
    /// job arguments must match, so a job can only ever act on the exact change it was created for. Pending,
    /// not "anything but Posted": a Failed posting is posted again only after the audited user retry puts it back
    /// to Pending, so a stray duplicate job cannot bypass that step (R9, D-41).
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
