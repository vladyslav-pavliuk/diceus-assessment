using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Tenancy;

/// <summary>
/// The cross-tenant lookups of the background jobs (D-31), kept in one place. A job has no user and therefore
/// no tenant yet, so the tenant filter would hide everything. EF Core 9 has no way to lift only the tenant
/// filter, and IgnoreQueryFilters() lifts the soft-delete filter too, so that condition is re-applied by hand.
/// </summary>
internal sealed class TenantDirectory(ClaimsDbContext dbContext) : ITenantDirectory
{
    public async Task<IReadOnlyList<Guid>> ListOrganisationIdsAsync(CancellationToken cancellationToken) =>
        await dbContext.Organisations.AsNoTracking()
            .OrderBy(organisation => organisation.Id)
            .Select(organisation => organisation.Id)
            .ToListAsync(cancellationToken);

    public Task<Guid?> FindOrganisationOfClaimAsync(Guid claimId, CancellationToken cancellationToken) =>
        dbContext.Claims.AsNoTracking()
            .IgnoreQueryFilters() // D-31: no tenant scope yet; soft delete re-applied below.
            .Where(claim => claim.Id == claimId && !EF.Property<bool>(claim, ShadowColumns.IsDeleted))
            .Select(claim => (Guid?)EF.Property<Guid>(claim, ShadowColumns.OrganisationId))
            .SingleOrDefaultAsync(cancellationToken);
}
