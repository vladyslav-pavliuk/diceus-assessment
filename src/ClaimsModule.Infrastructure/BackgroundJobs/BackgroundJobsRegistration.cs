using ClaimsModule.Application.Abstractions;
using ClaimsModule.Infrastructure.Ledger;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

internal static class BackgroundJobsRegistration
{
    /// <summary>The application database: Hangfire keeps its tables there too, in the [HangFire] schema (D-36).</summary>
    private const string ConnectionStringName = "ClaimsDb";

    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JobsOptions>().Bind(configuration.GetSection(JobsOptions.SectionName));

        // Registered before AddHangfire, which only adds a JobStorage when none exists. Every Hangfire service then
        // takes this instance from DI instead of the process-wide JobStorage.Current, so two hosts in one process
        // (the integration tests) never share storage by accident.
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
