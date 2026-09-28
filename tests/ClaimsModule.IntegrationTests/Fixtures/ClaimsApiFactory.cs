using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Domain.Claims.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the real API (Program.cs) in memory, in the Development environment, against the test
/// database. Adds <see cref="ProbeController"/> so the cross-cutting pipeline can be exercised
/// without production test endpoints, a <see cref="ControllableLedger"/> in place of the simulated one,
/// and the <see cref="ConcurrencyGate"/> hook for the concurrency tests.
/// </summary>
public class ClaimsApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public string ConnectionString => connectionString;

    public ControllableLedger Ledger => Services.GetRequiredService<ControllableLedger>();

    public ConcurrencyGate Gate => Services.GetRequiredService<ConcurrencyGate>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ClaimsDb", connectionString);

        // No Hangfire server: jobs are enqueued to the real storage, and tests that need a job run it themselves.
        builder.UseSetting("Jobs:RunServer", "false");

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);

            services.AddSingleton<ControllableLedger>();
            services.RemoveAll<IGeneralLedger>();
            services.AddSingleton<IGeneralLedger>(provider => provider.GetRequiredService<ControllableLedger>());

            services.AddSingleton<ConcurrencyGate>();
            services.AddScoped<IBeforeCommitHandler<ReserveApproved>, ConcurrencyGateHandler>();
            services.AddScoped<IBeforeCommitHandler<ReserveTransactionSubmitted>, ConcurrencyGateHandler>();

            ConfigureTestServices(services);
        });
    }

    /// <summary>Extra replacements for a derived host.</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }
}
