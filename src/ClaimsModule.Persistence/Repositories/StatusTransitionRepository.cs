using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Claims;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

/// <summary>The seeded ClaimStatusTransitions rows (FRS §4.2, D-09). A global table: only the soft-delete filter applies (D-12).</summary>
internal sealed class StatusTransitionRepository(ClaimsDbContext dbContext) : IStatusTransitionRepository
{
    public async Task<StatusTransitionTable> GetTableAsync(CancellationToken cancellationToken) =>
        new(await dbContext.ClaimStatusTransitions.AsNoTracking().ToListAsync(cancellationToken));
}
