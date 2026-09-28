using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Domain.Claims.Events;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Application.Claims.Jobs;

/// <summary>
/// After commit, so the job never sees uncommitted rows or runs for a rolled-back approval. A failed enqueue is picked
/// up later by the GL sweeper (D-15).
/// </summary>
internal sealed class GlPostingEnqueuer(IBackgroundJobScheduler scheduler, ILogger<GlPostingEnqueuer> logger) :
    IAfterCommitHandler<ReserveAutoApproved>,
    IAfterCommitHandler<ReserveApproved>,
    IAfterCommitHandler<GlPostingRetryRequested>
{
    public Task HandleAsync(ReserveAutoApproved domainEvent, CancellationToken cancellationToken) =>
        Enqueue(domainEvent.TransactionId, domainEvent.ClaimId, domainEvent.IdempotencyKey);

    public Task HandleAsync(ReserveApproved domainEvent, CancellationToken cancellationToken) =>
        Enqueue(domainEvent.TransactionId, domainEvent.ClaimId, domainEvent.IdempotencyKey);

    public Task HandleAsync(GlPostingRetryRequested domainEvent, CancellationToken cancellationToken) =>
        Enqueue(domainEvent.TransactionId, domainEvent.ClaimId, domainEvent.IdempotencyKey);

    private Task Enqueue(Guid reserveHistoryId, Guid claimId, string idempotencyKey)
    {
        var jobId = scheduler.EnqueueGlPosting(reserveHistoryId, claimId, idempotencyKey);
        logger.LogInformation("GL posting {IdempotencyKey} enqueued as job {JobId}", idempotencyKey, jobId);
        return Task.CompletedTask;
    }
}
