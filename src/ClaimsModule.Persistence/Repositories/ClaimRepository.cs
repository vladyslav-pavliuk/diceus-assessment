using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Claims;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

internal sealed class ClaimRepository(ClaimsDbContext dbContext) : IClaimRepository
{
    /// <summary>
    /// The full aggregate (D-39 Q2). A split query avoids the cartesian explosion of five collection
    /// includes; the tenant and soft-delete filters apply to the root and to every child.
    /// </summary>
    public Task<Claim?> GetAsync(Guid claimId, CancellationToken cancellationToken) =>
        dbContext.Claims
            .Include(claim => claim.LossEvent)
            .Include(claim => claim.Parties)
            .Include(claim => claim.RiskObjects)
            .Include(claim => claim.ValidationIssues)
            .Include(claim => claim.Documents)
            .Include(claim => claim.ReserveComponents).ThenInclude(component => component.Transactions)
            .AsSplitQuery()
            .SingleOrDefaultAsync(claim => claim.Id == claimId, cancellationToken);

    public void Add(Claim claim) => dbContext.Claims.Add(claim);
}
