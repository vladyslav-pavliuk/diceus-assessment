using ClaimsModule.Application.Abstractions;
using Hangfire;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>The Hangfire side of <see cref="IBackgroundJobScheduler"/>: BackgroundJob.Enqueue (FRS §12.1 "fire-and-forget").</summary>
internal sealed class HangfireBackgroundJobScheduler(IBackgroundJobClient client) : IBackgroundJobScheduler
{
    public string EnqueueGlPosting(Guid reserveHistoryId, Guid claimId, string idempotencyKey) =>
        client.Enqueue<PostGLReserveChangeJob>(job => job.ExecuteAsync(reserveHistoryId, claimId, idempotencyKey, null, CancellationToken.None));
}
