using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Audit;

namespace ClaimsModule.Persistence;

/// <summary>
/// The single writer of ClaimAuditLog (FRS §14.2). It only stages the row on the current DbContext,
/// so the row commits or rolls back with the change it describes (CLAUDE.md rule 5). OrganisationId is
/// stamped by the tenant interceptor; there is no update or delete path (BR-A-01).
/// </summary>
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
            // FRS §9.8: a GUID. The middleware and the jobs only ever set GUIDs (D-39 Q1), so a parse
            // failure is a bug and fails the command rather than writing an unlinked audit row.
            Guid.Parse(correlationContext.CorrelationId),
            timeProvider.GetUtcNow(),
            currentUser.UserId));
    }
}
