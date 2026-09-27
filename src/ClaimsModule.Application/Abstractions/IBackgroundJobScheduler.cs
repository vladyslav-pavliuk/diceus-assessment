namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// Hides Hangfire from the Application layer. Only Infrastructure references Hangfire.
/// Enqueue only after the triggering transaction has committed, so a job never sees uncommitted
/// rows (CLAUDE.md rule 5). The commit-to-enqueue gap is covered by the GL sweeper job (D-15).
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>
    /// Enqueues PostGLReserveChangeJob (FRS §12.1) and returns the job id, which is stored in
    /// ReserveHistory.PostingJobId.
    /// </summary>
    string EnqueueGlPosting(Guid reserveHistoryId, Guid claimId, string idempotencyKey);
}
