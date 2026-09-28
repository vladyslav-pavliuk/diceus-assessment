using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

/// <summary>Policies for commands. The tenant filter applies, so another organisation's policy is not found.</summary>
internal sealed class PolicyRepository(ClaimsDbContext dbContext) : IPolicyRepository
{
    // No tracking: a policy is reference data read by the claim, never changed by a claims command.
    public Task<Policy?> GetAsync(Guid policyId, CancellationToken cancellationToken) =>
        dbContext.Policies.AsNoTracking().SingleOrDefaultAsync(policy => policy.Id == policyId, cancellationToken);
}
