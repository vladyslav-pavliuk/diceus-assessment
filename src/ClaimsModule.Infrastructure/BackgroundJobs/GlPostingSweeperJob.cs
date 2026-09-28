using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims.Commands.RequeueStrandedGlPostings;
using Hangfire;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Makes GL posting at-least-once without an outbox table; the job's compare-and-set makes it exactly-once in effect (D-15).
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
