using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Audit;

/// <summary>
/// Append-only audit row (BR-A-01), written only through IAuditLogService. Persistence and a database
/// trigger reject updates and deletes, and the table has no soft-delete or modification columns (D-14).
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

    /// <summary>JSON.</summary>
    public string? OldValue { get; private set; }

    /// <summary>JSON.</summary>
    public string? NewValue { get; private set; }

    public Guid? RelatedEntityId { get; private set; }

    public string? RelatedEntityType { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Null for background jobs (D-33).</summary>
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
