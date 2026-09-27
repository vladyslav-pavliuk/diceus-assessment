namespace ClaimsModule.Domain.Common;

/// <summary>
/// Base type for every entity. Identity is a GUID (FRS §15.1), assigned at construction with
/// <see cref="SequentialGuid"/> so that an entity is complete from birth and domain events can carry
/// real ids before SaveChanges (D-30).
/// Audit columns (CreatedAt, UpdatedAt, UserCreated, UserModified), soft-delete columns
/// (IsDeleted, DeletedAt), OrganisationId and RowVer are EF Core shadow properties configured in
/// Persistence, so the domain model carries no persistence bookkeeping.
/// </summary>
public abstract class Entity
{
    /// <summary>For EF Core materialisation and HasData seeding.</summary>
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An entity id cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; private set; }
}
