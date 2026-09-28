using ClaimsModule.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>
/// One SQL Server 2022 container per test run, migrated from zero with the real migrations
/// (FRS §15.4), and one API host on top of it. Shared by every test in <see cref="ApiCollection"/>.
/// The empty database is created before the host starts, because Hangfire installs its own schema in it at
/// start-up (the recurring-job registration); EF migrations then add the application tables.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly SemaphoreSlim _jobsHostLock = new(1, 1);
    private JobsHost? _jobsHost;
    private string _connectionString = null!;

    public ClaimsApiFactory Factory { get; private set; } = null!;

    /// <summary>The host for the background-job tests, started on first use (its own database, a fake clock).</summary>
    public async Task<JobsHost> JobsHostAsync()
    {
        await _jobsHostLock.WaitAsync();
        try
        {
            return _jobsHost ??= await JobsHost.StartAsync(
                new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "ClaimsModuleJobTests" }.ConnectionString);
        }
        finally
        {
            _jobsHostLock.Release();
        }
    }

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();

        var connectionString = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
        {
            InitialCatalog = "ClaimsModuleTests",
        }.ConnectionString;

        _connectionString = connectionString;
        await TestDatabase.CreateEmptyDatabaseAsync(connectionString);
        Factory = new ClaimsApiFactory(connectionString);

        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_jobsHost is not null)
        {
            await _jobsHost.DisposeAsync();
        }

        _jobsHostLock.Dispose();
        await Factory.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
