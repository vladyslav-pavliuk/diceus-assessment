using System.Linq.Expressions;
using ClaimsModule.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Conventions;

/// <summary>
/// The FRS §15.1/§15.2 column conventions, applied to every entity in one place so that no
/// entity configuration can forget them.
/// </summary>
internal static class ModelBuilderConventions
{
    public static void ApplyStandardColumns(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => typeof(Entity).IsAssignableFrom(type.ClrType)))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);

            // Ids are assigned by the domain (sequential GUIDs, D-30); the default covers rows inserted outside EF.
            entity.Property(nameof(Entity.Id)).HasDefaultValueSql("NEWSEQUENTIALID()");

            // Audit columns, filled by a SaveChanges interceptor (Phase 2).
            entity.Property<DateTimeOffset>(ShadowColumns.CreatedAt).IsRequired();
            entity.Property<DateTimeOffset?>(ShadowColumns.UpdatedAt);
            entity.Property<Guid?>(ShadowColumns.UserCreated);
            entity.Property<Guid?>(ShadowColumns.UserModified);

            // Soft delete: IsDeleted BIT NOT NULL DEFAULT 0 + DeletedAt, hidden by a global query filter.
            // Phase 2 combines this filter with the tenant filter (EF Core 9 allows one filter per entity).
            entity.Property<bool>(ShadowColumns.IsDeleted).IsRequired().HasDefaultValue(false);
            entity.Property<DateTimeOffset?>(ShadowColumns.DeletedAt);
            entity.HasQueryFilter(BuildNotDeletedFilter(entityType.ClrType));
        }
    }

    // e => !EF.Property<bool>(e, "IsDeleted")
    private static LambdaExpression BuildNotDeletedFilter(Type clrType)
    {
        var entity = Expression.Parameter(clrType, "entity");
        var isDeleted = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(bool)],
            entity,
            Expression.Constant(ShadowColumns.IsDeleted));

        return Expression.Lambda(Expression.Not(isDeleted), entity);
    }
}
