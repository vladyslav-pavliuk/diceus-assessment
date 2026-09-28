using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Application.Claims.Commands.RequeueStrandedGlPostings;

/// <summary>
/// Recovers enqueues lost between commit and the after-commit handler (D-15). ReserveHistory is the outbox, since
/// PostingStatus = Pending already means "needs posting", and a duplicate enqueue is harmless because the job is idempotent.
/// </summary>
public sealed record RequeueStrandedGlPostingsCommand : ICommand<int>
{
    /// <summary>Outlasts the normal enqueue plus the job's own retries (10 + 30 + 60 s).</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    /// <summary>Per run and tenant; the next run picks up the rest.</summary>
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
