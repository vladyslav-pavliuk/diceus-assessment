using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Persistence;

public static class DependencyInjection
{
    public const string ConnectionStringName = "ClaimsDb";

    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<ClaimsDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            }

            // Retries cover transient Azure SQL errors, including a serverless database resuming from
            // auto-pause (D-36). Explicit transactions must then run inside the execution strategy (Phase 2 UoW).
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
        });

        services.AddScoped<IUserRepository, UserRepository>();

        services.AddHealthChecks().AddDbContextCheck<ClaimsDbContext>("database", tags: [HealthCheckTags.Ready]);

        return services;
    }
}

public static class HealthCheckTags
{
    /// <summary>Dependencies that must be up before the API takes traffic (the readiness probe).</summary>
    public const string Ready = "ready";
}
