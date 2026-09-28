using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims.Commands.DetectSlaBreaches;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// SlaMonitoringJob (FRS §12.2, Brief §3.5): every 15 minutes, for each organisation, records an
/// SLA_BREACH_DETECTED entry for Draft/Open claims not updated for more than 48 hours, at most once per claim
/// per 24 hours (<see cref="DetectSlaBreachesCommand"/>). It never changes a claim (D-01).
/// <para>
/// <see cref="DisableConcurrentExecutionAttribute"/> takes a distributed lock in Hangfire's SQL storage, so two
/// runs never overlap (a slow run, or two replicas): otherwise both could see "no breach in the last 24 hours"
/// for the same claim and both record one (ARCHITECTURE-PLAN §6.1 R11). No automatic retry: the next run, 15
/// minutes later, applies the same state-based rule, and a failed run stays visible in the dashboard.
/// </para>
/// </summary>
public sealed class SlaMonitoringJob(ITenantDirectory tenants, JobScopes jobScopes, ILogger<SlaMonitoringJob> logger)
{
    public const string RecurringJobId = "sla-monitoring";

    /// <summary>FRS §12.2: '*/15 * * * *'.</summary>
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
