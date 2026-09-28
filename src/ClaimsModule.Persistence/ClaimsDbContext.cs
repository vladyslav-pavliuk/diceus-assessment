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

    public DbSet<ReserveTransaction> ReserveHistory => Set<ReserveTransaction>();

    public DbSet<ClaimAuditLog> ClaimAuditLog => Set<ClaimAuditLog>();

    internal DbSet<ClaimNumberCounter> ClaimNumberCounters => Set<ClaimNumberCounter>();

    /// <summary>Guid.Empty without a tenant, which matches no row, so the filter fails closed.</summary>
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
        configurationBuilder.Properties<decimal>().HavePrecision(19, 4);
        configurationBuilder.Properties<DateTimeOffset>().HavePrecision(7);

        foreach (var enumType in typeof(Entity).Assembly.GetTypes().Where(type => type.IsEnum))
        {
            configurationBuilder.Properties(enumType).HaveConversion<string>().HaveMaxLength(50);
        }
    }

    /// <summary>
    /// Soft delete and tenant in one filter, since EF Core 9 allows one per entity. A lambda over this instance, so
    /// <see cref="CurrentOrganisationId"/> is a per-query parameter rather than a value cached in the model.
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
