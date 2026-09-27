using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// Loads and adds Claim aggregates for commands. Always the whole aggregate, including every reserve
/// transaction, so the domain checks its rules against complete data (D-39 Q2). Saving is the Unit of
/// Work's job; there is no Update or Delete here. Reads for screens are query projections, not this.
/// </summary>
public interface IClaimRepository
{
    Task<Claim?> GetAsync(Guid claimId, CancellationToken cancellationToken);

    void Add(Claim claim);
}
