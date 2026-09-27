using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Users;
using ClaimsModule.Infrastructure.Correlation;
using ClaimsModule.Infrastructure.Tenancy;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>
/// Direct access to the migrated test database through the API's own DI container, the way a
/// background job uses it: a scope with an explicit tenant and correlation id (D-31).
/// </summary>
internal sealed class TestDatabase(ApiFixture fixture)
{
    public IServiceProvider Services => fixture.Factory.Services;

    public const string SeededOrganisationName = "Demo Insurance Company";

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
}
