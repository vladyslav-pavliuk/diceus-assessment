using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Organisations;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Conventions;

/// <summary>
/// Applied to every entity in one place, so no configuration can forget them. Runs first, so per-entity
/// configurations can refer to the shadow columns.
/// </summary>
internal static class ModelBuilderConventions
{
    /// <summary>Append-only, so no soft-delete or modification columns (D-14).</summary>
    private static readonly HashSet<Type> AppendOnlyEntities = [typeof(ClaimAuditLog)];

    /// <summary>The tenant itself and global configuration carry no OrganisationId (D-12).</summary>
    private static readonly HashSet<Type> GlobalEntities = [typeof(Organisation), typeof(ClaimStatusTransition)];

    public static bool IsAppendOnly(Type clrType) => AppendOnlyEntities.Contains(clrType);

    public static bool IsSoftDeletable(Type clrType) => IsDomainEntity(clrType) && !IsAppendOnly(clrType);

    public static bool IsTenantOwned(Type clrType) => IsDomainEntity(clrType) && !GlobalEntities.Contains(clrType);

    public static void ApplyStandardColumns(this ModelBuilder modelBuilder)
    {
        var entityTypes = typeof(Entity).Assembly.GetTypes().Where(type => IsDomainEntity(type) && !type.IsAbstract);

        foreach (var clrType in entityTypes)
        {
            var entity = modelBuilder.Entity(clrType);

            // Ids come from the domain (D-30); the default covers rows inserted outside EF. ValueGeneratedNever matters:
            // otherwise EF treats a new child that already has an id as an existing row and UPDATEs it.
            entity.Property(nameof(Entity.Id)).HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedNever();

            if (IsTenantOwned(clrType))
            {
                entity.Property<Guid>(ShadowColumns.OrganisationId).IsRequired();
            }

            if (IsAppendOnly(clrType))
            {
                continue;
            }

            entity.Property<DateTimeOffset>(ShadowColumns.CreatedAt).IsRequired();
            entity.Property<DateTimeOffset?>(ShadowColumns.UpdatedAt);
            entity.Property<Guid?>(ShadowColumns.UserCreated);
            entity.Property<Guid?>(ShadowColumns.UserModified);

            // The query filter is added by the DbContext, together with the tenant filter.
            entity.Property<bool>(ShadowColumns.IsDeleted).IsRequired().HasDefaultValue(false);
            entity.Property<DateTimeOffset?>(ShadowColumns.DeletedAt);
        }
    }

    private static bool IsDomainEntity(Type clrType) => typeof(Entity).IsAssignableFrom(clrType);
}
