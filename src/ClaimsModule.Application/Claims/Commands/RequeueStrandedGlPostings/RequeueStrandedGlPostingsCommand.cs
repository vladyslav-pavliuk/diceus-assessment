using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Application.Claims.Commands.RequeueStrandedGlPostings;

/// <summary>
/// The body of GlPostingSweeperJob (D-15), for the current tenant: re-enqueues the GL job for approved
/// transactions still Pending some minutes after approval. Their enqueue may have been lost between COMMIT
/// and the after-commit enqueue (a crash, Hangfire storage briefly unavailable; ARCHITECTURE-PLAN §6.1 R4).
/// ReserveHistory itself is the outbox: PostingStatus = Pending already says "needs posting". A duplicate enqueue
/// is harmless, because the job is idempotent (R6). Returns how many jobs were enqueued.
/// </summary>
public sealed record RequeueStrandedGlPostingsCommand : ICommand<int>
{
    /// <summary>Long enough for the normal after-commit enqueue and the job's own retries (10 + 30 + 60 s) to have finished.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    /// <summary>A bound per run and tenant; the next run picks up the rest.</summary>
    public const int MaxPerRun = 100;
}

internal sealed class RequeueStrandedGlPostingsCommandHandler(
    IGlPostingStore postings,
    IBackgroundJobScheduler scheduler,
    TimeProvider timeProvider,
    ILogger<RequeueStrandedGlPostingsCommandHandler> logger) : IRequestHandler<RequeueStrandedGlPostingsCommand, int>
{
    public async Task<int> Handle(RequeueStrandedGlPostingsCommand request, CancellationToken cancellationToken)
    {
        var approvedBefore = timeProvider.GetUtcNow() - RequeueStrandedGlPostingsCommand.GracePeriod;
        var stranded = await postings.ListAwaitingPostingAsync(approvedBefore, RequeueStrandedGlPostingsCommand.MaxPerRun, cancellationToken);

        foreach (var posting in stranded)
        {
            var jobId = scheduler.EnqueueGlPosting(posting.ReserveHistoryId, posting.ClaimId, posting.IdempotencyKey);
            logger.LogWarning(
                "GL posting {IdempotencyKey} was still pending after {GracePeriod}; re-enqueued as job {JobId}",
                posting.IdempotencyKey,
                RequeueStrandedGlPostingsCommand.GracePeriod,
                jobId);
        }

        return stranded.Count;
    }
}
