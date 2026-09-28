using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Tenancy;

/// <summary>
/// IgnoreQueryFilters() also lifts the soft-delete filter, so that condition is re-applied by hand (D-31).
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
