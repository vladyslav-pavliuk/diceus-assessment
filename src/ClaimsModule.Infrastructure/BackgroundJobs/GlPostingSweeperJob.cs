using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims.Commands.RequeueStrandedGlPostings;
using Hangfire;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// GlPostingSweeperJob (D-15): every 5 minutes, re-enqueues GL postings whose after-commit enqueue was lost
/// (ARCHITECTURE-PLAN §6.1 R4), per organisation (<see cref="RequeueStrandedGlPostingsCommand"/>). This makes GL
/// posting at-least-once without an outbox table; the GL job's compare-and-set makes it exactly-once in effect.
/// </summary>
public sealed class GlPostingSweeperJob(ITenantDirectory tenants, JobScopes jobScopes)
{
    public const string RecurringJobId = "gl-posting-sweeper";
    public const string Schedule = "*/5 * * * *";

    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var requeued = 0;

        foreach (var organisationId in await tenants.ListOrganisationIdsAsync(cancellationToken))
        {
            requeued += await jobScopes.RunAsync(
                organisationId, correlationId, (sender, token) => sender.Send(new RequeueStrandedGlPostingsCommand(), token), cancellationToken);
        }

        return requeued;
    }
}
