using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Audit;

namespace ClaimsModule.Persistence;

/// <summary>Only stages the row, so it commits or rolls back with the change it describes.</summary>
internal sealed class AuditLogService(
    ClaimsDbContext dbContext,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    ICorrelationContext correlationContext) : IAuditLogService
{
    public void Record(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        dbContext.ClaimAuditLog.Add(ClaimAuditLog.Create(
            entry.ClaimId,
            entry.EventType,
            entry.Description,
            entry.OldValue,
            entry.NewValue,
            entry.RelatedEntityId,
            entry.RelatedEntityType,
            // The middleware and the jobs only ever set GUIDs (D-39), so a parse failure is a bug and fails the
            // command rather than writing an unlinked audit row.
            Guid.Parse(correlationContext.CorrelationId),
            timeProvider.GetUtcNow(),
            currentUser.UserId));
    }
}
