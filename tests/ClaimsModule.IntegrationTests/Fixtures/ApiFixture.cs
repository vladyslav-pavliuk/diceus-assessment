using ClaimsModule.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Azurite;
using Testcontainers.MsSql;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>
/// One SQL Server 2022 container per test run, migrated from zero with the real migrations
/// (FRS §15.4), and one API host on top of it. Shared by every test in <see cref="ApiCollection"/>.
/// The empty database is created before the host starts, because Hangfire installs its own schema in it at
/// start-up (the recurring-job registration); EF migrations then add the application tables.
/// Azurite (the Azure Storage emulator) and an API host that stores documents in it are started on first use.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    // --skipApiVersionCheck: the Azure SDK may send a newer REST API version than the Azurite image knows.
    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
        .WithInMemoryPersistence()
        .WithCommand("--skipApiVersionCheck")
        .Build();

    private readonly SemaphoreSlim _jobsHostLock = new(1, 1);
    private readonly SemaphoreSlim _azuriteLock = new(1, 1);
    private JobsHost? _jobsHost;
    private ClaimsApiFactory? _azureBlobHost;
    private bool _azuriteStarted;
    private string _connectionString = null!;

    /// <summary>The Azurite connection string (account key), starting the container on first use.</summary>
    public async Task<string> AzuriteConnectionStringAsync()
    {
        await _azuriteLock.WaitAsync();
        try
        {
            if (!_azuriteStarted)
            {
                await _azurite.StartAsync();
                _azuriteStarted = true;
            }

            return _azurite.GetConnectionString();
        }
        finally
        {
            _azuriteLock.Release();
        }
    }

    /// <summary>An API host on the same database whose Storage:Provider is AzureBlob, against Azurite.</summary>
    public async Task<ClaimsApiFactory> AzureBlobHostAsync()
    {
        var azurite = await AzuriteConnectionStringAsync();
        await _azuriteLock.WaitAsync();
        try
        {
            return _azureBlobHost ??= new AzureBlobApiFactory(_connectionString, azurite);
        }
        finally
        {
            _azuriteLock.Release();
        }
    }

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

        if (_azureBlobHost is not null)
        {
            await _azureBlobHost.DisposeAsync();
        }

        await _azurite.DisposeAsync();
        _azuriteLock.Dispose();
        _jobsHostLock.Dispose();
        await Factory.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }
}

/// <summary>The API with Storage:Provider = AzureBlob against Azurite (BR-D-03: the provider is chosen by configuration alone).</summary>
internal sealed class AzureBlobApiFactory(string connectionString, string azuriteConnectionString) : ClaimsApiFactory(connectionString)
{
    protected override void ConfigureSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:Provider", "AzureBlob");
        builder.UseSetting("Storage:AzureBlob:ConnectionString", azuriteConnectionString);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
