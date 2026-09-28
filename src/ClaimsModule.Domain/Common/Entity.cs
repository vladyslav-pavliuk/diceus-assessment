namespace ClaimsModule.Domain.Common;

/// <summary>
/// The id is assigned at construction, so domain events carry real ids before SaveChanges (D-30).
/// Audit, soft-delete, tenant and RowVer columns are EF Core shadow properties, not domain state.
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
