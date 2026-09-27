namespace ClaimsModule.Domain.Common;

/// <summary>
/// Base type for every entity. Identity is a GUID (FRS §15.1).
/// Audit columns (CreatedAt, UpdatedAt, UserCreated, UserModified) and soft-delete columns
/// (IsDeleted, DeletedAt) are EF Core shadow properties configured in Persistence, so the
/// domain model carries no persistence bookkeeping.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }
}
