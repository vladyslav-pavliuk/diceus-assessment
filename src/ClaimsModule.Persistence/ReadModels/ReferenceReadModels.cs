using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Policies;
using ClaimsModule.Application.ReferenceData;
using ClaimsModule.Application.Users;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.ReadModels;

/// <summary>Reference data (FRS §10.3). Cause codes are per organisation; the transition table is global (D-12).</summary>
internal sealed class ReferenceDataQueries(ClaimsDbContext dbContext, IMapper mapper) : IReferenceDataQueries
{
    public async Task<IReadOnlyList<CauseOfLossCodeDto>> ListCauseOfLossCodesAsync(PerilCategory? perilCategory, CancellationToken cancellationToken)
    {
        var codes = dbContext.CauseOfLossCodes.AsNoTracking().Where(code => code.IsActive);
        if (perilCategory is { } category)
        {
            codes = codes.Where(code => code.PerilCategory == category);
        }

        return await codes.OrderBy(code => code.SortOrder)
            .ProjectTo<CauseOfLossCodeDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// BR-C-05. SQL Server compares case-insensitively, so the stored code is compared ordinally here: a
    /// code is an exact identifier, and a claim should never store "col-fire" for "COL-FIRE".
    /// </summary>
    public async Task<bool> IsActiveCauseOfLossCodeAsync(string code, CancellationToken cancellationToken)
    {
        var stored = await dbContext.CauseOfLossCodes.AsNoTracking()
            .Where(candidate => candidate.Code == code && candidate.IsActive)
            .Select(candidate => candidate.Code)
            .FirstOrDefaultAsync(cancellationToken);

        return string.Equals(stored, code, StringComparison.Ordinal);
    }

    public async Task<IReadOnlyList<ClaimStatusTransitionDto>> ListStatusTransitionsAsync(CancellationToken cancellationToken) =>
        await dbContext.ClaimStatusTransitions.AsNoTracking()
            .ProjectTo<ClaimStatusTransitionDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
}

/// <summary>The simulated policy lookup (FRS §5.5, §10.3). Tenant-scoped.</summary>
internal sealed class PolicyQueries(ClaimsDbContext dbContext) : IPolicyQueries
{
    public async Task<IReadOnlyList<PolicyDto>> SearchAsync(string term, int maxResults, CancellationToken cancellationToken) =>
        await ToDtos(dbContext.Policies.AsNoTracking()
                .Where(policy => policy.PolicyNumber.Contains(term) || policy.ClientName.Contains(term))
                .OrderBy(policy => policy.PolicyNumber)
                .Take(maxResults))
            .ToListAsync(cancellationToken);

    public Task<PolicyDto?> GetAsync(Guid policyId, CancellationToken cancellationToken) =>
        ToDtos(dbContext.Policies.AsNoTracking().Where(policy => policy.Id == policyId)).SingleOrDefaultAsync(cancellationToken);

    // A hand-written projection: CoverageTypes is a JSON column behind a value converter, which EF
    // reads as a whole but cannot translate the per-element copy ProjectTo would generate.
    private static IQueryable<PolicyDto> ToDtos(IQueryable<Policy> policies) =>
        policies.Select(policy => new PolicyDto
        {
            Id = policy.Id,
            PolicyNumber = policy.PolicyNumber,
            ClientName = policy.ClientName,
            EffectiveDate = policy.EffectiveDate,
            ExpirationDate = policy.ExpirationDate,
            Status = policy.Status,
            CoverageTypes = policy.CoverageTypes,
        });
}

/// <summary>Users of the caller's organisation (D-29, D-18). Both filters apply, unlike the sign-in lookups.</summary>
internal sealed class UserQueries(ClaimsDbContext dbContext, IMapper mapper) : IUserQueries
{
    public async Task<IReadOnlyList<UserDto>> ListActiveAsync(UserRole? role, CancellationToken cancellationToken)
    {
        var users = dbContext.Users.AsNoTracking().Where(user => user.IsActive);
        if (role is { } wanted)
        {
            users = users.Where(user => user.Role == wanted);
        }

        return await users.OrderBy(user => user.DisplayName)
            .ProjectTo<UserDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
