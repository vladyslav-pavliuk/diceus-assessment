using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

internal sealed class PolicyRepository(ClaimsDbContext dbContext) : IPolicyRepository
{
    // No tracking: claims commands never change a policy.
    public Task<Policy?> GetAsync(Guid policyId, CancellationToken cancellationToken) =>
        dbContext.Policies.AsNoTracking().SingleOrDefaultAsync(policy => policy.Id == policyId, cancellationToken);
}
