using ClaimsModule.Application.Abstractions;
using ClaimsModule.Infrastructure.Ledger;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

internal static class BackgroundJobsRegistration
{
    /// <summary>Hangfire shares the application database, in its own schema (D-36).</summary>
    private const string ConnectionStringName = "ClaimsDb";

    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JobsOptions>().Bind(configuration.GetSection(JobsOptions.SectionName));

        // Registered before AddHangfire, so Hangfire takes this instance instead of the process-wide JobStorage.Current
        // and two hosts in one process (the integration tests) never share storage.
        services.AddSingleton<JobStorage>(provider =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            }

            // Hangfire creates and upgrades its own schema on first use; EF migrations own the application tables.
            return new SqlServerStorage(connectionString, new SqlServerStorageOptions { PrepareSchemaIfNecessary = true });
        });

        services.AddHangfire((provider, hangfire) => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(provider.GetRequiredService<JobStorage>()));

        if (configuration.GetSection(JobsOptions.SectionName).Get<JobsOptions>()?.RunServer ?? true)
        {
            services.AddHangfireServer();
        }

        services.AddHostedService<RecurringJobsRegistration>();

        services.AddSingleton<JobScopes>();
        services.AddScoped<PostGLReserveChangeJob>();
        services.AddScoped<SlaMonitoringJob>();
        services.AddScoped<GlPostingSweeperJob>();
        services.AddScoped<IdempotencyCleanupJob>();

        services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();
        services.AddSingleton<IGeneralLedger, SimulatedGeneralLedger>();

        return services;
    }
}
