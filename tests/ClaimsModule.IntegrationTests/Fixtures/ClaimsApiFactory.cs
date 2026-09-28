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
/// the <see cref="ConcurrencyGate"/> hook for the concurrency tests, and <see cref="FailingDocumentCommit"/>.
/// Documents go to the Development provider (the local file system) under <see cref="UploadsRoot"/>, a temporary
/// directory of this host; a derived host may switch to Azure Blob Storage with configuration.
/// </summary>
public class ClaimsApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public string ConnectionString => connectionString;

    public string UploadsRoot { get; } = Path.Combine(Path.GetTempPath(), "claims-module-tests", Guid.NewGuid().ToString("N"));

    public ControllableLedger Ledger => Services.GetRequiredService<ControllableLedger>();

    public ConcurrencyGate Gate => Services.GetRequiredService<ConcurrencyGate>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ClaimsDb", connectionString);

        // No Hangfire server: jobs are enqueued to the real storage, and tests that need a job run it themselves.
        builder.UseSetting("Jobs:RunServer", "false");
        builder.UseSetting("Storage:LocalFileSystem:RootPath", UploadsRoot);
        ConfigureSettings(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);

            services.AddSingleton<ControllableLedger>();
            services.RemoveAll<IGeneralLedger>();
            services.AddSingleton<IGeneralLedger>(provider => provider.GetRequiredService<ControllableLedger>());

            services.AddSingleton<ConcurrencyGate>();
            services.AddScoped<IBeforeCommitHandler<ReserveApproved>, ConcurrencyGateHandler>();
            services.AddScoped<IBeforeCommitHandler<ReserveTransactionSubmitted>, ConcurrencyGateHandler>();
            services.AddScoped<IBeforeCommitHandler<DocumentUploaded>, FailingDocumentCommit>();

            ConfigureTestServices(services);
        });
    }

    /// <summary>Extra replacements for a derived host.</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    /// <summary>Extra configuration for a derived host.</summary>
    protected virtual void ConfigureSettings(IWebHostBuilder builder)
    {
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(UploadsRoot))
        {
            Directory.Delete(UploadsRoot, recursive: true);
        }
    }
}

/// <summary>
/// Makes the document unit of work fail before commit when the file name starts with <see cref="Prefix"/>, the way a
/// database error at commit would (DOC-09): the metadata transaction rolls back after the blob was written.
/// </summary>
public sealed class FailingDocumentCommit : IBeforeCommitHandler<DocumentUploaded>
{
    public const string Prefix = "fail-commit";

    public Task HandleAsync(DocumentUploaded domainEvent, CancellationToken cancellationToken) =>
        domainEvent.DocumentName.StartsWith(Prefix, StringComparison.Ordinal)
            ? throw new InvalidOperationException("Simulated commit failure.")
            : Task.CompletedTask;
}
