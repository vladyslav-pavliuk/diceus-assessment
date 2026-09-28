using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Persistence.ClaimNumbers;
using ClaimsModule.Persistence.GlPosting;
using ClaimsModule.Persistence.Idempotency;
using ClaimsModule.Persistence.Interceptors;
using ClaimsModule.Persistence.ReadModels;
using ClaimsModule.Persistence.Repositories;
using ClaimsModule.Persistence.Tenancy;
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

            // Covers a serverless database resuming from auto-pause (D-36), so explicit transactions run inside the
            // execution strategy.
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());

            // The immutability guard must see a delete before it becomes a soft delete.
            options.AddInterceptors(
                provider.GetRequiredService<ImmutableRowsInterceptor>(),
                provider.GetRequiredService<AuditColumnsInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IClaimNumberGenerator, ClaimNumberGenerator>();
        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IStatusTransitionRepository, StatusTransitionRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IGlPostingStore, GlPostingStore>();
        services.AddScoped<ITenantDirectory, TenantDirectory>();

        services.AddScoped<IClaimQueries, ClaimQueries>();
        services.AddScoped<IReferenceDataQueries, ReferenceDataQueries>();
        services.AddScoped<IPolicyQueries, PolicyQueries>();
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<ISlaMonitoringQueries, SlaMonitoringQueries>();

        services.AddHealthChecks().AddDbContextCheck<ClaimsDbContext>("database", tags: [HealthCheckTags.Ready]);

        return services;
    }
}

public static class HealthCheckTags
{
    /// <summary>For the readiness probe.</summary>
    public const string Ready = "ready";
}
