namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// The only way to write ClaimAuditLog rows (FRS §14.2). The log is append-only: this service
/// has no update or delete members, and Persistence rejects modified or deleted audit rows (D-14).
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Stages an audit row in the current unit of work, so it commits or rolls back together with
    /// the state change it describes (CLAUDE.md rule 5). The service stamps CreatedAt (TimeProvider,
    /// UTC), CreatedByUserId (null for the system actor), OrganisationId and CorrelationId.
    /// </summary>
    void Record(AuditEntry entry);
}

/// <summary>One ClaimAuditLog row, as the caller describes it (FRS §9.8).</summary>
public sealed record AuditEntry(
    Guid ClaimId,
    string EventType,
    string Description,
    string? OldValue = null,
    string? NewValue = null,
    Guid? RelatedEntityId = null,
    string? RelatedEntityType = null);
