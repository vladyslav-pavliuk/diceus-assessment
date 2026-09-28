using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Policies;

namespace ClaimsModule.Application.Abstractions.Persistence;

public interface IPolicyRepository
{
    Task<Policy?> GetAsync(Guid policyId, CancellationToken cancellationToken);
}

/// <summary>Global, not tenant-scoped (D-12).</summary>
public interface IStatusTransitionRepository
{
    Task<StatusTransitionTable> GetTableAsync(CancellationToken cancellationToken);
}
