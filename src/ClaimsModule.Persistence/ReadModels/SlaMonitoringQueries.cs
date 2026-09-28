using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.ReadModels;

/// <summary>
/// The SLA job's query (FRS §12.2, D-01), one SQL statement for the current tenant. UpdatedAt is null for a
/// claim never changed since creation, and those are exactly the stale ones, so the age is COALESCE(UpdatedAt,
/// CreatedAt). The "no breach in the last 24h" check uses IX_ClaimAuditLog_ClaimId_EventType_CreatedAt.
/// </summary>
internal sealed class SlaMonitoringQueries(ClaimsDbContext dbContext) : ISlaMonitoringQueries
{
    public async Task<IReadOnlyList<SlaBreachCandidate>> ListBreachCandidatesAsync(
        DateTimeOffset staleBefore, DateTimeOffset lastBreachAfter, CancellationToken cancellationToken)
    {
        var statuses = SlaPolicy.MonitoredStatuses.ToList();

        return await dbContext.Claims.AsNoTracking()
            .Where(claim => statuses.Contains(claim.Status))
            .Select(claim => new
            {
                claim.Id,
                claim.ClaimNumber,
                claim.Status,
                LastUpdatedAt = EF.Property<DateTimeOffset?>(claim, ShadowColumns.UpdatedAt) ?? EF.Property<DateTimeOffset>(claim, ShadowColumns.CreatedAt),
            })
            .Where(claim => claim.LastUpdatedAt < staleBefore)
            .Where(claim => !dbContext.ClaimAuditLog.Any(entry =>
                entry.ClaimId == claim.Id
                && entry.EventType == AuditEventTypes.SlaBreachDetected
                && entry.CreatedAt > lastBreachAfter))
            .OrderBy(claim => claim.LastUpdatedAt)
            .Select(claim => new SlaBreachCandidate(claim.Id, claim.ClaimNumber, claim.Status, claim.LastUpdatedAt))
            .ToListAsync(cancellationToken);
    }
}
