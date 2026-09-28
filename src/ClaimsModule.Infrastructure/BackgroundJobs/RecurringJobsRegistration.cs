using Hangfire;
using Microsoft.Extensions.Hosting;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Registers the recurring jobs when the application starts (FRS §12 "Both must be registered on application
/// startup"). AddOrUpdate is idempotent, so every start (and every replica) writes the same definitions. Times are
/// UTC. While Container Apps has scaled the API to zero, nothing runs; on wake, Hangfire runs a missed occurrence
/// once (D-36).
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
