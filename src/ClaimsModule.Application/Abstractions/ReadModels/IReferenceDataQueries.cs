using ClaimsModule.Application.Policies;
using ClaimsModule.Application.ReferenceData;
using ClaimsModule.Application.Users;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Abstractions.ReadModels;

/// <summary>Tenant-scoped, except the transition table (D-12).</summary>
public interface IReferenceDataQueries
{
    /// <summary>Active codes only, in SortOrder.</summary>
    Task<IReadOnlyList<CauseOfLossCodeDto>> ListCauseOfLossCodesAsync(PerilCategory? perilCategory, CancellationToken cancellationToken);

    /// <summary>BR-C-05.</summary>
    Task<bool> IsActiveCauseOfLossCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClaimStatusTransitionDto>> ListStatusTransitionsAsync(CancellationToken cancellationToken);
}

public interface IPolicyQueries
{
    /// <summary>Matches part of the policy number or client name.</summary>
    Task<IReadOnlyList<PolicyDto>> SearchAsync(string term, int maxResults, CancellationToken cancellationToken);

    Task<PolicyDto?> GetAsync(Guid policyId, CancellationToken cancellationToken);
}

public interface IUserQueries
{
    Task<IReadOnlyList<UserDto>> ListActiveAsync(UserRole? role, CancellationToken cancellationToken);
}
