using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Audit;

/// <summary>
/// One row of the immutable, append-only claim event log (FRS §9.8, §14, BR-A-01).
/// Created only through IAuditLogService (FRS §14.2). There is no method that changes a row, and
/// Persistence rejects modified or deleted rows, backed by a database trigger (D-14).
/// Unlike every other table it has no soft-delete, UpdatedAt or UserModified columns (D-14).
/// </summary>
public sealed class ClaimAuditLog : Entity
{
    private ClaimAuditLog()
    {
    }

    private ClaimAuditLog(Guid id)
        : base(id)
    {
    }

    public Guid ClaimId { get; private set; }

    public string EventType { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    /// <summary>Previous value as JSON.</summary>
    public string? OldValue { get; private set; }

    /// <summary>New value as JSON.</summary>
    public string? NewValue { get; private set; }

    public Guid? RelatedEntityId { get; private set; }

    public string? RelatedEntityType { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Null for the system actor (background jobs, D-33).</summary>
    public Guid? CreatedByUserId { get; private set; }

    public static ClaimAuditLog Create(
        Guid claimId,
        string eventType,
        string description,
        string? oldValue,
        string? newValue,
        Guid? relatedEntityId,
        string? relatedEntityType,
        Guid? correlationId,
        DateTimeOffset createdAt,
        Guid? createdByUserId)
    {
        if (claimId == Guid.Empty)
        {
            throw new ArgumentException("An audit entry must belong to a claim.", nameof(claimId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new ClaimAuditLog(SequentialGuid.NewGuid())
        {
            ClaimId = claimId,
            EventType = eventType,
            Description = description,
            OldValue = oldValue,
            NewValue = newValue,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            CorrelationId = correlationId,
            CreatedAt = createdAt.ToUniversalTime(),
            CreatedByUserId = createdByUserId,
        };
    }
}
