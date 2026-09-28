namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// Call only after commit, so a job never sees uncommitted rows. The GL sweeper covers a lost enqueue (D-15).
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>
    /// Returns the job id for logging only. The job records its own id when it posts, because a retry or the
    /// sweeper may create several jobs per transaction (D-41).
    /// </summary>
    string EnqueueGlPosting(Guid reserveHistoryId, Guid claimId, string idempotencyKey);
}
