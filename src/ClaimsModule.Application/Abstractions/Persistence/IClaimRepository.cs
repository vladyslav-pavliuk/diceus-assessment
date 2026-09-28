using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// Always loads the whole aggregate, including every reserve transaction, so rules see complete data (D-39).
/// Saving is the Unit of Work's job.
/// </summary>
public interface IClaimRepository
{
    Task<Claim?> GetAsync(Guid claimId, CancellationToken cancellationToken);

    void Add(Claim claim);
}
