using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClaimsModule.Persistence.Interceptors;

/// <summary>
/// Fills the convention columns and turns deletes into soft deletes. It also touches the Claim row whenever anything in
/// its aggregate changes, so the Claim RowVer serialises concurrent commands, and refuses cross-tenant writes (D-12).
/// </summary>
internal sealed class AuditColumnsInterceptor(TimeProvider timeProvider, ICurrentUser currentUser, ITenantContext tenantContext)
    : SaveChangesInterceptor
{
    private const string ClaimIdProperty = nameof(ClaimParty.ClaimId);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        TouchChangedClaims(context, now);

        foreach (var entry in context.ChangeTracker.Entries().Where(IsChanged).ToList())
        {
            StampTenant(entry);
            StampAuditColumns(entry, now, userId);
        }
    }

    private static void TouchChangedClaims(DbContext context, DateTimeOffset now)
    {
        var changedClaimIds = context.ChangeTracker.Entries()
            .Where(entry => IsChanged(entry)
                && entry.Entity is not Claim
                && !ModelBuilderConventions.IsAppendOnly(entry.Metadata.ClrType)
                && entry.Metadata.FindProperty(ClaimIdProperty) is not null)
            .Select(entry => (Guid)entry.Property(ClaimIdProperty).CurrentValue!)
            .ToHashSet();

        foreach (var claim in context.ChangeTracker.Entries<Claim>().Where(claim => claim.State == EntityState.Unchanged && changedClaimIds.Contains(claim.Entity.Id)))
        {
            // Marks only UpdatedAt as modified; EF then includes RowVer in the UPDATE's WHERE clause.
            var updatedAt = claim.Property(ShadowColumns.UpdatedAt);
            updatedAt.CurrentValue = now;
            updatedAt.IsModified = true;
        }
    }

    private void StampTenant(EntityEntry entry)
    {
        if (entry.Metadata.FindProperty(ShadowColumns.OrganisationId) is null)
        {
            return;
        }

        var organisationId = entry.Property(ShadowColumns.OrganisationId);
        if (entry.State == EntityState.Added)
        {
            var tenant = tenantContext.OrganisationId
                ?? throw new InvalidOperationException($"Cannot insert {entry.Metadata.ClrType.Name}: no tenant scope is set (D-31).");

            var current = (Guid)organisationId.CurrentValue!;
            if (current == Guid.Empty)
            {
                organisationId.CurrentValue = tenant;
            }
            else if (current != tenant)
            {
                throw new InvalidOperationException($"Cannot insert {entry.Metadata.ClrType.Name} for another organisation.");
            }
        }
        else if (organisationId.IsModified)
        {
            throw new InvalidOperationException($"The organisation of {entry.Metadata.ClrType.Name} cannot change.");
        }
    }

    private static void StampAuditColumns(EntityEntry entry, DateTimeOffset now, Guid? userId)
    {
        if (entry.Metadata.FindProperty(ShadowColumns.UserCreated) is null)
        {
            // No audit columns on the audit log and the claim-number counter (D-14).
            return;
        }

        switch (entry.State)
        {
            case EntityState.Added:
                entry.Property(ShadowColumns.CreatedAt).CurrentValue = now;
                entry.Property(ShadowColumns.UserCreated).CurrentValue = userId;
                break;

            case EntityState.Deleted:
                entry.State = EntityState.Modified;
                entry.Property(ShadowColumns.IsDeleted).CurrentValue = true;
                entry.Property(ShadowColumns.DeletedAt).CurrentValue = now;
                goto case EntityState.Modified;

            case EntityState.Modified:
                entry.Property(ShadowColumns.UpdatedAt).CurrentValue = now;
                entry.Property(ShadowColumns.UserModified).CurrentValue = userId;
                break;
        }
    }

    private static bool IsChanged(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
}
