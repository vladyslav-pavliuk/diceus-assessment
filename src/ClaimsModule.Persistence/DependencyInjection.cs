using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Persistence.ClaimNumbers;
using ClaimsModule.Persistence.Interceptors;
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
        services.AddSingleton<ImmutableRowsInterceptor>();
        services.AddScoped<AuditColumnsInterceptor>();

        services.AddDbContext<ClaimsDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            }

            // Retries cover transient Azure SQL errors, including a serverless database resuming from
            // auto-pause (D-36). Explicit transactions therefore run inside the execution strategy (UnitOfWork).
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());

            // Order matters: the immutability guard must see a delete before it becomes a soft delete.
            options.AddInterceptors(
                provider.GetRequiredService<ImmutableRowsInterceptor>(),
                provider.GetRequiredService<AuditColumnsInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IClaimNumberGenerator, ClaimNumberGenerator>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
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
