using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Domain.Claims.Events;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Application.Claims.Jobs;

/// <summary>
/// Enqueues PostGLReserveChangeJob once an approval has committed (FRS §6.3, §6.4 step 8; CLAUDE.md rule 5):
/// auto-approval, manual approval, and a retry of a failed posting. After commit, so the job never sees
/// uncommitted rows and never runs for an approval that rolled back (ARCHITECTURE-PLAN §6.1 R5). If the
/// enqueue itself fails, the request still succeeds (the change is committed) and the GL sweeper re-enqueues
/// the posting later (D-15, R4).
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
