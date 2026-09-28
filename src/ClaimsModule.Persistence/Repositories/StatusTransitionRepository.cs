using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Claims;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

/// <summary>A global table: only the soft-delete filter applies (D-12).</summary>
internal sealed class StatusTransitionRepository(ClaimsDbContext dbContext) : IStatusTransitionRepository
{
    public async Task<StatusTransitionTable> GetTableAsync(CancellationToken cancellationToken) =>
        new(await dbContext.ClaimStatusTransitions.AsNoTracking().ToListAsync(cancellationToken));
}
