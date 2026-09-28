using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims.Commands.DetectSlaBreaches;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// The distributed lock stops overlapping runs (a slow run, or two replicas) from both recording the same breach. No
/// automatic retry: the next run applies the same state-based rule.
/// </summary>
public sealed class SlaMonitoringJob(ITenantDirectory tenants, JobScopes jobScopes, ILogger<SlaMonitoringJob> logger)
{
    public const string RecurringJobId = "sla-monitoring";

    public const string Schedule = "*/15 * * * *";

    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var breaches = 0;

        foreach (var organisationId in await tenants.ListOrganisationIdsAsync(cancellationToken))
        {
            breaches += await jobScopes.RunAsync(
                organisationId, correlationId, (sender, token) => sender.Send(new DetectSlaBreachesCommand(), token), cancellationToken);
        }

        logger.LogInformation("SLA monitoring recorded {BreachCount} breaches", breaches);
        return breaches;
    }
}
