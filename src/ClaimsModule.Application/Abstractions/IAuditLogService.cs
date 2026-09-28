namespace ClaimsModule.Application.Abstractions;

/// <summary>The only way to write audit rows. Append-only: there are no update or delete members (D-14).</summary>
public interface IAuditLogService
{
    /// <summary>
    /// Stages the row in the current unit of work, so it commits or rolls back with the state change.
    /// The service stamps the time, user, organisation and correlation id.
    /// </summary>
    void Record(AuditEntry entry);
}

public sealed record AuditEntry(
    Guid ClaimId,
    string EventType,
    string Description,
    string? OldValue = null,
    string? NewValue = null,
    Guid? RelatedEntityId = null,
    string? RelatedEntityType = null);
