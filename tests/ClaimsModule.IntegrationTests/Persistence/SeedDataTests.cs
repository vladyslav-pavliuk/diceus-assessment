using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.IntegrationTests.Fixtures;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Persistence;

/// <summary>
/// CONV-10: the seed comes from the migrations (never startup code) and matches the FRS tables
/// exactly. The expected values are typed in from the FRS here, independently of the seed code.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SeedDataTests(ApiFixture fixture)
{
    private readonly TestDatabase _database = new(fixture);

    [Fact]
    public async Task CONV_10_Policies_match_FRS_5_5()
    {
        await using var scope = await _database.TenantScopeAsync();
        var policies = await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Policies.ToListAsync();

        (string Number, string Client, string Effective, string Expiration, string[] Coverage)[] frs =
        [
            ("POL-2024-001001", "Meridian Transport LLC", "2024-01-01", "2026-12-31", ["Vehicle", "Cargo"]),
            ("POL-2024-001002", "Harborview Properties Inc", "2024-06-01", "2026-05-31", ["Property", "Liability"]),
            ("POL-2025-002001", "Coastal Builders Group", "2025-03-01", "2027-02-28", ["Property", "Equipment"]),
            ("POL-2025-002002", "Stanton Medical Group", "2025-01-01", "2026-12-31", ["Liability", "Vehicle"]),
            ("POL-2023-000099", "Archived Corp", "2020-01-01", "2021-12-31", ["Property"]),
        ];

        foreach (var expected in frs)
        {
            var policy = policies.Where(candidate => candidate.PolicyNumber == expected.Number).ShouldHaveSingleItem();
            policy.ClientName.ShouldBe(expected.Client);
            policy.EffectiveDate.ShouldBe(DateOnly.Parse(expected.Effective, System.Globalization.CultureInfo.InvariantCulture));
            policy.ExpirationDate.ShouldBe(DateOnly.Parse(expected.Expiration, System.Globalization.CultureInfo.InvariantCulture));
            policy.CoverageTypes.ShouldBe(expected.Coverage);
        }

        policies.Single(policy => policy.PolicyNumber == "POL-2023-000099").Status.ShouldBe(PolicyStatus.Expired);

        // D-34: three long-dated extras so the demo always has in-force policies.
        policies.Count.ShouldBe(8);
        policies.Count(policy => policy.ExpirationDate == new DateOnly(2030, 12, 31)).ShouldBe(3);
    }

    [Fact]
    public async Task CONV_10_Cause_of_loss_codes_match_FRS_5_6()
    {
        await using var scope = await _database.TenantScopeAsync();
        var codes = await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().CauseOfLossCodes.OrderBy(code => code.SortOrder).ToListAsync();

        codes.Select(code => (code.Code, code.Name, code.PerilCategory, code.IsActive)).ShouldBe(
        [
            ("COL-FIRE", "Fire", PerilCategory.Property, true),
            ("COL-FLOOD", "Flood", PerilCategory.Weather, true),
            ("COL-THEFT", "Theft", PerilCategory.Crime, true),
            ("COL-VEH-COL", "Vehicle Collision", PerilCategory.Auto, true),
            ("COL-VEH-COMP", "Vehicle Comprehensive", PerilCategory.Auto, true),
            ("COL-LIAB", "Third Party Liability", PerilCategory.Liability, true),
            ("COL-EQUIP", "Equipment Breakdown", PerilCategory.Equipment, true),
            ("COL-WIND", "Wind / Storm", PerilCategory.Weather, true),
            ("COL-INJURY", "Bodily Injury", PerilCategory.Liability, true),
            ("COL-OTHER", "Other / Unknown", PerilCategory.General, true),
        ]);
    }

    [Fact]
    public async Task CONV_10_Status_transitions_are_the_domain_table()
    {
        await using var scope = await _database.TenantScopeAsync();
        var seeded = await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().ClaimStatusTransitions.ToListAsync();

        static object Shape(ClaimStatusTransition row) => (row.FromStatus, row.ToStatus, row.MinimumRole, row.RequiresReason, row.IsSystemOnly);

        seeded.Select(Shape).ShouldBe(ClaimStatusTransition.FrsDefaults().Select(Shape), ignoreOrder: true);
    }

    [Fact]
    public async Task CONV_10_The_organisation_and_its_users_are_seeded()
    {
        await using var scope = await _database.TenantScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();

        // The seeded users belong to the seeded organisation (they are visible through its tenant filter).
        (await dbContext.Users.CountAsync()).ShouldBe(6);
    }
}
