using ClaimsModule.Application.Policies;
using ClaimsModule.Application.ReferenceData;
using ClaimsModule.Application.Users;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Abstractions.ReadModels;

/// <summary>Reference data reads (FRS §10.3), also used by validators for BR-C-05. Tenant-scoped except the transition table (D-12).</summary>
public interface IReferenceDataQueries
{
    /// <summary>Active codes only, in SortOrder (FRS §10.3 "List all active cause of loss codes").</summary>
    Task<IReadOnlyList<CauseOfLossCodeDto>> ListCauseOfLossCodesAsync(PerilCategory? perilCategory, CancellationToken cancellationToken);

    /// <summary>BR-C-05: the code exists and is active in the caller's organisation.</summary>
    Task<bool> IsActiveCauseOfLossCodeAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClaimStatusTransitionDto>> ListStatusTransitionsAsync(CancellationToken cancellationToken);
}

/// <summary>The simulated policy lookup (FRS §5.5, §10.3). Tenant-scoped.</summary>
public interface IPolicyQueries
{
    /// <summary>Policies whose number or client name contains <paramref name="term"/>, by policy number.</summary>
    Task<IReadOnlyList<PolicyDto>> SearchAsync(string term, int maxResults, CancellationToken cancellationToken);

    Task<PolicyDto?> GetAsync(Guid policyId, CancellationToken cancellationToken);
}

/// <summary>Users of the caller's organisation (D-29 handler filter, D-18 assign dialog).</summary>
public interface IUserQueries
{
    Task<IReadOnlyList<UserDto>> ListActiveAsync(UserRole? role, CancellationToken cancellationToken);
}
