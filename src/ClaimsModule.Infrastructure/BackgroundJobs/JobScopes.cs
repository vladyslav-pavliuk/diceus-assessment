using ClaimsModule.Infrastructure.Correlation;
using ClaimsModule.Infrastructure.Tenancy;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs one step of a background job the way an HTTP request runs a command: in its own DI scope (so its own
/// DbContext and unit of work), for one organisation (D-31: jobs have no user, so they set the tenant
/// explicitly), with the job's correlation id on every audit row and log line (CLAUDE.md rule 6).
/// </summary>
public sealed class JobScopes(IServiceScopeFactory scopeFactory, ILogger<JobScopes> logger)
{
    public async Task<TResult> RunAsync<TResult>(
        Guid organisationId,
        Guid correlationId,
        Func<ISender, CancellationToken, Task<TResult>> step,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<TenantContext>().SetOrganisation(organisationId);
        services.GetRequiredService<CorrelationContext>().Set(correlationId.ToString());

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId.ToString(),
            ["OrganisationId"] = organisationId,
        }))
        {
            return await step(services.GetRequiredService<ISender>(), cancellationToken);
        }
    }
}
