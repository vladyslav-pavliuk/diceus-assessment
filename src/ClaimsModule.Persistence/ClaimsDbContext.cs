using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Organisations;
using ClaimsModule.Domain.Users;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public sealed class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : DbContext(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);
        modelBuilder.ApplyStandardColumns();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // FRS §15.1: money DECIMAL(19,4); timestamps DATETIMEOFFSET(7).
        configurationBuilder.Properties<decimal>().HavePrecision(19, 4);
        configurationBuilder.Properties<DateTimeOffset>().HavePrecision(7);

        // CLAUDE.md rule 10: every domain enum is stored as NVARCHAR(50) text, never as an integer.
        foreach (var enumType in typeof(Entity).Assembly.GetTypes().Where(type => type.IsEnum))
        {
            configurationBuilder.Properties(enumType).HaveConversion<string>().HaveMaxLength(50);
        }
    }
}
