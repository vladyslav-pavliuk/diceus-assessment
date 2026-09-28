using ClaimsModule.Infrastructure.Correlation;
using ClaimsModule.Infrastructure.Tenancy;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs a job step like an HTTP request runs a command: its own DI scope and unit of work, an explicit tenant (D-31),
/// and the job's correlation id on every audit row and log line.
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
