using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.IntegrationTests.Fixtures;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Persistence;

/// <summary>
/// BR-C-04 / FRS §5.3 / D-10. Each test uses its own year, so the counters of different tests never meet
/// in the shared database.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ClaimNumberGeneratorTests(ApiFixture fixture)
{
    private readonly TestDatabase _database = new(fixture);

    [Fact]
    public async Task BR_C_04_50_parallel_creates_yield_unique_gap_free_numbers()
    {
        const int year = 2091;

        // Every create starts at the same moment, including the race to insert the year's first counter row.
        using var start = new ManualResetEventSlim();
        var creates = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(async () =>
            {
                start.Wait();
                return (await _database.CreateClaimAsync(year)).ClaimNumber;
            }))
            .ToList();
        start.Set();
        var numbers = await Task.WhenAll(creates);

        numbers.Distinct().Count().ShouldBe(50);
        numbers.Order().ShouldBe(Enumerable.Range(1, 50).Select(sequence => $"CLM-{year}-{sequence:D7}"));

        await using var scope = await _database.TenantScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        (await dbContext.Claims.CountAsync(claim => claim.ClaimNumber.StartsWith($"CLM-{year}-"))).ShouldBe(50);
        (await LastValueAsync(dbContext, year)).ShouldBe(50);
    }

    [Fact]
    public async Task BR_C_04_Rolled_back_create_does_not_consume_number()
    {
        const int year = 2092;
        (await _database.CreateClaimAsync(year)).ClaimNumber.ShouldBe($"CLM-{year}-0000001");

        // The number is drawn, then the unit of work fails before commit.
        await Should.ThrowAsync<InvalidOperationException>(() => _database.CreateClaimAsync(year, beforeCommit: _ => throw new InvalidOperationException("Simulated failure")));

        (await _database.CreateClaimAsync(year)).ClaimNumber.ShouldBe($"CLM-{year}-0000002");
    }

    [Fact]
    public async Task BR_C_04_Counter_is_per_year()
    {
        (await _database.CreateClaimAsync(2093)).ClaimNumber.ShouldBe("CLM-2093-0000001");
        (await _database.CreateClaimAsync(2094)).ClaimNumber.ShouldBe("CLM-2094-0000001");
        (await _database.CreateClaimAsync(2093)).ClaimNumber.ShouldBe("CLM-2093-0000002");
    }

    [Fact]
    public async Task BR_C_04_Numbers_are_only_drawn_inside_a_transaction()
    {
        await using var scope = await _database.TenantScopeAsync();

        await Should.ThrowAsync<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<IClaimNumberGenerator>().NextAsync(2095, CancellationToken.None));
    }

    [Fact]
    public async Task BR_C_04_Duplicate_claim_number_is_refused_by_the_database()
    {
        var first = await _database.CreateClaimAsync(2096);
        var second = await _database.CreateClaimAsync(2096);
        await using var scope = await _database.TenantScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var claim = await dbContext.Claims.SingleAsync(candidate => candidate.Id == second.Id);

        dbContext.Entry(claim).Property(nameof(claim.ClaimNumber)).CurrentValue = first.ClaimNumber;

        (await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync()))
            .InnerException!.Message.ShouldContain("UX_Claims_OrganisationId_ClaimNumber");
    }

    private static async Task<int> LastValueAsync(ClaimsDbContext dbContext, int year) =>
        (await dbContext.Database.SqlQuery<int>($"SELECT [LastValue] AS [Value] FROM [ClaimNumberCounters] WHERE [Year] = {year}").ToListAsync()).Single();
}
