using ClaimsModule.Application.Abstractions;
using Hangfire;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

internal sealed class HangfireBackgroundJobScheduler(IBackgroundJobClient client) : IBackgroundJobScheduler
{
    public string EnqueueGlPosting(Guid reserveHistoryId, Guid claimId, string idempotencyKey) =>
        client.Enqueue<PostGLReserveChangeJob>(job => job.ExecuteAsync(reserveHistoryId, claimId, idempotencyKey, null, CancellationToken.None));
}
