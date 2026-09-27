using System.Linq.Expressions;
using System.Reflection;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Organisations;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Domain.Users;
using ClaimsModule.Persistence.ClaimNumbers;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public sealed class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options, ITenantContext tenantContext) : DbContext(options)
{
    private static readonly MethodInfo ApplyQueryFilterMethod =
        typeof(ClaimsDbContext).GetMethod(nameof(ApplyQueryFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Policy> Policies => Set<Policy>();

    public DbSet<CauseOfLossCode> CauseOfLossCodes => Set<CauseOfLossCode>();

    public DbSet<ClaimStatusTransition> ClaimStatusTransitions => Set<ClaimStatusTransition>();

    public DbSet<Claim> Claims => Set<Claim>();

    /// <summary>The ReserveHistory table (FRS §9.6), for read models and the GL job.</summary>
    public DbSet<ReserveTransaction> ReserveHistory => Set<ReserveTransaction>();

    public DbSet<ClaimAuditLog> ClaimAuditLog => Set<ClaimAuditLog>();

    internal DbSet<ClaimNumberCounter> ClaimNumberCounters => Set<ClaimNumberCounter>();

    /// <summary>
    /// The tenant the global query filter compares against. EF Core evaluates it per query on this
    /// context instance. No tenant (anonymous request, job before it sets its scope) → Guid.Empty,
    /// which matches no row: the filter fails closed.
    /// </summary>
    public Guid CurrentOrganisationId => tenantContext.OrganisationId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyStandardColumns();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => typeof(Entity).IsAssignableFrom(type.ClrType)))
        {
            ApplyQueryFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
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

    /// <summary>
    /// One combined filter per entity (EF Core 9 supports one): soft delete (FRS §15.2) and tenant
    /// isolation (FRS §15.1, D-12). Written as a lambda over this instance so EF Core parameterises
    /// <see cref="CurrentOrganisationId"/> per query instead of caching the value in the model.
    /// </summary>
    private void ApplyQueryFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : Entity
    {
        var softDelete = ModelBuilderConventions.IsSoftDeletable(typeof(TEntity));
        var tenant = ModelBuilderConventions.IsTenantOwned(typeof(TEntity));

        Expression<Func<TEntity, bool>>? filter = (softDelete, tenant) switch
        {
            (true, true) => entity => !EF.Property<bool>(entity, ShadowColumns.IsDeleted)
                && EF.Property<Guid>(entity, ShadowColumns.OrganisationId) == CurrentOrganisationId,
            (true, false) => entity => !EF.Property<bool>(entity, ShadowColumns.IsDeleted),
            (false, true) => entity => EF.Property<Guid>(entity, ShadowColumns.OrganisationId) == CurrentOrganisationId,
            (false, false) => null,
        };

        if (filter is not null)
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
        }
    }
}
