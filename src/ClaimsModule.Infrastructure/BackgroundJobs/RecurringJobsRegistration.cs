using Hangfire;
using Microsoft.Extensions.Hosting;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// AddOrUpdate is idempotent, so every start and replica writes the same definitions. While the API is scaled to zero
/// nothing runs; on wake, Hangfire runs a missed occurrence once (D-36).
/// </summary>
internal sealed class RecurringJobsRegistration(IRecurringJobManager recurringJobs) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        recurringJobs.AddOrUpdate<SlaMonitoringJob>(
            SlaMonitoringJob.RecurringJobId, job => job.RunAsync(CancellationToken.None), SlaMonitoringJob.Schedule);

        recurringJobs.AddOrUpdate<GlPostingSweeperJob>(
            GlPostingSweeperJob.RecurringJobId, job => job.RunAsync(CancellationToken.None), GlPostingSweeperJob.Schedule);

        recurringJobs.AddOrUpdate<IdempotencyCleanupJob>(
            IdempotencyCleanupJob.RecurringJobId, job => job.RunAsync(CancellationToken.None), IdempotencyCleanupJob.Schedule);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
