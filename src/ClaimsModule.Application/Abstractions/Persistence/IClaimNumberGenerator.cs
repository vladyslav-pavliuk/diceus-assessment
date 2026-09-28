using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// Call inside the claim-creation transaction: the counter commits or rolls back with the claim, which keeps
/// the sequence gap-free (BR-C-04, D-10).
/// </summary>
public interface IClaimNumberGenerator
{
    Task<ClaimNumber> NextAsync(int year, CancellationToken cancellationToken);
}
