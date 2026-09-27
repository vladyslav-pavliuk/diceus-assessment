using ClaimsModule.IntegrationTests.Fixtures;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Platform;

[Collection(ApiCollection.Name)]
public sealed class MigrationTests(ApiFixture fixture)
{
    [Fact]
    public async Task CONV_11_Migrations_build_the_database_from_zero()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Database;

        (await database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await database.GetAppliedMigrationsAsync()).ShouldContain(migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CONV_11_Model_has_no_changes_missing_from_the_migrations()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();

        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }
}
