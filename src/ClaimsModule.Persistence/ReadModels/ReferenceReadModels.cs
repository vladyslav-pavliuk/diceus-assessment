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

    /// <summary>SQL Server compares case-insensitively, so the code is re-checked ordinally: it is an exact identifier.</summary>
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

    // Hand-written: EF cannot translate ProjectTo's per-element copy of the JSON CoverageTypes column.
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

/// <summary>Both filters apply, unlike the sign-in lookups.</summary>
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
