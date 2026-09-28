using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Policies;

namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>Loads a policy for a command (claim creation, policy linking). Tenant-scoped: another organisation's policy is not found.</summary>
public interface IPolicyRepository
{
    Task<Policy?> GetAsync(Guid policyId, CancellationToken cancellationToken);
}

/// <summary>Loads the ClaimStatusTransitions rows the aggregate enforces (FRS §4.2, D-09). Global, not tenant-scoped (D-12).</summary>
public interface IStatusTransitionRepository
{
    Task<StatusTransitionTable> GetTableAsync(CancellationToken cancellationToken);
}
