namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// Hides Hangfire from the Application layer. Only Infrastructure references Hangfire.
/// Enqueue only after the triggering transaction has committed, so a job never sees uncommitted
/// rows (CLAUDE.md rule 5). The commit-to-enqueue gap is covered by the GL sweeper job (D-15).
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>
    /// Enqueues PostGLReserveChangeJob (FRS §12.1) and returns the Hangfire job id, for logging. The job
    /// writes its own id to ReserveHistory.PostingJobId when it posts, because several jobs may exist for
    /// one transaction (a retry, the sweeper) and the one that posted is the one worth recording (D-41).
    /// </summary>
    string EnqueueGlPosting(Guid reserveHistoryId, Guid claimId, string idempotencyKey);
}
