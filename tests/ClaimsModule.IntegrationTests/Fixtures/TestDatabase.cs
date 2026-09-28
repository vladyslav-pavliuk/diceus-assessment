using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Domain.Users;
using ClaimsModule.Infrastructure.Correlation;
using ClaimsModule.Infrastructure.Tenancy;
using ClaimsModule.Persistence;
using ClaimsModule.Persistence.Conventions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>
/// Direct access to the migrated test database through the API's own DI container, the way a
/// background job uses it: a scope with an explicit tenant and correlation id (D-31).
/// </summary>
internal sealed class TestDatabase(Func<IServiceProvider> services)
{
    public TestDatabase(ApiFixture fixture)
        : this(() => fixture.Factory.Services)
    {
    }

    public IServiceProvider Services => services();

    public const string SeededOrganisationName = "Demo Insurance Company";

    /// <summary>Creates the database named in the connection string, empty.</summary>
    public static async Task CreateEmptyDatabaseAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{database}]";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// The seeded organisation, read from the database rather than from the seed code. Looked up by name,
    /// because the tenant-isolation tests add other organisations to the shared database.
    /// </summary>
    public async Task<Guid> SeededOrganisationIdAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Organisations
            .Where(organisation => organisation.Name == SeededOrganisationName)
            .Select(organisation => organisation.Id)
            .SingleAsync();
    }

    public async Task<AsyncServiceScope> TenantScopeAsync(Guid? organisationId = null)
    {
        var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetOrganisation(organisationId ?? await SeededOrganisationIdAsync());
        scope.ServiceProvider.GetRequiredService<CorrelationContext>().Set(Guid.NewGuid().ToString());
        return scope;
    }

    public async Task<Actor> SeededUserAsync(string username)
    {
        await using var scope = await TenantScopeAsync();
        var user = await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Users.SingleAsync(candidate => candidate.Username == username);
        return new Actor(user.Id, user.Role);
    }

    /// <summary>Creates a claim the way FNOL will: number drawn and claim inserted in one unit of work.</summary>
    public async Task<Claim> CreateClaimAsync(int year, string policyNumber = "POL-2025-003001", Func<Claim, Task>? beforeCommit = null)
    {
        var handler = await SeededUserAsync("handler.alex");
        await using var scope = await TenantScopeAsync();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ClaimsDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();

        return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            async cancellationToken =>
            {
                var policy = await dbContext.Policies.SingleAsync(candidate => candidate.PolicyNumber == policyNumber, cancellationToken);
                var number = await services.GetRequiredService<IClaimNumberGenerator>().NextAsync(year, cancellationToken);

                var claim = Claim.Create(
                    number,
                    policy,
                    new LossEventDetails(now.AddDays(-1), "Van reversed into a loading bay bollard.", "Depot 4", "COL-VEH-COL", 4_000m, null),
                    ClaimSeverity.Standard,
                    [new PartyDetails(PartyRole.Claimant, PartyType.Company, null, null, "Northwind Logistics Ltd", "claims@northwind.example", null, null)],
                    [new RiskObjectDetails(AssetType.Vehicle, "2021 Ford Transit", "Rear bumper", "WF0XXXTTGXKA00001")],
                    handler,
                    now);

                services.GetRequiredService<IClaimRepository>().Add(claim);
                if (beforeCommit is not null)
                {
                    await beforeCommit(claim);
                }

                return claim;
            },
            CancellationToken.None);
    }

    /// <summary>Runs <paramref name="change"/> on the loaded claim in one unit of work, as a command handler would.</summary>
    public async Task<TResult> ChangeClaimAsync<TResult>(Guid claimId, Func<Claim, IServiceProvider, Task<TResult>> change)
    {
        await using var scope = await TenantScopeAsync();
        var services = scope.ServiceProvider;

        return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            async cancellationToken =>
            {
                var claim = await services.GetRequiredService<IClaimRepository>().GetAsync(claimId, cancellationToken)
                    ?? throw new InvalidOperationException($"Claim {claimId} not found.");
                return await change(claim, services);
            },
            CancellationToken.None);
    }

    /// <summary>Submits a reserve transaction through the aggregate; ≤ $10,000 is auto-approved and enqueues the GL job after commit.</summary>
    public async Task<ReserveTransaction> SubmitReserveAsync(
        Guid claimId, ReserveComponentType component, decimal amount, string username = "handler.alex")
    {
        var submitter = await SeededUserAsync(username);
        return await ChangeClaimAsync(claimId, (claim, services) => Task.FromResult(
            claim.SubmitReserveTransaction(
                component, null, amount, "Test reserve.", submitter, services.GetRequiredService<TimeProvider>().GetUtcNow()).Transaction));
    }

    public async Task ChangeStatusAsync(Guid claimId, ClaimStatus target, string username = "handler.alex")
    {
        var actor = await SeededUserAsync(username);
        await ChangeClaimAsync(claimId, async (claim, services) =>
        {
            var table = await services.GetRequiredService<IStatusTransitionRepository>().GetTableAsync(CancellationToken.None);
            claim.ChangeStatus(target, null, null, actor, table, services.GetRequiredService<TimeProvider>().GetUtcNow());
            return true;
        });
    }

    public async Task<ReserveTransaction> ReserveTransactionAsync(Guid transactionId)
    {
        await using var scope = await TenantScopeAsync();
        return await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().ReserveHistory.AsNoTracking()
            .SingleAsync(transaction => transaction.Id == transactionId);
    }

    public async Task<IReadOnlyList<ClaimAuditLog>> AuditAsync(Guid claimId, string? eventType = null)
    {
        await using var scope = await TenantScopeAsync();
        return await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().ClaimAuditLog.AsNoTracking()
            .Where(entry => entry.ClaimId == claimId && (eventType == null || entry.EventType == eventType))
            .OrderBy(entry => entry.CreatedAt).ThenBy(entry => entry.Id)
            .ToListAsync();
    }

    /// <summary>The claim row's concurrency and SLA columns: RowVer, UpdatedAt and Status.</summary>
    public async Task<(byte[] RowVer, DateTimeOffset? UpdatedAt, ClaimStatus Status)> ClaimRowAsync(Guid claimId)
    {
        await using var scope = await TenantScopeAsync();
        var row = await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Claims.AsNoTracking()
            .Where(claim => claim.Id == claimId)
            .Select(claim => new
            {
                RowVer = EF.Property<byte[]>(claim, ShadowColumns.RowVer),
                UpdatedAt = EF.Property<DateTimeOffset?>(claim, ShadowColumns.UpdatedAt),
                claim.Status,
            })
            .SingleAsync();
        return (row.RowVer, row.UpdatedAt, row.Status);
    }
}
