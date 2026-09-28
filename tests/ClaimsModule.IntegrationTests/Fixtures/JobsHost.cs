using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>
/// A second API host for the background-job tests: its own database in the same SQL Server container, and a
/// <see cref="FakeTimeProvider"/> the tests move forward (48h, 24h, 5 min rules). Jobs are run directly, never
/// through HTTP, because a JWT validated against a clock two days ahead would be meaningless. Its own database
/// keeps the jobs from seeing, and time-travelling, the claims of the HTTP tests.
/// </summary>
public sealed class JobsHost : IAsyncDisposable
{
    private JobsHost(JobsApiFactory factory, FakeTimeProvider clock)
    {
        Factory = factory;
        Clock = clock;
    }

    public ClaimsApiFactory Factory { get; }

    public FakeTimeProvider Clock { get; }

    public IServiceProvider Services => Factory.Services;

    public ControllableLedger Ledger => Factory.Ledger;

    internal TestDatabase Database => new(() => Factory.Services);

    public static async Task<JobsHost> StartAsync(string connectionString)
    {
        await TestDatabase.CreateEmptyDatabaseAsync(connectionString);

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var factory = new JobsApiFactory(connectionString, clock);

        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Database.MigrateAsync();

        return new JobsHost(factory, clock);
    }

    public ValueTask DisposeAsync() => Factory.DisposeAsync();

    private sealed class JobsApiFactory(string connectionString, FakeTimeProvider clock) : ClaimsApiFactory(connectionString)
    {
        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }
    }
}
