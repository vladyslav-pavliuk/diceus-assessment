using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// Hands out the next claim number for the current organisation and the given year (FRS §5.3,
/// BR-C-04, D-10). Must be called inside the claim-creation transaction: the counter increment
/// commits or rolls back with the claim, which is what makes the sequence gap-free.
/// </summary>
public interface IClaimNumberGenerator
{
    Task<ClaimNumber> NextAsync(int year, CancellationToken cancellationToken);
}
