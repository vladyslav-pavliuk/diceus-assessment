using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Organisations;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Conventions;

/// <summary>
/// The FRS §15.1/§15.2 column conventions, applied to every domain entity in one place so that no
/// entity configuration can forget them. Runs before the per-entity configurations, so those can
/// refer to the shadow columns (for example in an index on OrganisationId).
/// </summary>
internal static class ModelBuilderConventions
{
    /// <summary>
    /// D-14: the audit log is append-only, so it has no soft-delete columns and no UpdatedAt or
    /// UserModified. It keeps its own business CreatedAt and CreatedByUserId (FRS §9.8).
    /// </summary>
    private static readonly HashSet<Type> AppendOnlyEntities = [typeof(ClaimAuditLog)];

    /// <summary>D-12: the tenant itself and global system configuration carry no OrganisationId.</summary>
    private static readonly HashSet<Type> GlobalEntities = [typeof(Organisation), typeof(ClaimStatusTransition)];

    public static bool IsAppendOnly(Type clrType) => AppendOnlyEntities.Contains(clrType);

    public static bool IsSoftDeletable(Type clrType) => IsDomainEntity(clrType) && !IsAppendOnly(clrType);

    public static bool IsTenantOwned(Type clrType) => IsDomainEntity(clrType) && !GlobalEntities.Contains(clrType);

    public static void ApplyStandardColumns(this ModelBuilder modelBuilder)
    {
        // Map every domain entity, including the Claim's children that have no DbSet.
        var entityTypes = typeof(Entity).Assembly.GetTypes().Where(type => IsDomainEntity(type) && !type.IsAbstract);

        foreach (var clrType in entityTypes)
        {
            var entity = modelBuilder.Entity(clrType);

            // Ids are assigned by the domain (sequential GUIDs, D-30); the default covers rows inserted outside EF.
            // ValueGeneratedNever matters: with a store-generated key, EF treats a new child that already has an
            // id (a party or reserve transaction added to a loaded claim) as an existing row and UPDATEs it.
            entity.Property(nameof(Entity.Id)).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedNever();

            if (IsTenantOwned(clrType))
            {
                // A real property where the domain has one (User); a shadow property everywhere else.
                entity.Property<Guid>(ShadowColumns.OrganisationId).IsRequired();
            }

            if (IsAppendOnly(clrType))
            {
                continue;
            }

            // Audit columns, filled by AuditColumnsInterceptor.
            entity.Property<DateTimeOffset>(ShadowColumns.CreatedAt).IsRequired();
            entity.Property<DateTimeOffset?>(ShadowColumns.UpdatedAt);
            entity.Property<Guid?>(ShadowColumns.UserCreated);
            entity.Property<Guid?>(ShadowColumns.UserModified);

            // Soft delete: IsDeleted BIT NOT NULL DEFAULT 0 + DeletedAt. The query filter is added by the
            // DbContext, together with the tenant filter (EF Core 9 allows one filter per entity).
            entity.Property<bool>(ShadowColumns.IsDeleted).IsRequired().HasDefaultValue(false);
            entity.Property<DateTimeOffset?>(ShadowColumns.DeletedAt);
        }
    }

    private static bool IsDomainEntity(Type clrType) => typeof(Entity).IsAssignableFrom(clrType);
}
